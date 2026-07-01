# Incantation System

## Purpose

The Incantation System is the source of truth for the spoken ritual phrase.

It owns:

- The ritual word vocabulary.
- The current shared ritual phrase.
- Which words are currently visible.
- Validation of a recognized phrase against the current phrase.
- Events that other systems can react to.

It does not own UI, microphone capture, networking, timers, book movement, demon behavior, player elimination, cards, notebook logic, or lore delivery.

## Current Ritual Rule

- The phrase starts with 1 word.
- All players say the same visible phrase.
- The book moves from player to player.
- After the book completes a full table rotation, 1 word is added to the shared phrase.
- The active player must say the full visible phrase before the hourglass runs out.

This replaces older notes where every individual turn added a new word. Word growth is rotation-based, not player-turn-based.

## Voice Recognition Direction

Whisper is the primary voice recognition system.

Windows speech recognition is fallback only.

Unity Dictation and Azure are not part of the current implementation plan.

Whisper validation should use full phrase matching because ritual success requires the complete visible phrase. The recognizer may emit partial or chunked candidates, but ritual success should only be accepted when the current full phrase is recognized.

Windows fallback may remain more sequential or keyword-oriented internally, but it must not become the main design path.

## Architecture

`IncantationWord` is a serializable data object containing visible ritual word text and optional speech aliases.

`IncantationWordLibrary` is the vocabulary source for generated ritual phrases and ritual speech aliases.

`IncantationManager` should remain focused on ritual phrase state and validation. Other systems may subscribe to its events, but they should not duplicate the rules for phrase order, phrase growth, correctness, or completion.

`VoicePhraseNormalizer` may build alias lookup from the ritual word library so adding or changing a ritual word affects recognition normalization in one place.

`RitualController` decides when a ritual phase starts and ends. `IncantationManager` decides whether the spoken phrase is correct.

## Spell Phrase Boundary

`SpellPhraseLibrary` is separate from ritual words.

Do not merge spell/card phrases into the ritual phrase vocabulary unless explicitly requested. Spell phrases belong to future spell, card, or interference systems. Ritual words belong to the core book-and-hourglass loop.

## Paused Integrations

Notebook, card, lore, and demon reaction integrations are paused until the core ritual loop and voice recognition work reliably.

`WhisperSandbox` is a sandbox/reference area and should not be modified during core ritual work unless explicitly requested.
