# Technical Architecture

## Current Priority

Priority 1 is the clean core ritual loop and reliable voice recognition.

## Architecture Boundaries

- Prefer extending existing systems over creating new ones.
- Keep gameplay logic out of visual-only objects.
- Keep the Seat system logical and the chair visual.
- The Seat system owns the configured physical seating order around the table.
- Keep `BookGhost` as a visual placement reference only.
- Keep `SpellPhraseLibrary` separate from ritual words.
- Treat `WhisperSandbox` as sandbox/reference code unless explicitly asked to change it.

## Physical Seat Order

The cursed book advances according to the configured physical seating order, not according to GameObject names, seat numbers, hierarchy order, player join order, or network player index.

The Core Ritual Engine must never assume sequential numbering.

The current prototype clockwise order is:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise traversal is the exact reverse.

Traversal rules may later be temporarily changed by spell-card effects such as reverse rotation, random next player, skip occupied seat, or double jump. Those effects modify traversal behavior; they do not move ownership of physical order out of the Seat system.

## Voice Architecture

Whisper is the primary recognition path.

Windows speech recognition is fallback only.

Unity Dictation and Azure are not part of the current project plan.

## Paused Systems

Notebook, cards, lore, demon reactions, assets, visuals, and networking should not be changed during core ritual tasks unless explicitly requested.
