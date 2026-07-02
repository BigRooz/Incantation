# Book State Machine

## Purpose

This document defines the official book state machine for Incantation.

The book is the main character of the ritual. It owns turns, chooses when pressure begins, advances after every resolved turn, and keeps the ritual from becoming stuck on one player.

This document is a design and architecture reference. It does not authorize code changes, scene changes, networking changes, visual changes, card implementation, demon reactions, or lore delivery by itself.

The state machine exists to make these rules explicit:

- The book owns turns.
- The book follows Seat System traversal.
- The book advances after every turn result.
- The book may pause before moving.
- The book never gets permanently stuck.

## State List

### Idle

The ritual is not active.

The book may be at a default table position, at its last known position, or in a scene setup position. No player is active, no hourglass is running, and no ritual listening session is active.

Allowed influences:

- Lobby or ritual setup may request a transition toward `WaitingForPlayers`.
- Debug or editor setup may position the book before runtime.
- No gameplay system may start a turn from `Idle` without ritual setup.

### WaitingForPlayers

The book is waiting for the valid seated player set.

This state covers lobby readiness, automatic seating, and the handoff into the ritual loop. The Seat System must provide occupied active seats before the book can begin choosing a destination.

Allowed influences:

- Lobby and ready-check systems may determine when enough players are ready.
- Automatic seating may assign players to Seats.
- Seat System provides the active occupied seat set and traversal rule.
- Core ritual orchestration may initialize phrase, turn, and book state.

### MovingToSeat

The real book is moving to a target Seat destination.

The destination must come from Seat System traversal or an authorized traversal modifier. The book moves to the Seat's book destination, such as `BookTarget` or `BookGhost` according to the current book movement adapter. `BookGhost` remains visual-only and must not own gameplay logic.

Allowed influences:

- Book movement system controls interpolation, travel duration, hesitation, and arrival detection.
- Seat System owns the target Seat selection.
- Future pacing modifiers may slow, speed, delay, freeze, or dramatize movement.
- Core ritual orchestration may cancel movement only when the ritual stops or game ends.

### ArrivedAtSeat

The book has reached the chosen Seat.

The active player is the player seated at the Seat where the book has arrived. The player does not own the turn. The book's arrival creates the turn.

Allowed influences:

- Book movement system raises or reports arrival.
- Core ritual orchestration confirms the active Seat is still valid.
- Seat or player state may invalidate the destination if the player was eliminated or removed before arrival.

### WaitingBeforeTurn

The book has arrived, but pressure has not started yet.

This is a short breath before the turn begins. It can create tension, laughter, eye contact, accusations, and panic before the hourglass flips.

Allowed influences:

- Book pacing rules may define the pause length.
- Future spells may extend, shorten, skip, or freeze this pause.
- Core ritual orchestration owns the transition into `Listening`.
- UI and audio may react, but may not advance the state on their own.

### Listening

The active player's recitation window is open.

The hourglass is running. The visible shared ritual phrase is shown. The active player must speak the full phrase. Whisper is the primary recognition path. Windows speech recognition is fallback only. `PhraseValidator` is the only authority for whether the spoken phrase satisfies the current visible ritual phrase.

Allowed influences:

- Hourglass system provides timeout pressure.
- WhisperController provides recognized phrase candidates.
- Windows speech recognition may provide fallback candidates only through the approved fallback path.
- PhraseValidator decides whether a candidate matches the current full visible phrase.
- GrowingIncantationManager provides the current shared phrase but does not advance turns.
- Other players may interfere in the future only through approved interference systems.

### ResolvingTurn

The book is resolving the active turn result.

This state handles success, timeout, failure, elimination, retry decisions, recognition grace windows, and turn bookkeeping. Traversal does not happen until the turn result has been resolved.

Allowed influences:

- Core ritual orchestration owns turn resolution.
- PhraseValidator provides success or mismatch results.
- Hourglass system provides timeout results.
- Elimination rules may deactivate the player's Seat.
- TurnManager or equivalent turn-order authority records completion and rotation progress.
- GrowingIncantationManager adds exactly one word only after a full active-seat rotation.

