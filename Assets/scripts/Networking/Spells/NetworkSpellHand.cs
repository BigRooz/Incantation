using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking.Spells
{
    /// <summary>
    /// Owns one player's private server-authoritative spell hand. It observes the existing
    /// ritual lifecycle and drives only the owning player's existing physical hand presentation.
    /// It validates complete spell requests and consumes cards, but owns no targeting or effects.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayer))]
    [RequireComponent(typeof(NetworkCharacterPresentation))]
    public sealed class NetworkSpellHand : NetworkBehaviour
    {
        public const int MaximumHandSize = 3;

        private static readonly SyncTypeSettings OwnerOnlySettings =
            new(ReadPermission.OwnerOnly);

        [Header("Authoritative Definition Pool")]
        [Tooltip("Unique SpellDefinition identities available to the server. Duplicate IDs are ignored.")]
        [SerializeField] private SpellDefinition[] definitionPool = Array.Empty<SpellDefinition>();

        [Header("Local Gaze Selection")]
        [SerializeField, Min(0.1f)] private float gazeMaximumDistance = 5f;
        [SerializeField, Min(0f)] private float gazeDwellSeconds = 0.15f;
        [SerializeField, Min(0f)] private float gazeDeselectGraceSeconds = 0.1f;

        private readonly SyncList<SpellCardInstance> hand = new(OwnerOnlySettings);
        private readonly SyncVar<uint> ritualSequence = new(0, OwnerOnlySettings);
        private readonly SyncVar<uint> turnSequence = new(0, OwnerOnlySettings);
        private readonly SyncVar<bool> spellUsedThisTurn = new(false, OwnerOnlySettings);
        private readonly SyncVar<uint> handRevision = new(0, OwnerOnlySettings);

        private readonly Dictionary<string, SpellDefinition> definitionsById =
            new(StringComparer.Ordinal);
        private readonly List<SpellDefinition> grantableDefinitions = new();
        private readonly SpellCardInstance[] presentedInstances =
            new SpellCardInstance[MaximumHandSize];

        private NetworkPlayer networkPlayer;
        private NetworkCharacterPresentation characterPresentation;
        private NetworkRitualAuthority ritualAuthority;
        private SpellHandController spellHandPresentation;
        private uint nextInstanceId = 1;
        private bool isAliveParticipant;
        private bool isBookLocked;
        private bool isInteractionAvailable;
        private uint lastProcessedCastRequestSequence;
        private bool consumptionPresentationPending;
        private Coroutine consumptionPresentationRoutine;
        private Camera localGameplayCamera;
        private int gazeCandidateIndex = -1;
        private float gazeCandidateStartedAt;
        private float gazeLostStartedAt = -1f;

        public static NetworkSpellHand Local { get; private set; }
        public static bool IsLocalSpellToggleReserved =>
            Local != null && Local.ReservesLocalToggleInput;

        public int HandCount => hand.Count;
        public uint RitualSequence => ritualSequence.Value;
        public uint TurnSequence => turnSequence.Value;
        public bool SpellUsedThisTurn => spellUsedThisTurn.Value;
        public bool CanSuccessfullyUseSpellThisTurn => !spellUsedThisTurn.Value;
        public bool IsBookLocked => isBookLocked;
        public bool IsInteractionAvailable => isInteractionAvailable;
        public string PlayerId => networkPlayer != null ? networkPlayer.PlayerId : string.Empty;
        public IReadOnlyList<SpellCardInstance> Hand => hand;
        public event Action<uint, uint, SpellCastResult> LocalSpellCastResolved;
        public event Action<SpellCardInstance> LocalSpellCardSelected;

        public static int CalculateTurnGrantCount(int currentHandCount)
        {
            return currentHandCount >= 0 && currentHandCount < MaximumHandSize ? 1 : 0;
        }

        public bool TryGetSelectedCastCard(
            out SpellCardInstance selectedCard,
            out SpellDefinition selectedDefinition,
            out RitualSnapshot snapshot)
        {
            snapshot = ritualAuthority != null ? ritualAuthority.Snapshot : default;
            selectedCard = default;
            selectedDefinition = null;
            if (!IsOwner || !isInteractionAvailable || isBookLocked ||
                spellUsedThisTurn.Value || spellHandPresentation == null ||
                !spellHandPresentation.IsOpen)
            {
                return false;
            }

            int selectedIndex = spellHandPresentation.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= hand.Count)
                return false;

            selectedCard = hand[selectedIndex];
            return definitionsById.TryGetValue(
                selectedCard.DefinitionId,
                out selectedDefinition) &&
                selectedDefinition != null &&
                !string.IsNullOrWhiteSpace(selectedDefinition.SpokenIncantation);
        }

        public bool CanSubmitSelectedCast(
            SpellCardInstance selectedCard,
            uint expectedRitualSequence,
            uint expectedTurnSequence)
        {
            RitualSnapshot snapshot = ritualAuthority != null
                ? ritualAuthority.Snapshot
                : default;
            return IsOwner && isInteractionAvailable && !isBookLocked &&
                !spellUsedThisTurn.Value && ContainsCard(selectedCard) &&
                snapshot.SequenceId.Value == expectedRitualSequence &&
                snapshot.Turn.SequenceId.Value == expectedTurnSequence;
        }

        private bool ContainsCard(SpellCardInstance card)
        {
            for (int index = 0; index < hand.Count; index++)
            {
                if (hand[index].Equals(card))
                    return true;
            }

            return false;
        }

        public bool RequestSpellCast(SpellCastRequest request)
        {
            if (!IsOwner || request.RequestSequence == 0)
                return false;

            if (IsServerInitialized)
            {
                ProcessSpellCastRequest(Owner, request);
                return true;
            }

            RequestSpellCastServerRpc(request);
            return true;
        }

        private bool ReservesLocalToggleInput
        {
            get
            {
                NetworkRitualAuthority authority = ritualAuthority ??
                    NetworkRitualAuthority.Instance;
                if (!IsOwner || hand.Count == 0 || authority == null)
                    return false;

                RitualSnapshot snapshot = authority.Snapshot;
                return snapshot.Phase != RitualPhase.Inactive &&
                    snapshot.Phase != RitualPhase.Completed &&
                    TryFindParticipant(
                        snapshot.Roster,
                        networkPlayer != null ? networkPlayer.PlayerId : string.Empty,
                        out RitualRosterEntrySnapshot participant) &&
                    participant.IsActive && participant.IsAlive;
            }
        }

        private void Awake()
        {
            networkPlayer = GetComponent<NetworkPlayer>();
            characterPresentation = GetComponent<NetworkCharacterPresentation>();
            BuildDefinitionRegistry();
        }

        private void OnEnable()
        {
            if (characterPresentation != null)
                characterPresentation.CharacterInstanceChanged += HandleCharacterInstanceChanged;
        }

        private void OnDisable()
        {
            if (characterPresentation != null)
                characterPresentation.CharacterInstanceChanged -= HandleCharacterInstanceChanged;

            ClosePresentationAndReleaseInput();
        }

        private void Update()
        {
            ResolveRitualAuthority();

            if (IsServerInitialized)
                ProcessAuthoritativeLifecycle();

            RefreshEligibilityAndPresentation();
            ProcessLocalInput();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            hand.OnChange += HandleHandChanged;
            ritualSequence.OnChange += HandleRitualSequenceChanged;
            turnSequence.OnChange += HandleTurnSequenceChanged;
            spellUsedThisTurn.OnChange += HandleSpellUsedChanged;
            handRevision.OnChange += HandleHandRevisionChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
                Local = this;

            BindCharacterPresentation(
                characterPresentation != null ? characterPresentation.CharacterInstance : null);
            RefreshOwnerPresentation();
        }

        public override void OnStopNetwork()
        {
            hand.OnChange -= HandleHandChanged;
            ritualSequence.OnChange -= HandleRitualSequenceChanged;
            turnSequence.OnChange -= HandleTurnSequenceChanged;
            spellUsedThisTurn.OnChange -= HandleSpellUsedChanged;
            handRevision.OnChange -= HandleHandRevisionChanged;

            if (Local == this)
                Local = null;

            ClosePresentationAndReleaseInput();
            base.OnStopNetwork();
        }

        private void ProcessAuthoritativeLifecycle()
        {
            if (ritualAuthority == null)
                return;

            RitualSnapshot snapshot = ritualAuthority.Snapshot;
            if (snapshot.Phase == RitualPhase.Inactive)
            {
                ResetAuthoritativeHand();
                return;
            }

            if (snapshot.SequenceId.Value == 0 || string.IsNullOrEmpty(networkPlayer.PlayerId))
                return;

            bool foundParticipant = TryFindParticipant(
                snapshot.Roster,
                networkPlayer.PlayerId,
                out RitualRosterEntrySnapshot participant);
            if (!foundParticipant)
                return;

            if (ritualSequence.Value != snapshot.SequenceId.Value)
            {
                BeginAuthoritativeMatch(snapshot.SequenceId.Value, participant.IsAlive);
            }

            if (!participant.IsAlive || !participant.IsActive)
                return;

            if (!string.Equals(
                    snapshot.ActivePlayerId,
                    networkPlayer.PlayerId,
                    StringComparison.Ordinal) ||
                snapshot.Turn.SequenceId.Value == 0 ||
                turnSequence.Value == snapshot.Turn.SequenceId.Value)
            {
                return;
            }

            BeginAuthoritativePlayerTurn(snapshot.Turn.SequenceId.Value);
        }

        private void BeginAuthoritativeMatch(uint matchRitualSequence, bool participantIsAlive)
        {
            hand.Clear();
            ritualSequence.Value = matchRitualSequence;
            turnSequence.Value = 0;
            spellUsedThisTurn.Value = false;

            if (participantIsAlive)
            {
                foreach (SpellDefinition definition in grantableDefinitions)
                {
                    if (hand.Count >= MaximumHandSize)
                        break;

                    TryGrantCard(definition);
                }

                while (hand.Count < MaximumHandSize && TryGrantOneCard())
                {
                }
            }

            IncrementRevision();
        }

        private void BeginAuthoritativePlayerTurn(uint authoritativeTurnSequence)
        {
            turnSequence.Value = authoritativeTurnSequence;
            spellUsedThisTurn.Value = false;

            if (CalculateTurnGrantCount(hand.Count) == 1)
                TryGrantOneCard();

            IncrementRevision();
        }

        private bool TryGrantOneCard()
        {
            if (hand.Count >= MaximumHandSize || grantableDefinitions.Count == 0)
                return false;

            SpellDefinition definition = grantableDefinitions[
                UnityEngine.Random.Range(0, grantableDefinitions.Count)];
            return TryGrantCard(definition);
        }

        private bool TryGrantCard(SpellDefinition definition)
        {
            if (hand.Count >= MaximumHandSize)
                return false;

            if (definition == null || string.IsNullOrEmpty(definition.DefinitionId))
                return false;

            uint instanceId = AllocateInstanceId();
            if (instanceId == 0)
                return false;

            hand.Add(new SpellCardInstance(instanceId, definition.DefinitionId));
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        private void RequestSpellCastServerRpc(
            SpellCastRequest request,
            NetworkConnection sender = null)
        {
            ProcessSpellCastRequest(sender, request);
        }

        private void ProcessSpellCastRequest(
            NetworkConnection sender,
            SpellCastRequest request)
        {
            int cardIndex = -1;
            bool accepted = TryValidateSpellCastRequest(
                sender,
                request,
                out cardIndex,
                out bool phraseMatched,
                out string rejectReason);
            SpellCastResult result = accepted
                ? SpellCastResult.Accepted
                : SpellCastResult.Rejected;

            if (Owner != null)
            {
                TargetSpellCastResolved(
                    Owner,
                    request.RequestSequence,
                    request.CardInstanceId,
                    result);
            }

            if (accepted)
            {
                spellUsedThisTurn.Value = true;
                hand.RemoveAt(cardIndex);
                IncrementRevision();
            }

        }

        private bool TryValidateSpellCastRequest(
            NetworkConnection sender,
            SpellCastRequest request,
            out int cardIndex,
            out bool phraseMatched,
            out string rejectReason)
        {
            cardIndex = -1;
            phraseMatched = false;
            rejectReason = string.Empty;

            if (!IsServerInitialized)
            {
                rejectReason = "ServerNotInitialized";
                return false;
            }

            if (sender == null)
            {
                rejectReason = "MissingSender";
                return false;
            }

            if (Owner == null)
            {
                rejectReason = "MissingOwner";
                return false;
            }

            if (sender.ClientId != Owner.ClientId)
            {
                rejectReason = "SenderDoesNotOwnHand";
                return false;
            }

            if (request.RequestSequence == 0)
            {
                rejectReason = "InvalidRequestSequence";
                return false;
            }

            if (request.RequestSequence <= lastProcessedCastRequestSequence)
            {
                rejectReason = "DuplicateOrOutOfOrderRequest";
                return false;
            }

            lastProcessedCastRequestSequence = request.RequestSequence;
            if (ritualAuthority == null)
                ResolveRitualAuthority();
            if (ritualAuthority == null)
            {
                rejectReason = "MissingRitualAuthority";
                return false;
            }

            RitualSnapshot snapshot = ritualAuthority.Snapshot;
            if (snapshot.SequenceId.Value == 0)
            {
                rejectReason = "RitualInactive";
                return false;
            }

            if (snapshot.SequenceId.Value != request.RitualSequence)
            {
                rejectReason = "RitualSequenceMismatch";
                return false;
            }

            if (snapshot.Turn.SequenceId.Value == 0)
            {
                rejectReason = "TurnInactive";
                return false;
            }

            if (snapshot.Turn.SequenceId.Value != request.TurnSequence)
            {
                rejectReason = "TurnSequenceMismatch";
                return false;
            }

            if (snapshot.Phase == RitualPhase.Inactive || snapshot.Phase == RitualPhase.Completed)
            {
                rejectReason = "RitualPhaseNotEligible";
                return false;
            }

            if (spellUsedThisTurn.Value)
            {
                rejectReason = "SpellAlreadyUsedThisTurn";
                return false;
            }

            if (!TryFindParticipant(
                    snapshot.Roster,
                    PlayerId,
                    out RitualRosterEntrySnapshot participant))
            {
                rejectReason = "ParticipantNotFound";
                return false;
            }

            if (!participant.IsActive)
            {
                rejectReason = "ParticipantInactive";
                return false;
            }

            if (!participant.IsAlive)
            {
                rejectReason = "ParticipantDead";
                return false;
            }

            if (IsBookArrivalForPlayer(snapshot, PlayerId))
            {
                rejectReason = "BookArrived";
                return false;
            }

            for (int index = 0; index < hand.Count; index++)
            {
                SpellCardInstance candidate = hand[index];
                if (candidate.InstanceId == request.CardInstanceId &&
                    string.Equals(
                        candidate.DefinitionId,
                        request.DefinitionId,
                        StringComparison.Ordinal))
                {
                    cardIndex = index;
                    break;
                }
            }

            if (cardIndex < 0)
            {
                rejectReason = "CardNotInAuthoritativeHand";
                return false;
            }

            if (!definitionsById.TryGetValue(
                    request.DefinitionId,
                    out SpellDefinition definition) || definition == null)
            {
                rejectReason = "DefinitionNotFound";
                return false;
            }

            string normalizedPhrase = SpellDefinition.NormalizeSpokenPhrase(
                request.NormalizedPhrase);
            if (normalizedPhrase.Length == 0)
            {
                rejectReason = "PhraseEmpty";
                return false;
            }

            if (normalizedPhrase.Length > 256)
            {
                rejectReason = "PhraseTooLong";
                return false;
            }

            phraseMatched = definition.AcceptsNormalizedSpokenPhrase(normalizedPhrase);
            if (!phraseMatched)
            {
                rejectReason = "PhraseMismatch";
                return false;
            }

            return true;
        }

        [TargetRpc]
        private void TargetSpellCastResolved(
            NetworkConnection connection,
            uint requestSequence,
            uint cardInstanceId,
            SpellCastResult result)
        {
            if (!IsOwner)
                return;

            if (result == SpellCastResult.Accepted)
                PlayAuthoritativeConsumption(cardInstanceId);

            LocalSpellCastResolved?.Invoke(
                requestSequence,
                cardInstanceId,
                result);
        }

        private uint AllocateInstanceId()
        {
            if (nextInstanceId == 0)
                return 0;

            uint allocated = nextInstanceId;
            nextInstanceId = nextInstanceId == uint.MaxValue ? 0 : nextInstanceId + 1;
            return allocated;
        }

        private void ResetAuthoritativeHand()
        {
            if (ritualSequence.Value == 0 && turnSequence.Value == 0 &&
                hand.Count == 0 && !spellUsedThisTurn.Value)
            {
                return;
            }

            hand.Clear();
            ritualSequence.Value = 0;
            turnSequence.Value = 0;
            spellUsedThisTurn.Value = false;
            IncrementRevision();
        }

        private void IncrementRevision()
        {
            handRevision.Value = handRevision.Value == uint.MaxValue
                ? 1
                : handRevision.Value + 1;
        }

        private void RefreshEligibilityAndPresentation()
        {
            RitualSnapshot snapshot = ritualAuthority != null
                ? ritualAuthority.Snapshot
                : default;
            bool ritualIsActive = ritualAuthority != null &&
                snapshot.SequenceId.Value > 0 &&
                snapshot.Phase != RitualPhase.Inactive &&
                snapshot.Phase != RitualPhase.Completed;
            isAliveParticipant = ritualIsActive &&
                TryFindParticipant(
                    snapshot.Roster,
                    networkPlayer.PlayerId,
                    out RitualRosterEntrySnapshot participant) &&
                participant.IsActive &&
                participant.IsAlive;
            isBookLocked = ritualIsActive &&
                IsBookArrivalForPlayer(snapshot, networkPlayer.PlayerId);
            bool nextInteractionAvailable = IsOwner && isAliveParticipant &&
                !isBookLocked && hand.Count > 0;

            if (isInteractionAvailable != nextInteractionAvailable)
            {
                isInteractionAvailable = nextInteractionAvailable;
                spellHandPresentation?.SetInteractionAllowed(isInteractionAvailable);
            }

            if (IsOwner && (!isInteractionAvailable || isBookLocked))
            {
                ClearLocalGazeSelection(true);
                if (spellHandPresentation != null && spellHandPresentation.IsOpen)
                    spellHandPresentation.CloseHand();

                ReleaseSpellHandInputContext();
            }
        }

        private void ProcessLocalInput()
        {
            if (!IsOwner || !ReservesLocalToggleInput || spellHandPresentation == null)
                return;

            bool ownsInputContext =
                LocalInputContextGate.Current == LocalInputContext.Gameplay ||
                LocalInputContextGate.Current == LocalInputContext.SpellHand;
            if (!ownsInputContext)
                return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!isInteractionAvailable)
                    return;

                if (spellHandPresentation.IsOpen)
                {
                    ClearLocalGazeSelection(true);
                    spellHandPresentation.CloseHand();
                    ReleaseSpellHandInputContext();
                }
                else
                {
                    LocalInputContextGate.SetContext(LocalInputContext.SpellHand);
                    spellHandPresentation.OpenHand();
                    ResetGazeCandidate();
                }
            }

            if (!isInteractionAvailable || !spellHandPresentation.IsOpen)
                return;

            ProcessLocalGazeSelection();
        }

        private void SelectLocalCard(int index)
        {
            if (index < 0 || index >= hand.Count ||
                spellHandPresentation.SelectedIndex == index)
                return;

            spellHandPresentation.SelectCard(index);
            LocalSpellCardSelected?.Invoke(hand[index]);
        }

        private void ProcessLocalGazeSelection()
        {
            if (spellUsedThisTurn.Value)
            {
                ClearLocalGazeSelection(true);
                return;
            }

            Camera gazeCamera = ResolveLocalGameplayCamera();
            if (gazeCamera == null)
            {
                ProcessGazeMiss();
                return;
            }

            Ray gazeRay = gazeCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));
            if (!spellHandPresentation.TryGetGazeCardIndex(
                    gazeRay,
                    gazeMaximumDistance,
                    out int hitIndex) || hitIndex >= hand.Count)
            {
                ProcessGazeMiss();
                return;
            }

            gazeLostStartedAt = -1f;
            if (gazeCandidateIndex != hitIndex)
            {
                gazeCandidateIndex = hitIndex;
                gazeCandidateStartedAt = Time.unscaledTime;
                return;
            }

            if (spellHandPresentation.SelectedIndex != hitIndex &&
                Time.unscaledTime - gazeCandidateStartedAt >= gazeDwellSeconds)
            {
                SelectLocalCard(hitIndex);
            }
        }

        private void ProcessGazeMiss()
        {
            gazeCandidateIndex = -1;
            gazeCandidateStartedAt = 0f;
            if (spellHandPresentation == null || spellHandPresentation.SelectedIndex < 0)
            {
                gazeLostStartedAt = -1f;
                return;
            }

            if (gazeLostStartedAt < 0f)
            {
                gazeLostStartedAt = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - gazeLostStartedAt >= gazeDeselectGraceSeconds)
                ClearLocalGazeSelection(true);
        }

        private void ClearLocalGazeSelection(bool notifySelectionChanged)
        {
            ResetGazeCandidate();
            if (spellHandPresentation == null || spellHandPresentation.SelectedIndex < 0)
                return;

            spellHandPresentation.ClearSelection();
            if (notifySelectionChanged)
                LocalSpellCardSelected?.Invoke(default);
        }

        private void ResetGazeCandidate()
        {
            gazeCandidateIndex = -1;
            gazeCandidateStartedAt = 0f;
            gazeLostStartedAt = -1f;
        }

        private Camera ResolveLocalGameplayCamera()
        {
            GameObject characterInstance = characterPresentation != null
                ? characterPresentation.CharacterInstance
                : null;
            if (localGameplayCamera != null && characterInstance != null &&
                localGameplayCamera.enabled &&
                localGameplayCamera.gameObject.activeInHierarchy &&
                localGameplayCamera.transform.IsChildOf(characterInstance.transform))
            {
                return localGameplayCamera;
            }

            localGameplayCamera = null;
            if (characterInstance == null)
                return null;

            foreach (Camera candidate in characterInstance.GetComponentsInChildren<Camera>(true))
            {
                if (!candidate.enabled || !candidate.gameObject.activeInHierarchy)
                    continue;

                localGameplayCamera = candidate;
                break;
            }

            return localGameplayCamera;
        }

        private void HandleCharacterInstanceChanged(GameObject characterInstance)
        {
            BindCharacterPresentation(characterInstance);
            RefreshOwnerPresentation();
        }

        private void BindCharacterPresentation(GameObject characterInstance)
        {
            localGameplayCamera = null;
            ResetGazeCandidate();
            if (spellHandPresentation != null)
            {
                spellHandPresentation.SetDebugInputEnabled(false);
                if (!IsOwner)
                    spellHandPresentation.HideHand();
            }

            spellHandPresentation = characterInstance != null
                ? characterInstance.GetComponentInChildren<SpellHandController>(true)
                : null;
            if (spellHandPresentation == null)
                return;

            spellHandPresentation.SetDebugInputEnabled(false);
            spellHandPresentation.SetInteractionAllowed(IsOwner && isInteractionAvailable);
            if (!IsOwner)
                spellHandPresentation.ApplyAuthoritativeHand(Array.Empty<SpellDefinition>());
        }

        private void RefreshOwnerPresentation()
        {
            if (!IsOwner || spellHandPresentation == null ||
                consumptionPresentationPending)
                return;

            int count = Mathf.Min(hand.Count, MaximumHandSize);
            bool presentationChanged = false;
            for (int index = 0; index < MaximumHandSize; index++)
            {
                SpellCardInstance current = index < count ? hand[index] : default;
                if (!presentedInstances[index].Equals(current))
                {
                    presentationChanged = true;
                    break;
                }
            }

            if (!presentationChanged)
            {
                spellHandPresentation.SetInteractionAllowed(isInteractionAvailable);
                return;
            }

            SpellDefinition[] slots = new SpellDefinition[MaximumHandSize];
            int[] previousSlotForNewSlot = new int[MaximumHandSize];
            Array.Fill(previousSlotForNewSlot, -1);
            for (int index = 0; index < count; index++)
            {
                SpellCardInstance instance = hand[index];
                definitionsById.TryGetValue(instance.DefinitionId, out slots[index]);
                for (int previousIndex = 0;
                    previousIndex < presentedInstances.Length;
                    previousIndex++)
                {
                    if (presentedInstances[previousIndex].Equals(instance))
                    {
                        previousSlotForNewSlot[index] = previousIndex;
                        break;
                    }
                }
            }

            for (int index = 0; index < count; index++)
                presentedInstances[index] = hand[index];
            for (int index = count; index < MaximumHandSize; index++)
                presentedInstances[index] = default;

            spellHandPresentation.ApplyAuthoritativeHand(
                slots,
                previousSlotForNewSlot);
            spellHandPresentation.SetInteractionAllowed(isInteractionAvailable);
        }

        private void BuildDefinitionRegistry()
        {
            definitionsById.Clear();
            grantableDefinitions.Clear();
            if (definitionPool == null)
                return;

            foreach (SpellDefinition definition in definitionPool)
            {
                if (definition == null || string.IsNullOrEmpty(definition.DefinitionId) ||
                    definitionsById.ContainsKey(definition.DefinitionId))
                {
                    continue;
                }

                definitionsById.Add(definition.DefinitionId, definition);
                grantableDefinitions.Add(definition);
            }
        }

        private void ResolveRitualAuthority()
        {
            if (ritualAuthority == null)
                ritualAuthority = NetworkRitualAuthority.Instance;
        }

        private static bool TryFindParticipant(
            RitualRosterSnapshot roster,
            string playerId,
            out RitualRosterEntrySnapshot participant)
        {
            if (!string.IsNullOrEmpty(playerId))
            {
                foreach (RitualRosterEntrySnapshot entry in roster.Entries)
                {
                    if (string.Equals(entry.PlayerId, playerId, StringComparison.Ordinal))
                    {
                        participant = entry;
                        return true;
                    }
                }
            }

            participant = default;
            return false;
        }

        private static bool IsBookArrivalForPlayer(
            RitualSnapshot snapshot,
            string playerId)
        {
            RitualBookArrivalSnapshot arrival = snapshot.BookArrival;
            return arrival.HasArrived &&
                arrival.RitualSequenceId.Value == snapshot.SequenceId.Value &&
                arrival.TurnSequenceId.Value == snapshot.Turn.SequenceId.Value &&
                arrival.TargetSeatId == snapshot.Turn.ActiveSeatId &&
                string.Equals(arrival.PlayerId, playerId, StringComparison.Ordinal);
        }

        private void PlayAuthoritativeConsumption(uint cardInstanceId)
        {
            if (spellHandPresentation == null)
                return;

            int presentationIndex = -1;
            for (int index = 0; index < presentedInstances.Length; index++)
            {
                if (presentedInstances[index].InstanceId == cardInstanceId)
                {
                    presentationIndex = index;
                    break;
                }
            }

            if (presentationIndex < 0)
                return;

            if (consumptionPresentationRoutine != null)
                StopCoroutine(consumptionPresentationRoutine);
            consumptionPresentationPending = true;
            spellHandPresentation.ConsumeCardAt(presentationIndex);
            consumptionPresentationRoutine = StartCoroutine(
                CompleteConsumptionPresentation(
                    spellHandPresentation.ConsumptionDuration));
        }

        private IEnumerator CompleteConsumptionPresentation(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            consumptionPresentationPending = false;
            consumptionPresentationRoutine = null;
            RefreshOwnerPresentation();
        }

        private void HandleHandChanged(
            SyncListOperation operation,
            int index,
            SpellCardInstance previousValue,
            SpellCardInstance currentValue,
            bool asServer)
        {
            if (IsOwner && (asServer || !IsServerInitialized))
                RefreshOwnerPresentation();
        }

        private void HandleRitualSequenceChanged(uint previous, uint current, bool asServer)
        {
            if (IsOwner && (asServer || !IsServerInitialized))
                RefreshOwnerPresentation();
        }

        private void HandleTurnSequenceChanged(uint previous, uint current, bool asServer)
        {
        }

        private void HandleSpellUsedChanged(bool previous, bool current, bool asServer)
        {
        }

        private void HandleHandRevisionChanged(uint previous, uint current, bool asServer)
        {
            if (IsOwner && (asServer || !IsServerInitialized))
                RefreshOwnerPresentation();
        }

        private void ClosePresentationAndReleaseInput()
        {
            if (consumptionPresentationRoutine != null)
            {
                StopCoroutine(consumptionPresentationRoutine);
                consumptionPresentationRoutine = null;
                consumptionPresentationPending = false;
            }

            if (spellHandPresentation != null && spellHandPresentation.IsOpen)
            {
                ClearLocalGazeSelection(true);
                spellHandPresentation.CloseHand();
            }

            ReleaseSpellHandInputContext();
        }

        private void ReleaseSpellHandInputContext()
        {
            if (IsOwner && LocalInputContextGate.Current == LocalInputContext.SpellHand)
                LocalInputContextGate.RestoreGameplay();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            gazeMaximumDistance = Mathf.Max(0.1f, gazeMaximumDistance);
            gazeDwellSeconds = Mathf.Max(0f, gazeDwellSeconds);
            gazeDeselectGraceSeconds = Mathf.Max(0f, gazeDeselectGraceSeconds);
            BuildDefinitionRegistry();
        }
    }
}
