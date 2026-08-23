using System;
using Incantation.Networking.Ritual;
using UnityEngine;

namespace Incantation.Networking.Spells
{
    /// <summary>
    /// Owns the local complete-phrase spell attempt presentation. It borrows the existing
    /// production Whisper recognizer only before authoritative Book arrival and delegates all
    /// card validation and mutation to NetworkSpellHand.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkSpellHand))]
    public sealed class SpellVoiceCastController : MonoBehaviour
    {
        public static SpellVoiceCastController Local { get; private set; }

        private NetworkSpellHand networkSpellHand;
        private WhisperVoiceRecognizer whisperRecognizer;
        private bool attemptActive;
        private bool awaitingAuthority;
        private uint attemptGeneration;
        private uint attemptRequestSequence;
        private SpellCardInstance attemptedCard;
        private uint attemptedRitualSequence;
        private uint attemptedTurnSequence;

        public bool IsListening => attemptActive && whisperRecognizer != null &&
            whisperRecognizer.IsListening;
        public string LastRawTranscript { get; private set; } = string.Empty;
        public string LastBankedTranscript { get; private set; } = string.Empty;
        public SpellCastResult LastResult { get; private set; }

        private void Awake()
        {
            networkSpellHand = GetComponent<NetworkSpellHand>();
        }

        private void OnEnable()
        {
            if (networkSpellHand != null)
            {
                networkSpellHand.LocalSpellCastResolved += HandleAuthoritativeResult;
                networkSpellHand.LocalSpellCardSelected += HandleLocalSpellCardSelected;
            }
        }

        private void OnDisable()
        {
            if (networkSpellHand != null)
            {
                networkSpellHand.LocalSpellCastResolved -= HandleAuthoritativeResult;
                networkSpellHand.LocalSpellCardSelected -= HandleLocalSpellCardSelected;
            }

            CancelAttempt("Spell voice controller disabled.");
            UnsubscribeRecognizer();
            if (Local == this)
                Local = null;
        }

        public static void ReleaseMicrophoneForRitual()
        {
            Local?.CancelAttemptForRitualHandoff();
        }

        private void Update()
        {
            if (networkSpellHand == null || !networkSpellHand.IsOwner)
                return;

            ResolveRecognizer();
            if (attemptActive)
            {
                if (!networkSpellHand.CanSubmitSelectedCast(
                        attemptedCard,
                        attemptedRitualSequence,
                        attemptedTurnSequence))
                {
                    CancelAttempt("Captured card or authoritative eligibility changed.");
                }

                return;
            }

            if (awaitingAuthority)
                return;

            if (!networkSpellHand.TryGetSelectedCastCard(
                    out SpellCardInstance selectedCard,
                    out SpellDefinition selectedDefinition,
                    out RitualSnapshot snapshot))
            {
                CancelAttempt("Spell selection or eligibility changed.");
                return;
            }

            if (whisperRecognizer != null &&
                !whisperRecognizer.IsListening && !whisperRecognizer.IsProcessingRecognition)
            {
                BeginAttempt(selectedCard, selectedDefinition, snapshot);
            }
        }

        private void BeginAttempt(
            SpellCardInstance card,
            SpellDefinition definition,
            RitualSnapshot snapshot)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.SpokenIncantation))
                return;

            attemptGeneration++;
            Local = this;
            attemptActive = true;
            attemptedCard = card;
            attemptedRitualSequence = snapshot.SequenceId.Value;
            attemptedTurnSequence = snapshot.Turn.SequenceId.Value;
            LastRawTranscript = string.Empty;
            LastBankedTranscript = string.Empty;
            LastResult = SpellCastResult.None;
            whisperRecognizer.SetCapturePurpose(WhisperCapturePurpose.Spell);
            whisperRecognizer.ConfigureAuthoritativeSession(
                attemptedRitualSequence,
                attemptedTurnSequence,
                networkSpellHand.PlayerId);
            whisperRecognizer.StartListening();
            if (!whisperRecognizer.IsListening && !whisperRecognizer.IsProcessingRecognition)
                attemptActive = false;
        }

        private void HandlePhraseRecognized(string transcript)
        {
            if (!attemptActive || networkSpellHand == null || !networkSpellHand.IsOwner)
                return;

            uint completedGeneration = attemptGeneration;
            LastRawTranscript = transcript ?? string.Empty;
            LastBankedTranscript = SpellDefinition.NormalizeSpokenPhrase(LastRawTranscript);
            attemptActive = false;

            if (completedGeneration != attemptGeneration ||
                !networkSpellHand.CanSubmitSelectedCast(
                    attemptedCard,
                    attemptedRitualSequence,
                    attemptedTurnSequence))
            {
                LastResult = SpellCastResult.Rejected;
                return;
            }

            if (attemptRequestSequence == uint.MaxValue)
            {
                LastResult = SpellCastResult.Rejected;
                return;
            }

            attemptRequestSequence++;
            awaitingAuthority = true;
            bool submitted = networkSpellHand.RequestSpellCast(new SpellCastRequest(
                attemptRequestSequence,
                attemptedRitualSequence,
                attemptedTurnSequence,
                attemptedCard.InstanceId,
                attemptedCard.DefinitionId,
                LastBankedTranscript));
            if (!submitted)
            {
                awaitingAuthority = false;
                LastResult = SpellCastResult.Rejected;
            }
        }

        private void HandleAuthoritativeResult(
            uint requestSequence,
            uint cardInstanceId,
            SpellCastResult result)
        {
            if (requestSequence != attemptRequestSequence ||
                cardInstanceId != attemptedCard.InstanceId)
            {
                return;
            }

            LastResult = result;
            awaitingAuthority = false;
        }

        private void HandleLocalSpellCardSelected(SpellCardInstance selectedCard)
        {
            if (attemptActive && !attemptedCard.Equals(selectedCard))
                CancelAttempt("Player selected a different spell card.");
        }

        private void CancelAttempt(string reason)
        {
            CancelAttempt(reason, false);
        }

        private void CancelAttemptForRitualHandoff()
        {
            CancelAttempt("Ritual recitation has microphone priority.", true);
        }

        private void CancelAttempt(string reason, bool preserveMicrophoneOwnership)
        {
            if (!attemptActive && !awaitingAuthority)
                return;

            bool wasCapturingOrTranscribing = attemptActive;
            attemptGeneration++;
            attemptActive = false;
            awaitingAuthority = false;
            LastResult = SpellCastResult.Rejected;
            if (wasCapturingOrTranscribing && whisperRecognizer != null)
            {
                if (preserveMicrophoneOwnership)
                    whisperRecognizer.CancelListeningForHandoff(reason);
                else
                    whisperRecognizer.CancelListening(reason);
            }
        }

        private void ResolveRecognizer()
        {
            if (whisperRecognizer != null)
                return;

            whisperRecognizer = FindFirstObjectByType<WhisperVoiceRecognizer>(
                FindObjectsInactive.Include);
            if (whisperRecognizer != null)
                whisperRecognizer.OnSpellPhraseRecognized += HandlePhraseRecognized;
        }

        private void UnsubscribeRecognizer()
        {
            if (whisperRecognizer == null)
                return;

            whisperRecognizer.OnSpellPhraseRecognized -= HandlePhraseRecognized;
            whisperRecognizer = null;
        }
    }
}