### WaitingBetweenTurns

The turn has resolved, and the book is preparing to advance.

This is the table's reaction window after the result. Players should have time to laugh, accuse, panic, celebrate, or watch the consequences land before the next destination is chosen or movement begins.

Allowed influences:

- Book pacing rules may define the delay.
- Future spells may shorten, extend, freeze, or skip this delay.
- Core ritual orchestration may skip this state only when the game is ending.
- UI, audio, and animation may react, but may not choose the next Seat.

### ChoosingNextSeat

The book asks the Seat System where it should go next.

The Seat System owns physical traversal. The Core Ritual Engine and Book state machine must not assume seat numbering, hierarchy order, player join order, or network player index.

Allowed influences:

- Seat System provides the next active destination according to the current traversal rule.
- TurnManager or equivalent turn-order authority may track visited active Seats and rotation completion.
- Elimination state removes Seats from active traversal.
- Future traversal modifiers may change direction, skip, randomize, double jump, freeze, delay, or otherwise bend traversal through approved rule changes.

### GameOver

The ritual has reached a valid ending condition.

The book stops advancing turns. No new listening session starts. The final state may trigger game-over presentation, winner handling, campaign completion handling, or future demon consequence handling.

Allowed influences:

- Game mode rules decide when the ritual has ended.
- Core ritual orchestration stops listening, stops the hourglass, and prevents new turns.
- UI and end-of-game systems may display results.
- Future demon reactions may respond after the core loop is complete, but may not retroactively own turn resolution.

## State Transitions

| From | To | Trigger | Owner |
| --- | --- | --- | --- |
| `Idle` | `WaitingForPlayers` | Ritual setup begins | Lobby or ritual setup entry point |
| `WaitingForPlayers` | `ChoosingNextSeat` | Players are seated, ready, and ritual state is initialized | Core ritual orchestration |
| `ChoosingNextSeat` | `MovingToSeat` | Seat System returns a valid active destination | Book state machine through Seat System traversal |
| `ChoosingNextSeat` | `GameOver` | No valid next destination exists or game mode ending is reached | Game mode rules through core ritual orchestration |
| `MovingToSeat` | `ArrivedAtSeat` | Real book reaches its Seat destination | Book movement system |
| `MovingToSeat` | `ChoosingNextSeat` | Target Seat becomes invalid before arrival | Core ritual orchestration through Seat System validation |
| `MovingToSeat` | `GameOver` | Ritual is stopped or game mode ending is reached | Core ritual orchestration |
| `ArrivedAtSeat` | `WaitingBeforeTurn` | Arrival is accepted for a valid active Seat | Core ritual orchestration |
| `ArrivedAtSeat` | `ChoosingNextSeat` | Arrived Seat is no longer active | Core ritual orchestration through Seat System validation |
| `WaitingBeforeTurn` | `Listening` | Pre-turn pause completes | Book pacing rules through core ritual orchestration |
| `WaitingBeforeTurn` | `GameOver` | Ritual is stopped or game mode ending is reached | Core ritual orchestration |
| `Listening` | `ResolvingTurn` | Valid phrase, timeout, cancellation, or terminal recognition result occurs | Core ritual orchestration |
| `ResolvingTurn` | `Listening` | Player may retry and time remains | Core ritual orchestration |
| `ResolvingTurn` | `WaitingBetweenTurns` | Turn result is final and the ritual continues | Core ritual orchestration |
| `ResolvingTurn` | `GameOver` | Turn result creates or reveals a game-ending condition | Game mode rules through core ritual orchestration |
| `WaitingBetweenTurns` | `ChoosingNextSeat` | Between-turn delay completes | Book pacing rules through core ritual orchestration |
| `WaitingBetweenTurns` | `GameOver` | Ritual is stopped or game mode ending is reached | Core ritual orchestration |
| `GameOver` | `Idle` | New ritual reset is explicitly requested | Lobby or ritual setup entry point |

## Transition Ownership

The book state machine owns which state the book is in, but it does not own every rule that causes a transition.

