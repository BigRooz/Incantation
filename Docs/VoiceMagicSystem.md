# Voice Magic System

Purpose: define current voice-recognition direction and recognition-versus-validation boundaries.

Questions answered here:

- Which voice validation modes exist?
- Which recognizer path is preferred for realtime prototype play?
- What should voice recognizers not own?

This document does not contain current task planning, full ritual architecture, ritual word vocabulary ownership, or external voice-service setup.

Read next: `Docs/IncantationSystem.md` for phrase ownership and `Docs/CoreRitualLoopArchitecture.md` for ritual coordination.

## Current Voice Plan

The current prototype supports two ritual validation modes:

1. `WordByWordRealtime`
2. `FullPhrase`

`WordByWordRealtime` is the default prototype mode. It uses keyword recognition to validate each expected word immediately, creating the current realtime visual word absorption feel.

`FullPhrase` is an optional strict mode. It validates a full phrase transcript against the full visible ritual phrase.

Whisper is kept in the project, but it should not be forced as the only validation path.

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.

Do not use Unity Dictation.

Do not use Azure voice services.

## Recognition Versus Validation

Voice recognizers produce candidates.

Ritual validation decides whether those candidates satisfy the current phrase.

In `WordByWordRealtime`, the recognizer emits recognized words or aliases and the ritual accepts or rejects the next expected word.

In `FullPhrase`, the recognizer emits a full transcript candidate and the ritual validates it against the full visible phrase.

Wrong words should produce rejection feedback without automatically changing the phrase or moving the book.

Accepted words may be visually absorbed in realtime.

## Ritual Phrase Rules

- The phrase starts with 1 word.
- All active players say the same visible phrase.
- The active player must satisfy the selected validation mode.
- After the book completes a full active table rotation, 1 word is added.
- The phrase does not grow after every player.

## Current Recognizer Guidance

- Prefer `WindowsKeywordVoiceRecognizer` for the current realtime prototype loop.
- Use Whisper for full-phrase or experimental recognition tasks when explicitly needed.
- Keep `WhisperSandbox` as a sandbox/reference area unless explicitly asked to change it.
- Keep recognizers behind `IVoiceRecognizer` where possible.

## Spell Phrase Boundary

`SpellPhraseLibrary` is separate from ritual words.

Do not merge spell/card phrase work into the core ritual phrase system unless explicitly requested.
