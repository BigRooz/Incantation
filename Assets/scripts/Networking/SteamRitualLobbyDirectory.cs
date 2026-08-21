using System;
using Steamworks;
using UnityEngine;

namespace Incantation.Networking
{
    /// <summary>
    /// Provides Steam Lobby discovery for Ritual Seals. Lobby metadata is a session locator only;
    /// FishNet and the Host server remain authoritative for every gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamRitualLobbyDirectory : MonoBehaviour
    {
        public const string GameMetadataKey = "game";
        public const string GameMetadataValue = "INCANTATION_DEV";
        public const string SealMetadataKey = "ritualSeal";
        public const string ProtocolMetadataKey = "protocol";
        public const string ProtocolMetadataValue = "1";

        private const int LobbyCapacity = 4;
        private const int MaximumSealAttempts = 12;

        [SerializeField, Min(5f)] private float operationTimeout = 20f;

        private enum PendingOperation
        {
            None,
            CreateCollisionSearch,
            CreateLobby,
            JoinSearch,
            JoinLobby
        }

        private CallResult<LobbyMatchList_t> lobbyMatchListResult;
        private CallResult<LobbyCreated_t> lobbyCreatedResult;
        private CallResult<LobbyEnter_t> lobbyEnterResult;
        private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback;
        private PendingOperation pendingOperation;
        private Func<string> sealGenerator;
        private string candidateSeal = string.Empty;
        private string requestedJoinSeal = string.Empty;
        private int sealAttemptCount;
        private float operationDeadline;
        private CSteamID activeLobbyId = CSteamID.Nil;
        private bool ownsActiveLobby;
        private bool joiningInvitedLobby;

        public bool IsCreating =>
            pendingOperation == PendingOperation.CreateCollisionSearch ||
            pendingOperation == PendingOperation.CreateLobby;
        public bool IsJoining =>
            pendingOperation == PendingOperation.JoinSearch ||
            pendingOperation == PendingOperation.JoinLobby;
        public bool HasActiveLobby => activeLobbyId != CSteamID.Nil;
        public float OperationTimeout => operationTimeout;

        public event Action<string> HostedLobbyReady;
        public event Action<string, ulong> JoinedLobbyReady;
        public event Action<ulong> InvitedLobbyJoinRequested;
        public event Action<string> OperationFailed;

        private void Awake()
        {
            lobbyMatchListResult = CallResult<LobbyMatchList_t>.Create(HandleLobbyMatchList);
            lobbyCreatedResult = CallResult<LobbyCreated_t>.Create(HandleLobbyCreated);
            lobbyEnterResult = CallResult<LobbyEnter_t>.Create(HandleLobbyEntered);
            if (CanUseSteam())
            {
                gameLobbyJoinRequestedCallback =
                    Callback<GameLobbyJoinRequested_t>.Create(HandleGameLobbyJoinRequested);
            }
        }

        private void OnDestroy()
        {
            if (ownsActiveLobby)
            {
                CloseHostedLobby();
            }
            else
            {
                LeaveActiveLobby();
            }

            gameLobbyJoinRequestedCallback?.Dispose();
        }

        private void Update()
        {
            if (pendingOperation != PendingOperation.None && Time.unscaledTime >= operationDeadline)
            {
                Fail("Steam Ritual discovery timed out.");
            }
        }

        public bool BeginCreateLobby(Func<string> generateSeal)
        {
            if (!CanUseSteam() || generateSeal == null || pendingOperation != PendingOperation.None || HasActiveLobby)
            {
                return false;
            }

            sealGenerator = generateSeal;
            sealAttemptCount = 0;
            return SearchNextSealCandidate();
        }

        public bool BeginJoinLobby(string normalizedSeal)
        {
            if (!CanUseSteam() || string.IsNullOrEmpty(normalizedSeal) ||
                pendingOperation != PendingOperation.None || HasActiveLobby)
            {
                return false;
            }

            requestedJoinSeal = normalizedSeal;
            joiningInvitedLobby = false;
            pendingOperation = PendingOperation.JoinSearch;
            operationDeadline = Time.unscaledTime + operationTimeout;
            ApplyLobbyFilters(normalizedSeal);
            lobbyMatchListResult.Set(SteamMatchmaking.RequestLobbyList());
            return true;
        }

        public bool BeginJoinInvitedLobby(ulong lobbyId)
        {
            if (!CanUseSteam() || lobbyId == 0 || pendingOperation != PendingOperation.None || HasActiveLobby)
            {
                return false;
            }

            requestedJoinSeal = string.Empty;
            joiningInvitedLobby = true;
            return BeginLobbyEntry(new CSteamID(lobbyId));
        }

        public bool OpenHostedLobbyInviteOverlay(out string failureReason)
        {
            if (!CanUseSteam())
            {
                failureReason = "Steam is unavailable.";
                return false;
            }

            if (!ownsActiveLobby || activeLobbyId == CSteamID.Nil)
            {
                failureReason = "No hosted Steam Ritual is available.";
                return false;
            }

            SteamFriends.ActivateGameOverlayInviteDialog(activeLobbyId);
            failureReason = string.Empty;
            return true;
        }

        public void CancelPendingOperation()
        {
            pendingOperation = PendingOperation.None;
            sealGenerator = null;
            candidateSeal = string.Empty;
            requestedJoinSeal = string.Empty;
            joiningInvitedLobby = false;
        }

        public void LeaveActiveLobby()
        {
            CancelPendingOperation();
            if (!CanUseSteam() || activeLobbyId == CSteamID.Nil)
            {
                activeLobbyId = CSteamID.Nil;
                ownsActiveLobby = false;
                return;
            }

            SteamMatchmaking.LeaveLobby(activeLobbyId);
            activeLobbyId = CSteamID.Nil;
            ownsActiveLobby = false;
        }

        public void CloseHostedLobby()
        {
            CancelPendingOperation();
            if (!CanUseSteam() || activeLobbyId == CSteamID.Nil)
            {
                activeLobbyId = CSteamID.Nil;
                ownsActiveLobby = false;
                return;
            }

            SteamMatchmaking.SetLobbyJoinable(activeLobbyId, false);
            SteamMatchmaking.DeleteLobbyData(activeLobbyId, SealMetadataKey);
            SteamMatchmaking.LeaveLobby(activeLobbyId);
            activeLobbyId = CSteamID.Nil;
            ownsActiveLobby = false;
        }

        private bool SearchNextSealCandidate()
        {
            if (sealAttemptCount >= MaximumSealAttempts)
            {
                Fail("A unique Ritual Seal could not be reserved.");
                return false;
            }

            sealAttemptCount++;
            candidateSeal = sealGenerator();
            pendingOperation = PendingOperation.CreateCollisionSearch;
            operationDeadline = Time.unscaledTime + operationTimeout;
            ApplyLobbyFilters(candidateSeal);
            lobbyMatchListResult.Set(SteamMatchmaking.RequestLobbyList());
            return true;
        }

        private static void ApplyLobbyFilters(string seal)
        {
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                GameMetadataKey, GameMetadataValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                ProtocolMetadataKey, ProtocolMetadataValue, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(
                SealMetadataKey, seal, ELobbyComparison.k_ELobbyComparisonEqual);
        }

        private void HandleLobbyMatchList(LobbyMatchList_t result, bool ioFailure)
        {
            if (pendingOperation == PendingOperation.CreateCollisionSearch)
            {
                if (ioFailure)
                {
                    Fail("Steam could not check Ritual Seal availability.");
                    return;
                }

                if (result.m_nLobbiesMatching > 0)
                {
                    SearchNextSealCandidate();
                    return;
                }

                pendingOperation = PendingOperation.CreateLobby;
                operationDeadline = Time.unscaledTime + operationTimeout;
                lobbyCreatedResult.Set(
                    SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, LobbyCapacity));
                return;
            }

            if (pendingOperation != PendingOperation.JoinSearch)
            {
                return;
            }

            if (ioFailure)
            {
                Fail("Steam could not search for that Ritual.");
                return;
            }

            if (result.m_nLobbiesMatching == 0)
            {
                Fail("Ritual not found");
                return;
            }

            if (result.m_nLobbiesMatching != 1)
            {
                Fail("Multiple Rituals use that Seal");
                return;
            }

            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(0);
            if (lobbyId == CSteamID.Nil || !HasExpectedMetadata(lobbyId, requestedJoinSeal))
            {
                Fail("Ritual not found");
                return;
            }

            int memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
            if (memberLimit > 0 && SteamMatchmaking.GetNumLobbyMembers(lobbyId) >= memberLimit)
            {
                Fail("Ritual is full");
                return;
            }

            BeginLobbyEntry(lobbyId);
        }

