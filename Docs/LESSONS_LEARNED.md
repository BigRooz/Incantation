# Lessons Learned

This document preserves practical discoveries from development so future work does not repeat avoidable mistakes.

Purpose: preserve practical historical learning without turning it into current status.

Questions answered here:

- What mistakes should future work avoid?
- What prototype discoveries affected gameplay feel?
- What implementation traps have already been found?

This document does not contain current project status, active task scope, authoritative gameplay law, or milestone planning.

Read next: `Docs/PROJECT_STATUS.md` for current reality or the relevant source-of-truth document before making changes.

## Gameplay Feel

- Real-time word validation feels significantly better than delayed judgment for the current prototype.
- Visual feedback must happen immediately when a word is accepted or rejected.
- Wrong word rejection is valuable because it creates panic and retry pressure while the hourglass continues.
- Gameplay feel is more important than technical elegance when choosing between two working prototype paths.
- The game is strongest when players can clearly see who is under pressure and what they are trying to say.
- The ritual should be fun before cards, demon reactions, lore delivery, or campaign objectives return.

## Phrase Progression

- Phrase growth must happen after a full active table rotation, not after each player.
- Shared phrase growth creates table-wide memory pressure.
- Per-player phrase complexity would weaken the feeling that everyone is trapped in the same ritual.
- `SpellPhraseLibrary` must remain separate from ritual words.

## Voice

- `WindowsKeywordVoiceRecognizer` is currently the best fit for immediate word-level prototype feedback.
- Whisper is useful, but it should stay in full-phrase or experimental paths unless a future task proves a realtime approach.
- Voice recognizers should emit candidates only. They should not decide success, mutate the phrase, advance turns, or eliminate players.
- Speech aliases are useful for recognition reliability, but alias learning should not merge spell/card phrases into the ritual vocabulary.

## Seats

- Physical seat order must never depend on hierarchy order.
- Physical seat order must never depend on sequential seat numbers.
- Player join order and network IDs should not define table traversal.
- `SeatManager` is the correct owner for configured physical order.
- The current clockwise order is Seat1, Seat5, Seat3, Seat6, Seat2, Seat7, Seat4, Seat8.
- Debug occupants dramatically speed up Play Mode iteration.
- Debug occupants are not the final lobby or networking model.

## Book

- One real book creates a stronger shared threat than many gameplay books.
- `BookGhost` is useful as a placement reference but must never become gameplay authority.
- Book movement after ritual acceptance improves responsiveness, but orchestration must avoid duplicate movement on the next turn.
- Arrival is currently duration-based through `BookMover.moveDuration`; a future real arrival callback would be cleaner.

## Hourglass And Pressure

- The hourglass is the pressure system, not an elimination authority by itself.
- Timeout behavior should be explicit and should not restart duplicate listeners, duplicate timers, or duplicate movement.
- Possession lighting near the end of the timer improves ritual pressure when it remains subordinate to gameplay clarity.

## Atmosphere

- Fire ambience greatly improves immersion.
- Small atmosphere improvements create large emotional impact when they support the table.
- Ambient audio, fire flicker, room veil, and possession lighting make the prototype feel more complete without changing the gameplay rules.
- Atmosphere should not become a reason to expand scope before the core ritual is reliable.

## Architecture

- Modular systems reduce regressions.
- The project is in a runtime migration state; `RitualController` still matters even though `CoreRitualLoop` points to a cleaner future.
- Incremental migration is safer than replacing the working prototype in one pass.
- Stable gameplay should be protected before adding abstractions or new systems.
- UI should display phrase state, not own phrase authority.
- Visual components should react to gameplay, not drive gameplay.

## Workflow

- Small validated tasks are safer than broad cleanup passes.
- Documentation-only tasks must not modify scripts, scenes, prefabs, or assets.
- `git status` before editing is mandatory because Unity scene files can be dirty for unrelated reasons.
- If a task changes a decision, update the decision rationale and the source-of-truth docs in the same task.
- New developers need both what exists and why it exists; preserving rationale is as important as listing files.
