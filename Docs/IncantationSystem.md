# Incantation System

Purpose: define the spoken ritual phrase system and its boundaries.

Questions answered here:

- Who owns ritual word vocabulary and phrase state?
- How do validation modes affect phrase acceptance?
- What should the incantation system not own?

This document does not contain current task planning, full voice implementation details, book movement ownership, or elimination rules.

Read next: `Docs/VoiceMagicSystem.md` for voice-recognition boundaries or `Docs/CoreRitualLoopArchitecture.md` for ritual coordination.

## Purpose

The Incantation System is the source of truth for the spoken ritual phrase.

It owns:

- The ritual word vocabulary.
- The current shared ritual phrase.
- Which words are currently visible.
- Word-by-word acceptance state for the current phrase.
- Validation of recognized candidates against the current phrase.
- Events that other systems can react to.

It does not own UI, microphone capture, networking, timers, book movement, demon behavior, player elimination, cards, notebook logic, or lore delivery.

## Current Ritual Rule

- The phrase starts with 1 word.
- All active players say the same visible phrase.
- The book moves from active seat to active seat.
- After the book completes a full active table rotation, 1 word is added to the shared phrase.
- The active player must satisfy the selected validation mode before the hourglass runs out.

This replaces older notes where every individual turn added a new word. Word growth is rotation-based, not player-turn-based.

## Voice Recognition Direction

The current prototype supports two validation modes:

- `WordByWordRealtime`: default prototype mode. Recognized keywords are validated immediately against the next expected word, allowing realtime visual word absorption and wrong-word rejection feedback.
- `FullPhrase`: optional strict mode. A full transcript is validated against the full visible phrase.

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.

Whisper is kept for full-phrase or experimental recognition paths, but it must not be forced as the only validation path.

Unity Dictation and Azure are not part of the current implementation plan and must not be reintroduced.

## Architecture

`IncantationWord` is a serializable data object containing visible ritual word text and optional speech aliases.

`IncantationWordLibrary` is the vocabulary source for generated ritual phrases and ritual speech aliases.

`IncantationManager` should remain focused on ritual phrase state, word acceptance, and validation. Other systems may subscribe to its events, but they should not duplicate the rules for phrase order, phrase growth, correctness, or completion.

`VoicePhraseNormalizer` may build alias lookup from the ritual word library so adding or changing a ritual word affects recognition normalization in one place.

Offline, `IncantationManager` evaluates and applies candidates through `PhraseValidator`. During
a network ritual, `NetworkRitualAuthority` is the only phrase judge: it reads the server
`IncantationManager` phrase, uses the same pure evaluation methods, and publishes an immutable
verdict. Each peer's `IncantationManager` applies that verdict for progress and presentation
without independently comparing the submitted speech.

## Spell Phrase Boundary

`SpellPhraseLibrary` is separate from ritual words.

Do not merge spell/card phrases into the ritual phrase vocabulary unless explicitly requested. Spell phrases belong to future spell, card, or interference systems. Ritual words belong to the core book-and-hourglass loop.

## Paused Integrations

Notebook, card, lore, and demon reaction integrations are paused until the core ritual loop and voice interaction work reliably.

`WhisperSandbox` is a sandbox/reference area and should not be modified during core ritual work unless explicitly requested.
