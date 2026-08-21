using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking.Spells
{
    /// <summary>
    /// Owns one player's private server-authoritative spell hand. It observes the existing
    /// ritual lifecycle and drives only the owning player's existing physical hand presentation.
    /// Spell validation, targeting, effects, and consumption requests are intentionally absent.
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

        private readonly SyncList<SpellCardInstance> hand = new(OwnerOnlySettings);
        private readonly SyncVar<uint> ritualSequence = new(0, OwnerOnlySettings);
        private readonly SyncVar<uint> turnSequence = new(0, OwnerOnlySettings);
        private readonly SyncVar<bool> spellUsedThisTurn = new(false, OwnerOnlySettings);
        private readonly SyncVar<uint> handRevision = new(0, OwnerOnlySettings);

        private readonly Dictionary<string, SpellDefinition> definitionsById =
            new(StringComparer.Ordinal);
        private readonly List<SpellDefinition> grantableDefinitions = new();

        private NetworkPlayer networkPlayer;
        private NetworkCharacterPresentation characterPresentation;
        private NetworkRitualAuthority ritualAuthority;
        private SpellHandController spellHandPresentation;
        private uint nextInstanceId = 1;
        private bool isAliveParticipant;
        private bool isBookLocked;
        private bool isInteractionAvailable;

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
        public IReadOnlyList<SpellCardInstance> Hand => hand;

        public static int CalculateTurnGrantCount(int currentHandCount)
        {
            return currentHandCount >= 0 && currentHandCount < MaximumHandSize ? 1 : 0;
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
            if (definition == null || string.IsNullOrEmpty(definition.DefinitionId))
                return false;

            uint instanceId = AllocateInstanceId();
            if (instanceId == 0)
                return false;

            hand.Add(new SpellCardInstance(instanceId, definition.DefinitionId));
            return true;
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
            RitualBookArrivalSnapshot bookArrival = snapshot.BookArrival;
            isBookLocked = ritualIsActive &&
                bookArrival.HasArrived &&
                bookArrival.RitualSequenceId.Value == snapshot.SequenceId.Value &&
                bookArrival.TurnSequenceId.Value == snapshot.Turn.SequenceId.Value &&
                bookArrival.TargetSeatId == snapshot.Turn.ActiveSeatId &&
                string.Equals(
                    bookArrival.PlayerId,
                    networkPlayer.PlayerId,
                    StringComparison.Ordinal);
            bool nextInteractionAvailable = IsOwner && isAliveParticipant &&
                !isBookLocked && hand.Count > 0;

            if (isInteractionAvailable != nextInteractionAvailable)
            {
                isInteractionAvailable = nextInteractionAvailable;
                spellHandPresentation?.SetInteractionAllowed(isInteractionAvailable);
            }

            if (IsOwner && (!isInteractionAvailable || isBookLocked))
            {
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
                    spellHandPresentation.CloseHand();
                    ReleaseSpellHandInputContext();
                }
                else
                {
                    LocalInputContextGate.SetContext(LocalInputContext.SpellHand);
                    spellHandPresentation.OpenHand();
                }
            }

            if (!isInteractionAvailable || !spellHandPresentation.IsOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1))
                spellHandPresentation.SelectCard(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                spellHandPresentation.SelectCard(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                spellHandPresentation.SelectCard(2);
        }

        private void HandleCharacterInstanceChanged(GameObject characterInstance)
        {
            BindCharacterPresentation(characterInstance);
            RefreshOwnerPresentation();
        }

        private void BindCharacterPresentation(GameObject characterInstance)
        {
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
            if (!IsOwner || spellHandPresentation == null)
                return;

            SpellDefinition[] slots = new SpellDefinition[MaximumHandSize];
            int count = Mathf.Min(hand.Count, MaximumHandSize);
            for (int index = 0; index < count; index++)
            {
                SpellCardInstance instance = hand[index];
                definitionsById.TryGetValue(instance.DefinitionId, out slots[index]);
            }

            spellHandPresentation.ApplyAuthoritativeHand(slots);
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
            if (spellHandPresentation != null && spellHandPresentation.IsOpen)
                spellHandPresentation.CloseHand();

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
            BuildDefinitionRegistry();
        }
    }
}
