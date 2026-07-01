# Technical Architecture

## Current Priority

Priority 1 is the clean core ritual loop and reliable voice recognition.

## Architecture Boundaries

- Prefer extending existing systems over creating new ones.
- Keep gameplay logic out of visual-only objects.
- Keep the Seat system logical and the chair visual.
- Keep `BookGhost` as a visual placement reference only.
- Keep `SpellPhraseLibrary` separate from ritual words.
- Treat `WhisperSandbox` as sandbox/reference code unless explicitly asked to change it.

## Voice Architecture

Whisper is the primary recognition path.

Windows speech recognition is fallback only.

Unity Dictation and Azure are not part of the current project plan.

## Paused Systems

Notebook, cards, lore, demon reactions, assets, visuals, and networking should not be changed during core ritual tasks unless explicitly requested.
