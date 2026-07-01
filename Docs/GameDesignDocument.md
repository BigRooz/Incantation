# Game Design Document

## Current Core Design

Incantation is a seated multiplayer party game built around one cursed book, one table, an hourglass, and spoken ritual pressure.

## Current Flow

1. Players enter a lobby.
2. Players ready up.
3. Players sit automatically when the ritual starts.
4. The single cursed book moves to a player.
5. The phrase starts with 1 word.
6. The active player says the full visible phrase.
7. The book moves to the next player.
8. All players say the same current phrase during their turns.
9. After a full table rotation, 1 word is added.
10. Whisper validates the spoken phrase.
11. Windows speech recognition is fallback only.

## Paused Systems

Notebook, cards, lore, and demon reactions are paused until the core voice loop works.
