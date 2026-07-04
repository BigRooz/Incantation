# Why These Decisions Exist

This document records the reasoning behind major Incantation decisions so future developers understand what they are protecting before changing it.

Purpose: explain why durable design and technical decisions exist.

Questions answered here:

- Why does Incantation have one book?
- Why are players seated?
- Why does phrase growth happen by rotation?
- Why is realtime word-by-word validation the default prototype feel?
- Why are some systems paused?

This document does not contain today's status, the immediate next task, detailed code ownership, or milestone history.

Read next: `DECISIONS.md` for the shorter decision ledger, then the relevant system document before changing implementation.

For the shorter decision ledger, read `DECISIONS.md`. For strict gameplay law, read `Docs/GAMEPLAY_TRUTH.md`.

## Why One Book

There is one real gameplay book because the book is the main character.

One shared book makes the room feel manipulated by the same cursed presence. Players can feel chosen, spared, threatened, or betrayed by where it moves. Multiple gameplay books would split attention and turn the ritual into parallel personal tasks instead of one shared social event.

Future visual effects may duplicate pages, shadows, echoes, or illusions, but gameplay authority should remain with one main cursed book unless the core identity is deliberately changed.

## Why Players Stay Seated

Players stay seated because the game happens around the table.

Seated play focuses development and player attention on:

- The book.
- The hourglass.
- Spoken words.
- Faces.
- Hands.
- Subtle body reactions.
- Social pressure.

Walking would shift the game toward navigation, locomotion, collision, level design, and broad animation work. Those are expensive distractions from the ritual.

## Why Clockwise Seat Traversal

The book travels through a configured physical table order because the table is a place, not a sorted list.

The current clockwise order is:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise is the exact reverse.

This order belongs to `SeatManager`. It must not be inferred from hierarchy order, seat number, GameObject name, player join order, or network player index. Future traversal effects may reverse, skip, jump, or randomize within the physical seat system, but they should not steal ownership from the Seat system.

## Why Growing Incantations

Growing phrases create shared table pressure.

The phrase starts with one word so the ritual is immediately understandable. It grows by one word after a full active table rotation so every active player faces the same phrase before difficulty increases.

Growing after every player would spike difficulty too quickly and make different players carry different burdens during the same rotation. Per-player phrase growth would weaken shared memory pressure.

## Why Real-Time Validation

`WordByWordRealtime` is the default prototype mode because immediate response feels better.

When a correct word is accepted instantly, players see the phrase being consumed as they speak. When a wrong word is rejected immediately, the mistake lands while the hourglass is still draining. That produces stronger panic, comedy, and retry pressure than waiting for a delayed full transcript judgment.

## Why Full Phrase Still Exists

`FullPhrase` remains because strict full recitation is still useful.

It can support:

- Alternate difficulty.
- Regression testing.
- Whisper experiments.
- Future modes that intentionally require complete phrase delivery.
- Comparison against realtime validation.

Keeping it does not mean it should replace `WordByWordRealtime` as the default prototype feel.

## Why Windows Keyword Recognition Is Currently Preferred

`WindowsKeywordVoiceRecognizer` is currently preferred for the realtime prototype because it can recognize configured ritual words and aliases immediately.

That matches the desired default feel: word-by-word acceptance, visible absorption, wrong-word rejection, and quick retry while time remains.

Its platform limitation is acceptable during the current Windows-focused prototype phase. Non-Windows paths can use mock or alternate recognizers for testing, but should not change the default design target without an explicit task.

## Why Whisper Is Still Part Of The Project

Whisper remains useful for full-phrase and experimental recognition paths.

It can evaluate longer transcripts, support research, and provide options when strict phrase recognition is desired. However, it is not currently the best default for realtime word-by-word feedback. It should not be forced as the only validation path.

`WhisperSandbox` is a sandbox/reference area. Do not rework it during core ritual tasks unless explicitly requested.

## Why Unity Dictation And Azure Are Not Used

Unity Dictation and Azure voice services are outside the current plan.

The prototype already has a clearer realtime path through Windows keyword recognition and an experimental full-phrase path through Whisper. Adding dictation or cloud services would increase complexity, dependency risk, and configuration burden before the core loop is proven.

## Why Modular Architecture

Incantation has many systems that can easily blur together: seats, book movement, voice recognition, validation, phrase growth, timers, UI, ambience, lobby, networking, cards, demon reactions, and game modes.

Modular ownership protects the prototype:

- `SeatManager` owns physical order.
- Book components move the one real book.
- Voice recognizers produce candidates.
- Validation decides whether candidates satisfy the visible phrase.
- Phrase systems own phrase state.
- Hourglass systems own timer pressure.
- Ritual orchestration coordinates systems.
- Game mode rules eventually own elimination and endings.

This separation makes the project easier to migrate without breaking the playable loop.

## Why One Cabin

The cabin is the ritual container.

One table in one dark room keeps scope focused and gives the game a strong first playable identity. More rooms, traversal spaces, or exploration goals would dilute the social ritual and pull work away from lobby, seating, voice reliability, timeout, and elimination.

Atmosphere should support the table. It should not replace the table.

## Why No Walking

No walking is a deliberate design and production choice.

The memorable moments should come from speech, hesitation, betrayal, and the book's movement, not from pathfinding or player locomotion. Character work should focus on seated embodiment: heads, eyes, mouth, shoulders, posture, hands, and reactions.

## Why Inspector-Driven Configuration

Unity scene relationships are visible and tuneable in the Inspector.

Serialized references are preferred because they make scene setup explicit and reduce fragile runtime discovery. `GameObject.Find` and hardcoded object names should be avoided where a serialized reference communicates ownership better.

Inspector-driven setup is especially important for:

- Physical seat order.
- Book destinations.
- Voice recognizer selection.
- Hourglass duration.
- Incantation display references.
- Lighting and ambience tuning.

## Why Debug Occupants Exist

Debug occupants exist to keep the local ritual testable before lobby and networking are ready.

They are not the production player model. They should remain available until lobby seating is stable, then gradually move out of the critical path. Do not build game mode rules, identity, or networking assumptions around them.

## Why Secondary Systems Are Paused

Cards, demon reactions, notebook, lore delivery, campaign objectives, and networking polish are paused because the core loop must stand on its own.

The game must be fun with:

- Seated players.
- One moving book.
- The hourglass.
- A shared visible phrase.
- Voice validation.
- Wrong-word feedback.
- Retry.
- Timeout.
- Elimination.

Once that works end to end, secondary systems can be judged by whether they increase social tension around the table.
