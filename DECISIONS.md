# Decisions

This file records current project guidance that should survive individual task conversations.

## Current Direction

- Incantation is a competitive social party game in a dark fantasy horror atmosphere.
- The cursed book is unique. Do not build one gameplay book per player.
- Players stay seated. Characters do not walk around.
- The ritual loop is centered on the lobby, ready check, automatic seating, book movement, shared spoken phrase, and voice recognition.
- The phrase starts with 1 word.
- Every player says the same current phrase when the book reaches them.
- After a full table rotation, the phrase grows by 1 word.
- Whisper is the primary voice recognition system.
- Windows speech recognition is fallback only.
- Unity Dictation and Azure are not current project dependencies.

## System Boundaries

- `SpellPhraseLibrary` is separate from ritual words. Do not merge spell/card phrases with the core ritual phrase vocabulary unless explicitly requested.
- Notebook, card, lore, and demon systems are paused until the clean core ritual loop and voice recognition are reliable.
- `WhisperSandbox` is a sandbox/reference area. Do not rework it during core ritual tasks unless explicitly requested.
- Assets, visuals, and networking are not priority areas for TASK-041 follow-up work unless explicitly requested.

## Task Discipline

- Prefer small isolated tasks.
- Do not modify more than necessary.
- One task equals one commit.
- Documentation-only tasks must not change gameplay code or scenes.