        private void HandleLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (pendingOperation != PendingOperation.CreateLobby)
            {
                if (!ioFailure && result.m_eResult == EResult.k_EResultOK)
                {
                    SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
                }

                return;
            }

            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Fail("Steam could not create the Ritual lobby.");
                return;
            }

            CSteamID lobbyId = new CSteamID(result.m_ulSteamIDLobby);
            bool metadataPublished =
                SteamMatchmaking.SetLobbyData(lobbyId, GameMetadataKey, GameMetadataValue) &&
                SteamMatchmaking.SetLobbyData(lobbyId, ProtocolMetadataKey, ProtocolMetadataValue) &&
                SteamMatchmaking.SetLobbyData(lobbyId, SealMetadataKey, candidateSeal);

            if (!metadataPublished)
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                Fail("Steam could not publish the Ritual Seal.");
                return;
            }

            SteamMatchmaking.SetLobbyJoinable(lobbyId, true);
            activeLobbyId = lobbyId;
            ownsActiveLobby = true;
            string hostedSeal = candidateSeal;
            ClearPendingState();
            HostedLobbyReady?.Invoke(hostedSeal);
        }

        private void HandleLobbyEntered(LobbyEnter_t result, bool ioFailure)
        {
            if (pendingOperation != PendingOperation.JoinLobby)
            {
                if (!ioFailure && result.m_EChatRoomEnterResponse ==
                    (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    SteamMatchmaking.LeaveLobby(new CSteamID(result.m_ulSteamIDLobby));
                }

                return;
            }

            if (ioFailure || result.m_EChatRoomEnterResponse !=
                (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Fail(MapLobbyEnterFailure(result.m_EChatRoomEnterResponse));
                return;
            }

            CSteamID lobbyId = new CSteamID(result.m_ulSteamIDLobby);
            string resolvedSeal = ResolveAndValidateSeal(lobbyId);
            if (string.IsNullOrEmpty(resolvedSeal) ||
                (!joiningInvitedLobby && resolvedSeal != requestedJoinSeal))
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                Fail("Ritual invitation is invalid");
                return;
            }

            if (joiningInvitedLobby &&
                (SteamMatchmaking.GetLobbyMemberLimit(lobbyId) != LobbyCapacity ||
                 SteamMatchmaking.GetNumLobbyMembers(lobbyId) < 1 ||
                 SteamMatchmaking.GetNumLobbyMembers(lobbyId) > LobbyCapacity))
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                Fail("Ritual Lobby is unavailable");
                return;
            }

            CSteamID ownerId = SteamMatchmaking.GetLobbyOwner(lobbyId);
            if (ownerId == CSteamID.Nil || ownerId.m_SteamID == 0)
            {
                SteamMatchmaking.LeaveLobby(lobbyId);
                Fail("Ritual Host is unavailable");
                return;
            }

            activeLobbyId = lobbyId;
            ownsActiveLobby = false;
            ulong hostSteamId64 = ownerId.m_SteamID;
            ClearPendingState();
            JoinedLobbyReady?.Invoke(resolvedSeal, hostSteamId64);
        }

        private bool BeginLobbyEntry(CSteamID lobbyId)
        {
            if (lobbyId == CSteamID.Nil)
            {
                return false;
            }

            pendingOperation = PendingOperation.JoinLobby;
            operationDeadline = Time.unscaledTime + operationTimeout;
            lobbyEnterResult.Set(SteamMatchmaking.JoinLobby(lobbyId));
            return true;
        }

        private static string ResolveAndValidateSeal(CSteamID lobbyId)
        {
            if (SteamMatchmaking.GetLobbyData(lobbyId, GameMetadataKey) != GameMetadataValue ||
                SteamMatchmaking.GetLobbyData(lobbyId, ProtocolMetadataKey) != ProtocolMetadataValue)
            {
                return string.Empty;
            }

            string normalizedSeal = RitualSealService.NormalizeSeal(
                SteamMatchmaking.GetLobbyData(lobbyId, SealMetadataKey));
            return normalizedSeal.Length == 4 ? normalizedSeal : string.Empty;
        }

        private void HandleGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            if (request.m_steamIDLobby == CSteamID.Nil)
            {
                return;
            }

            InvitedLobbyJoinRequested?.Invoke(request.m_steamIDLobby.m_SteamID);
        }

        private static bool HasExpectedMetadata(CSteamID lobbyId, string seal)
        {
            return SteamMatchmaking.GetLobbyData(lobbyId, GameMetadataKey) == GameMetadataValue &&
                   SteamMatchmaking.GetLobbyData(lobbyId, ProtocolMetadataKey) == ProtocolMetadataValue &&
                   SteamMatchmaking.GetLobbyData(lobbyId, SealMetadataKey) == seal;
        }

        private static string MapLobbyEnterFailure(uint response)
        {
            return response == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseFull
                ? "Ritual is full"
                : "Ritual no longer exists";
        }

        private static bool CanUseSteam()
        {
            return SteamPlatformBootstrap.Instance != null &&
                   SteamPlatformBootstrap.Instance.IsInitialized;
        }

        private void Fail(string reason)
        {
            ClearPendingState();
            OperationFailed?.Invoke(reason);
        }

        private void ClearPendingState()
        {
            pendingOperation = PendingOperation.None;
            sealGenerator = null;
            candidateSeal = string.Empty;
            requestedJoinSeal = string.Empty;
            joiningInvitedLobby = false;
        }
    }
}
