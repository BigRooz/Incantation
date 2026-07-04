# Incantation

Purpose: introduce the repository and point contributors to the official documentation entry point.

Questions answered here:

- What is Incantation?
- Where should a new contributor start?
- What is the short version of the current prototype?

This document does not contain the authoritative reading order, full current status, detailed architecture, or task workflow.

Read next: `Docs/START_HERE.md`.

Incantation is a Unity 6 multiplayer party game set in a dark fantasy horror atmosphere.

Tagline:

> Speak forbidden words. Betray your friends. Be the last mage standing.

The game is built around one cursed book, one table, seated players, an hourglass, and voice-driven ritual pressure. Horror is the atmosphere; the real goal is memorable social chaos.

## Start Here

New developers should begin with `Docs/START_HERE.md`.

That document is the repository onboarding map and official reading order. It points to the authoritative docs, current prototype state, design rationale, architecture boundaries, workflow, and next task.

`Docs/GAMEPLAY_TRUTH.md` is the gameplay source of truth. If another gameplay document conflicts with it, update the conflicting document or deliberately update `GAMEPLAY_TRUTH.md`.

## Current v0.1 Prototype

The current playable prototype is a local table-ritual slice.

It includes:

- One real cursed book.
- Physical seat traversal around the table.
- Shared incantation phrase growth by full active table rotation.
- Local debug seat occupants for Play Mode testing.
- `WordByWordRealtime` validation as the default prototype mode.
- `FullPhrase` validation as an optional strict mode.
- `WindowsKeywordVoiceRecognizer` for immediate realtime keyword validation.
- Whisper retained for full-phrase or experimental recognition paths.
- Realtime visual word absorption.
- Wrong word rejection feedback.
- Hourglass timer pressure.
- Book movement after ritual acceptance.
- Ambient audio, fire flicker, hourglass light possession, room veil, and dark cabin ambience.

## Current Vision

- Players enter through a lobby.
- Players ready up.
- When the ritual begins, players sit automatically around the table.
- One real cursed book moves from player to player.
- The ritual phrase starts with 1 word.
- All active players say the same visible phrase when the book reaches them.
- After a full active table rotation, 1 new word is added to the shared phrase.
- The phrase does not grow after every player.
- Voice validation supports both realtime word validation and optional full-phrase validation.
- Unity Dictation and Azure voice services are not part of the current plan.

## Next Milestone

Lobby is the next major milestone.

Networking is not implemented yet. Current debug occupants exist only to test the ritual loop locally.

## Current Priority

Priority 1 is the clean core ritual loop and reliable voice interaction.

Paused systems include notebook, cards, lore delivery, demon reactions, campaign objectives, and networking polish. They should wait until the core table, book, hourglass, and voice loop works.

## Engine

Unity 6

## Status

v0.1 playable prototype.
