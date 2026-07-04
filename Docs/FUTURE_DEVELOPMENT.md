# Future Development

This document describes the current milestone, future milestones, known limitations, technical debt, and production priorities.

For the short milestone list, read `Docs/MILESTONES.md`. For project onboarding, read `Docs/START_HERE.md`.

## Current Milestone

The current milestone is Lobby.

Goal:

Move from local debug occupants to a real pre-ritual player flow that can hand active players into the existing seated ritual loop.

Expected scope:

1. Lobby entry.
2. Ready check.
3. Minimum and maximum player rules for the prototype.
4. Automatic seat assignment through `SeatManager` physical order.
5. Ritual start handoff into the current loop.
6. Preservation of local debug occupant testing until lobby seating is stable.

Out of scope:

- Production networking flow unless explicitly scoped.
- Interference cards.
- Demon reactions.
- Campaign objectives.
- New art pass.
- Multiple gameplay books.
- Walking characters.

## Next Milestone

After lobby handoff is stable, formalize failure and elimination.

Expected scope:

1. Retry rules.
2. Timeout behavior.
3. Elimination rules.
4. Skipping eliminated seats.
5. End condition for Last Priest Standing.
6. Clear feedback when a player fails, retries, times out, or is eliminated.

The first elimination pass should stay simple. Avoid extra punishment systems until the table loop is reliable.

## Later Milestones

Future milestones should happen in this approximate order:

1. Networked player flow.
2. Interference cards.
3. Demon reactions.
4. Multiplayer polish.
5. Campaign objectives.
6. Lore delivery.
7. Broader content and balance.

Every milestone must strengthen the table ritual.

## Known Technical Debt

### Runtime Migration

`RitualController` is still the working prototype orchestration surface.

`CoreRitualLoop`, `TurnManager`, `GrowingIncantationManager`, and `PhraseValidator` represent the cleaner logic direction, but the migration is incomplete.

Future work should migrate incrementally while keeping Play Mode working.

### Core Loop Versus Physical Seats

`TurnManager` currently tracks player count and indexes. `SeatManager` owns physical Seat order.

Production work should connect core-loop state to physical Seats without moving physical-order authority out of `SeatManager`.

### Legacy Display Bridge

`CoreRitualLoopBridge` mirrors core phrase state into `IncantationManager` while UI and feedback still depend on the legacy event model.

Future work should let UI read from the cleaner core phrase authority directly, then remove bridge-only legacy mirroring.

### Book Arrival

`BookController` currently treats arrival as complete after `BookMover.moveDuration`.

Future work should expose a true movement completion callback or awaitable movement result from `BookMover`.

### Timeout And Elimination

Timeout exists in prototype form, but production elimination is not complete.

Future work should separate:

- Timer expiry.
- Turn failure.
- Retry eligibility.
- Elimination.
- Game mode end condition.

### Debug Occupants

Debug occupants are useful for local testing, but they are not the final player model.

Lobby work should preserve debug testing while adding a production seating path.

### Inspector Reference Coverage

`Docs/INSPECTOR_REFERENCE.md` records known intentional values, but camera and production audio mixer values are not yet authoritative.

Future tuning should update that document.

## Known Limitations

- No lobby yet.
- No ready check yet.
- No production automatic seating from lobby players yet.
- No real multiplayer networking flow yet.
- No networked player seating yet.
- No production elimination flow yet.
- No interference cards yet.
- No demon reactions yet.
- No campaign objectives yet.
- `WindowsKeywordVoiceRecognizer` is Windows-only.
- Whisper remains experimental or full-phrase oriented for the current plan.
- The prototype relies on local scene setup and inspector references.

## Production Priorities

Priority order:

1. Core loop stability.
2. Reliable voice interaction.
3. Lobby.
4. Ready check.
5. Automatic seating.
6. Retry, timeout, failure, and elimination.
7. Networked player flow.
8. Interference cards.
9. Demon reactions.
10. Multiplayer polish.
11. Campaign objectives.

Do not let later priorities distract from earlier ones.

## Readiness Questions

Before starting a future feature, answer:

1. Does this make the ritual more memorable?
2. Does this preserve one real book?
3. Does this keep players seated?
4. Does this keep voice central?
5. Does this preserve physical seat order ownership?
6. Does this avoid paused systems unless explicitly scoped?
7. Can it be validated in Play Mode?
8. Which documentation must be updated if this changes a decision?
