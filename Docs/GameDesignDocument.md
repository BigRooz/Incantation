# Game Design Document

This document summarizes the current design direction.

For onboarding, read `Docs/START_HERE.md`.

For authoritative gameplay rules, read `Docs/GAMEPLAY_TRUTH.md`.

## Current Core Design

Incantation is a seated multiplayer party game built around one cursed book, one table, an hourglass, and spoken ritual pressure.

It is a competitive social party game in a dark fantasy horror atmosphere.

The core vision has not changed: the book is the main character, voice is central, and the goal is memorable social chaos.

## v0.1 Playable Prototype

The current prototype is a local table ritual, not the full multiplayer product.

Current prototype features:

- One cursed book.
- Physical seat traversal.
- Local debug seat occupants.
- Growing incantation by full active table rotation.
- `WordByWordRealtime` voice validation as the default prototype feel.
- `FullPhrase` validation as an optional strict mode.
- Realtime visual word absorption.
- Wrong word rejection feedback.
- Hourglass timer pressure.
- Book movement after ritual acceptance.
- Ambient audio.
- Fire flicker.
- Hourglass light possession effect.
- Room veil and dark cabin ambience.

## Current Flow

1. Local debug occupants fill active seats for testing.
2. The single cursed book moves to the active seat.
3. The phrase starts with 1 word.
4. The active player says the visible phrase.
5. In `WordByWordRealtime`, words are accepted one at a time with immediate feedback.
6. In `FullPhrase`, the full transcript is validated against the visible phrase.
7. Wrong words are rejected while time remains.
8. Accepted words are visually absorbed.
9. When the phrase is accepted, the book moves to the next active seat.
10. All active players say the same current phrase during their turns.
11. After a full active table rotation, 1 word is added.

## Next Milestone

Lobby is the next major milestone.

The next design step is:

1. Lobby entry.
2. Ready check.
3. Automatic seating.
4. Handoff into the existing table ritual.

Networking is not implemented yet.

## Paused Systems

Notebook, cards, lore, campaign, networking, and demon reactions are paused until the core voice loop works.

Do not create multiple gameplay books.

Do not make characters walk around.
