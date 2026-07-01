# Voice Magic System

## Current Voice Plan

Whisper is the primary voice recognition system.

Windows speech recognition is fallback only.

Do not use Unity Dictation.

Do not use Azure voice services.

## Ritual Phrase Rules

- The phrase starts with 1 word.
- All players say the same visible phrase.
- The active player must say the full current phrase.
- After the book completes a full table rotation, 1 word is added.

## Spell Phrase Boundary

`SpellPhraseLibrary` is separate from ritual words.

Do not merge spell/card phrase work into the core ritual phrase system unless explicitly requested.