- Lobby and ready-check systems own readiness before the ritual starts.
- Seat System owns physical Seat order and next-destination selection.
- Book movement owns movement start, cancellation, and arrival reporting.
- Core ritual orchestration owns state coordination and prevents duplicate movement, listening, timer starts, and turn resolution.
- Hourglass owns timeout pressure during `Listening`.
- WhisperController owns primary recognition session flow.
- Windows speech recognition owns only fallback candidate generation when enabled.
- PhraseValidator owns phrase correctness.
- GrowingIncantationManager owns the current shared phrase and one-word growth after full active-seat rotations.
- Game mode rules own valid ending conditions.

No UI, visual-only object, `BookGhost`, card, spell, demon reaction, voice chat system, networking transport, or lore system may directly force a turn to succeed, fail, advance, or choose the next Seat.

## Allowed State Influences

### Book-Owned Influences

The book state machine may influence:

- When a turn exists.
- When the book starts moving.
- Whether the book pauses before a turn.
- Whether the book pauses between turns.
- When the next Seat is requested.
- Whether the ritual should stop advancing because the game is over.

### Seat-Owned Influences

The Seat System may influence:

- Which occupied active Seats can participate.
- Physical clockwise traversal.
- Physical counter-clockwise traversal.
- Future traversal patterns layered over the configured physical order.
- Skipping empty or inactive Seats.

The Seat System must not validate speech, grow phrases, start listening, or decide player consequences.

### Voice-Owned Influences

Voice systems may influence:

- When recognized text candidates become available.
- Whether recognition is still processing.
- Whether fallback recognition is needed.

Voice systems must not decide gameplay success. They only provide candidates to validation.

### Hourglass-Owned Influences

The hourglass may influence:

- When the speaking window expires.
- Whether a turn enters timeout resolution.
- How much pressure the active player feels.

The hourglass must not choose the next Seat, mutate the phrase, or directly eliminate a player without core turn resolution.

### Phrase-Owned Influences

GrowingIncantationManager may influence:

- The current visible ritual phrase.
- Phrase initialization.
- Adding exactly one word after a full active-seat rotation.

It must not choose active players, move the book, listen to microphones, or decide if a recognized candidate is correct.

### Validation-Owned Influences

PhraseValidator may influence:

- Whether the recognized phrase matches the expected full visible phrase.
- Failure reasons for mismatch if structured validation exists.

It must not advance turns, eliminate players, move the book, or mutate the phrase.

### Future Spell-Owned Influences

Future spells may influence:

- Pacing.
- Movement speed.
- Direction.
- Random destination rules.
- Freeze duration.
- Skip rules.
- Delay rules.

Future spells must modify approved rule inputs. They must not bypass the book state machine, replace Seat System traversal ownership, create private player-owned turns, or directly mutate the shared ritual phrase.

## Success And Failure

Success and failure affect consequences first, then advancement.

### Success

When the active player speaks the full visible phrase correctly before timeout:

1. `Listening` transitions to `ResolvingTurn`.
2. Listening stops.
3. The hourglass stops.
4. The turn is marked successful.
5. Turn completion is recorded for active-seat rotation tracking.
6. If every active Seat has been visited exactly once in the current rotation, the phrase gains exactly one word.
7. The state machine transitions to `WaitingBetweenTurns` unless game mode rules end the game.
8. The book later transitions to `ChoosingNextSeat`.

Success does not choose the next Seat directly. Traversal still belongs to the Seat System.

### Failure While Time Remains

When the active player says an invalid phrase while time remains:

1. `Listening` transitions to `ResolvingTurn`.
2. PhraseValidator reports mismatch.
3. Core ritual orchestration decides whether retry is allowed.
4. If retry is allowed and time remains, the state returns to `Listening`.
5. The book does not move during retry.

Failure while time remains does not advance traversal unless the failure rule explicitly ends the turn.

### Timeout Or Final Failure

When the hourglass expires without a valid phrase, or when a future rule makes failure final:

