# Project Status

This document describes the current reality of Incantation.

Purpose: give contributors a concise status snapshot that is rewritten whenever the project changes.

Questions answered here:

- What version or milestone is the project currently in?
- What works now?
- What is experimental, incomplete, or paused?
- What is the recommended next task?
- What known bugs or technical debt matter today?

This document does not contain historical progress notes, long-term design philosophy, milestone archives, or detailed architecture.

Read next: `Docs/NEXT_TASK.md` for the immediate objective, then `Docs/Roadmap.md` for milestone order.

## Current Version

Incantation is currently a v0.1 playable local prototype of the core seated ritual loop.

## Current Branch

Unknown from documentation. Confirm with `git branch --show-current` before branch-specific work.

## Current Milestone

Lobby.

The next major milestone is to replace local debug occupant assumptions with a real pre-ritual player flow while preserving the current local test path.

## Current Goal

Stabilize the core ritual loop and add the first lobby, ready check, automatic seating, and ritual-start handoff.

The core vision has not changed: one cursed book, one table, seated players, an hourglass, voice pressure, betrayal, tension, laughter, and memorable social moments.

The book is the main character.

## Current Stable Systems

The current prototype includes:

1. One cursed book.
2. Physical seat traversal.
3. Growing incantation by full active table rotation.
4. Local debug seat occupants.
5. Local lobby foundation in `MainGame`.
6. Explicit Start Ritual handoff from lobby into the existing ritual path.
7. `WordByWordRealtime` validation as the default prototype mode.
8. `FullPhrase` validation as an optional strict mode.
9. `WindowsKeywordVoiceRecognizer` for immediate realtime keyword validation.
10. Whisper retained for full-phrase or experimental recognition paths.
11. Isolated three-card Spell Hand visual foundation with hidden, table, raised, selected, and consumed presentation states.
12. `SpellDefinition` ScriptableObject data for card identity, text, rarity, and optional presentation references.
13. Realtime visual word absorption.
14. Wrong word rejection feedback.
15. Hourglass timer pressure.
16. Book movement after ritual acceptance.
17. Failed-seat elimination after the absorption, book aftermath, and Book Prison handoff completes.
18. Automatic ritual continuation with the remaining alive occupied seats.
19. Ambient audio.
20. Fire flicker.
21. Hourglass light possession effect.
22. Room veil and dark cabin ambience.

## Experimental Or Incomplete Systems

- `CoreRitualLoop` is the cleaner logic direction, but migration from `RitualController` is incomplete.
- `CoreRitualLoopBridge` mirrors core phrase state into legacy display paths during migration.
- Whisper remains available for full-phrase or experimental recognition paths, but it is not the default realtime path.
- Timeout and retry behavior exist in prototype form.
- Failed players can be eliminated after the Book Prison transition, and the ritual can continue with remaining alive seats.
- The last-player-remaining end-of-game presentation is not implemented yet; the ritual stops with one warning TODO log when only one alive seat remains.
- Debug occupants are useful local testing support, not the final player model.

## Known Missing Product Systems

- Ready check.
- Production automatic seating from lobby players.
- Networking.
- Production end-of-game flow for the last surviving player.
- Interference cards.
- Spell Hand gameplay, spell execution, drawing, inventory, voice activation, card replacement, and networking. The visual hand and definition-driven presentation data are present.
- Demon reactions.
- Campaign objectives.

Lobby is the next major milestone.

## Known Bugs

No active known bugs are documented here today.

When a bug becomes part of the current project state, add it here briefly and remove it when fixed. Historical bug narratives belong in `Docs/LESSONS_LEARNED.md` or `CHANGELOG.md`.

## Technical Debt

- Runtime orchestration is still split between the working `RitualController` surface and cleaner core-loop direction.
- Core-loop turn indexing must eventually reconcile with physical Seat objects while leaving physical order authority in `SeatManager`.
- Legacy incantation display paths still depend on `IncantationManager`.
- `BookController` arrival is duration-based because `BookMover` does not expose a true completion callback.
- Failed-seat elimination still lives in the prototype `RitualController` flow rather than a dedicated production game-mode rules layer.
- Inspector reference coverage is incomplete for some camera and production audio mixer values.

## Voice State

The prototype supports two voice validation modes:

- `WordByWordRealtime`: default prototype mode. Uses keyword recognition for immediate visual word validation.
- `FullPhrase`: optional strict mode. Uses full phrase transcript validation.

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.

Whisper is kept in the project but should not be forced as the only validation path.

Do not reintroduce Unity Dictation or Azure.

## Seat State

`SeatManager` owns physical seat order.

The current clockwise order is:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise order is the exact reverse.

Debug occupants are for local testing only.

## Phrase State

The phrase starts with 1 word.

Every active player speaks the same visible phrase.

The phrase grows by 1 word after every full active table rotation.

The phrase does not grow after every player.

`SpellPhraseLibrary` is separate from ritual words.

## Hard Boundaries

- Do not create multiple gameplay books.
- Do not make characters walk around.
- Do not move gameplay authority into visual-only objects.
- Do not put gameplay scripts on `BookGhost`.
- Do not modify paused systems unless explicitly requested.

## Recommended Next Task

Read `Docs/NEXT_TASK.md`.

The immediate objective is the first lobby flow: lobby entry, ready state, automatic seating through the Seat system, and handoff into the existing one-book ritual loop.

## Last Reviewed

2026-07-04 during DOCS-004 documentation governance work.