1. `Listening` transitions to `ResolvingTurn`.
2. Listening stops or enters a short approved recognition-processing grace window.
3. The failure consequence is applied.
4. The player may be eliminated if the active game mode and current rules say timeout eliminates.
5. Seat active state is updated before the next destination is selected.
6. The state machine transitions to `WaitingBetweenTurns` unless game mode rules end the game.
7. The book later transitions to `ChoosingNextSeat`.

Failure consequences do not own traversal. After consequences update player and Seat state, traversal asks the Seat System for the next valid active destination.

## Eliminated Players

Eliminated players deactivate their Seat for traversal.

An eliminated Seat:

- Is skipped when choosing the next destination.
- Does not count as active for future rotation completion.
- Does not need to speak the phrase in future rotations.
- Must not permanently trap the book.

If the current player is eliminated during `ResolvingTurn`, the state machine still advances through `WaitingBetweenTurns` and `ChoosingNextSeat` unless the game is over.

If the next chosen destination becomes eliminated before the book arrives, `MovingToSeat` returns to `ChoosingNextSeat` and requests another valid active destination.

If elimination leaves no valid active destination, or leaves a game-mode-defined winner or ending condition, the state machine transitions to `GameOver`.

## Future Spell Modifiers

Future spells should bend the state machine by changing authorized rule inputs, not by replacing the book as the ritual driver.

### Pacing

Spells may:

- Increase or decrease `WaitingBeforeTurn`.
- Increase or decrease `WaitingBetweenTurns`.
- Add hesitation before `MovingToSeat`.
- Shorten or extend movement duration.

Pacing spells must eventually release the state unless the game enters `GameOver`.

### Movement

Spells may:

- Speed up the book.
- Slow down the book.
- Add dramatic curves or hesitation.
- Fake toward one Seat before moving to the true Seat.
- Temporarily freeze the book.

Movement spells must still preserve one real book and must not create one book per player.

### Direction

Spells may:

- Reverse clockwise traversal into counter-clockwise traversal.
- Restore default direction after a duration or turn count.
- Apply cursed directional patterns through the Seat System.

Direction changes must use the configured physical Seat order and must not assume sequential Seat numbering.

### Random Destination

Spells may:

- Request a random active Seat.
- Exclude the current Seat.
- Exclude recently visited Seats if the active traversal rule allows it.

Random destination rules still belong to Seat System traversal. They must skip empty and eliminated Seats.

### Freeze

Spells may:

- Freeze the book before movement.
- Freeze the book during movement.
- Freeze the book between turns.

Freeze effects must have a release condition, duration, cancellation condition, or game-ending condition. The book may create suspense, but it must never get permanently stuck.

### Skip

Spells may:

- Skip the next active Seat.
- Skip a specific active Seat once.
- Double jump through traversal.

Skip effects must update rotation tracking clearly so phrase growth still means every active Seat in the current living ritual circle has been visited as defined by the active traversal rules.

### Delay

Spells may:

- Delay listening after arrival.
- Delay choosing the next Seat.
- Delay movement after choosing a Seat.

Delay effects must not allow duplicate listening sessions, duplicate hourglass starts, or duplicate turn resolution.

## Rules

- The book owns turns.
- Players do not own turns.
- The active player is the player seated where the book is located.
- The book follows Seat System traversal.
- The Seat System owns physical Seat order.
- The book advances after every final turn result unless the game is over.
- The book may pause before moving.
- The book may pause before a turn.
- The book may pause between turns.
- The book never gets permanently stuck.
- Empty Seats are skipped.
- Eliminated Seats are skipped.
- Success and failure do not directly own traversal.
- Consequences update player, Seat, phrase, or game state before traversal asks for the next destination.
- Phrase growth happens after a full active-seat rotation, not after every turn.
- Whisper is the primary recognition path.
- Windows speech recognition is fallback only.
- PhraseValidator is the only phrase correctness authority.
- GrowingIncantationManager is the only current ritual phrase authority.
- `SpellPhraseLibrary` remains separate from ritual words.
- `BookGhost` is visual-only and must not contain gameplay scripts.
- Future spells modify approved pacing, movement, traversal, or delay rules instead of bypassing the book state machine.
