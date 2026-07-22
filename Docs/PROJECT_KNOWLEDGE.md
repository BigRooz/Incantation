# Project Knowledge

This document preserves critical project knowledge that should not depend on previous conversations.

Purpose: preserve the current code map, runtime migration notes, known traps, and validation expectations.

Questions answered here:

- What does the prototype really prove?
- Which scripts currently own which responsibilities?
- Where is the runtime mid-migration?
- What design rules are easy to break?
- How should core-loop code tasks be manually validated?

This document does not contain the immediate next task, current status snapshot, historical milestone archive, or durable design-law authority.

Read next: the focused system document for the area you are changing, then `Docs/WORKFLOW.md` before implementation.

Read this after `Docs/START_HERE.md` and `Docs/PROJECT_STATUS.md` when taking over development, planning a feature, or deciding whether an old system is current, paused, legacy, or experimental.

## Current Engine And Project Shape

- Unity version: 6000.0.56f1.
- Render pipeline: URP 17.0.4.
- Input package is present, but existing prototype scripts still include older input patterns in places.
- The current playable scene is `Assets/Scenes/MainGame.unity`.
- The prototype is local-first. Networking packages or Unity multiplayer helpers in the project do not mean production networking is implemented.

## What The Prototype Really Is

The current game is a local ritual prototype.

It proves:

- One real book can move around the table.
- Seats can be occupied locally for Play Mode testing.
- The ritual phrase can be shared by all active players.
- The phrase can grow by one word after a full active table rotation.
- Realtime word-by-word validation can produce immediate feedback.
- The hourglass can create pressure.
- Wrong words can be rejected while time remains.

It does not prove:

- Lobby readiness.
- Real player identity.
- Networked seating.
- Multiplayer authority.
- Production end-of-game presentation.
- Card or spell interference.
- Demon reactions.
- Campaign progression.

Treat anything beyond the local ritual as future work unless a specific document says it is implemented.

## Runtime Reality

The runtime is in a migration state.

`RitualController` is still the working prototype orchestration surface. It coordinates seat selection, book movement, hourglass timing, voice listening, incantation display, validation mode handling, retry behavior, and successful turn advancement.

`CoreRitualLoop` is the cleaner logic direction. It owns logic-only coordination between:

- `TurnManager`.
- `GrowingIncantationManager`.
- `PhraseValidator`.

`CoreRitualLoopBridge` adapts the cleaner core loop into the older `IncantationManager` display and event model while migration is incomplete.

Important implication:

Do not assume the presence of `CoreRitualLoop` means the old runtime has been replaced. The current safe approach is incremental migration, not a big rewrite.

## Important Code Map

### Ritual

- `Assets/Scripts/Ritual/RitualController.cs`
  - Current prototype orchestrator.
  - Selects occupied seats through `SeatManager`.
  - Moves the book.
  - Starts and stops the hourglass.
  - Starts and stops voice recognition.
  - Applies `WordByWordRealtime` or `FullPhrase` validation behavior.
  - Bridges into `CoreRitualLoopBridge` when available.

### Core Ritual Loop

- `Assets/Scripts/CoreRitualLoop/CoreRitualLoop.cs`
  - Logic-only direction for production ritual state.
  - Does not know about book movement, UI, timers, scenes, networking, or voice recognizer implementations.

- `Assets/Scripts/CoreRitualLoop/CoreRitualLoopBridge.cs`
  - Migration adapter between `CoreRitualLoop` and the legacy `IncantationManager` UI/event shape.
  - Uses reflection to mirror the core phrase into the legacy incantation list.
  - TODO in code: remove the legacy mirror after UI reads directly from `CoreRitualLoop`.

- `Assets/Scripts/CoreRitualLoop/TurnManager.cs`
  - Logic-only player index and rotation completion tracking.
  - Current implementation is count-based, not Seat-object-based.
  - Future production work must reconcile this with `SeatManager` physical seat ownership.

- `Assets/Scripts/CoreRitualLoop/GrowingIncantationManager.cs`
  - Owns the shared current ritual phrase for the core loop.
  - Resets to one word.
  - Unlocks exactly one word at a time.
  - Repeats configured vocabulary if more words are unlocked than unique configured words.

- `Assets/Scripts/CoreRitualLoop/PhraseValidator.cs`
  - Full-phrase validation helper.
  - Normalizes punctuation, whitespace, and casing.
  - Does not own voice recognition or turn advancement.

### Incantation And Display

- `Assets/Scripts/Incantation/IncantationManager.cs`
  - Legacy/current display-facing incantation model.
  - Tracks current words, completion state, phrase replay, correct word events, incorrect word events, and completed phrase events.
  - Still used by `RitualController` and display feedback.
  - Should not become the long-term owner of every ritual responsibility.

- `Assets/Scripts/Incantation/IncantationWordLibrary.cs`
  - Core ritual word source and speech alias source for recognition.
  - Separate from `SpellPhraseLibrary`.

- `Assets/Scripts/Incantation/IncantationTextDisplay.cs`
  - Visual phrase display and replay feedback surface.
  - It reacts to incantation state; it must not own phrase authority.

### Voice

- `Assets/Scripts/Voice/IVoiceRecognizer.cs`
  - Recognizer interface used by ritual systems.

- `Assets/Scripts/Voice/WindowsKeywordVoiceRecognizer.cs`
  - Current preferred realtime prototype recognizer.
  - Windows-only.
  - Builds keywords from `IncantationWordLibrary` words and speech aliases.
  - Emits recognized keywords as candidates.
  - Does not decide gameplay success.

- `Assets/Scripts/Voice/WhisperVoiceRecognizer.cs`
  - Full-recording Whisper recognizer path.
  - Emits one final transcript per completed listening session.
  - Includes timing and VAD guards for short and long phrases.
  - Kept for full-phrase or experimental paths.
  - Must not be forced as the only validation path.

- `Assets/Scripts/Voice/MockVoiceRecognizer.cs`
  - Useful for non-Windows or testing paths.

- `Assets/Scripts/Voice/VoicePhraseNormalizer.cs`
  - Normalizes speech and aliases.

### Seats

- `Assets/Scripts/Seats/SeatManager.cs`
  - Owns configured physical seat order and occupied-seat lookup.
  - Official clockwise order must be assigned in the Inspector as Seat1, Seat5, Seat3, Seat6, Seat2, Seat7, Seat4, Seat8.
  - Provides reverse order for counter-clockwise traversal.
  - Contains debug occupant support for local Play Mode.

- `Assets/Scripts/Seats/Seat.cs`
  - Logical seat.
  - Holds references such as `PlayerSpawn`, `BookTarget`, `BookGhost`, `LookTarget`, hands, and click zone.
  - `GetBookDestination()` currently prefers `BookGhost` when present, otherwise `BookTarget`.

- `Assets/Scripts/Seats/DebugSeatFlowSimulator.cs`
  - Local testing support.
  - Do not mistake this for production lobby or networking.

### Book

- `Assets/Scripts/Book/BookMover.cs`
  - Moves the single real book transform to a Seat destination over time.
  - Uses smooth interpolation.

- `Assets/Scripts/Book/BookController.cs`
  - Focused adapter around `BookMover`.
  - Raises movement and ritual accepted events.
  - Arrival is currently duration-based because `BookMover` does not expose a true arrival callback.

- `Assets/Scripts/Book/BookFeedbackController.cs`, `BookTextMagicEffect.cs`, and related book visual scripts
  - Visual or feedback support.
  - Must not choose traversal, mutate phrases, validate speech, or own turn results.

### Hourglass

- `Assets/Scripts/Hourglass/HourglassController.cs`
  - Timer pressure for active turns.
  - Should signal timeout pressure, not independently own traversal, phrase growth, or elimination rules.

### Player

- `Assets/Scripts/player/PlayerMovement.cs`
  - Despite the name, current behavior is seated mouse-driven head/neck/spine look motion.
  - Do not treat it as permission to add walking.

- `Assets/Scripts/player/HeadIdleMotion.cs`, `HeadEffect.cs`, `BodyMotion.cs`
  - Seated character motion and visual feel.

### Spell Hand Presentation

- `Assets/Scripts/SpellHand/SpellHandController.cs`
  - Owns exactly three authored card views and their hidden, table, raised, selected, and consumed visual states.
  - Uses only Inspector-assigned table, raised, and inspect poses.
  - Has no dependency on ritual, book, incantation, timer, seat, spell execution, or networking systems.
- `Assets/Scripts/SpellHand/SpellCardView.cs`
  - Presents one Inspector-assigned `SpellDefinition` through optional TMP text, Sprite artwork, a physical rarity Light, and Canvas references.
  - Supports runtime presentation reassignment through `SetDefinition(SpellDefinition)`.
  - Applies rarity color to its Inspector-assigned `GlowLight`; selection fades from current intensity to the configured selected intensity, while normal deselection and hand close fade to zero.
  - One replaceable transition coroutine prevents overlapping fades during rapid selection changes. Optional post-fade flicker uses deterministic layered sine waves.
  - Hiding, disabling, consuming, clearing the definition, or losing the Light reference cancels the transition and shuts the Light down immediately.
  - Remains visual-only and contains no spell execution or gameplay behavior.
- `Assets/Scripts/SpellHand/SpellDefinition.cs` and `SpellRarity.cs`
  - Store stable identity, player-facing text, presentation rarity, and optional artwork, glow, audio, and visual-prefab references.
  - Rarity colors are presentation defaults only. No probability, drawing, inventory, ownership, activation, or effect logic exists.
- The Spell Hand currently has no automatic game-flow connection. `ShowHand()`, `HideHand()`, `OpenHand()`, `CloseHand()`, `SelectCard(int)`, and `ConsumeSelectedCard()` are explicit presentation calls; optional debug keys exist only for Play Mode validation.

### Paused Or Legacy Areas

- `Assets/Scripts/Notebook`
  - Paused.

- `Assets/Scripts/Spells`
  - `SpellPhraseLibrary` and spell phrase data are separate from core ritual words.
  - Do not merge spell/card phrases into the ritual vocabulary.

- `Assets/Scripts/Voice/WhisperSandboxUI.cs`
  - Sandbox/reference area.
  - Do not rework during core ritual tasks unless explicitly requested.

## Scene Knowledge

The scene should be understood as layered systems:

- Room and ambience are visual environment only.
- Players are character instances or local debug occupants.
- Book contains the single real moving book.
- SeatSystem contains logical seats and physical order.
- Managers contain global orchestration and shared systems.
- Ritual controller objects bridge current prototype flow.
- Lighting and ambience support mood but do not own gameplay.

`BookGhost` is a visual/editor placement reference. It must never contain gameplay scripts.

The current code path may move the book toward `BookGhost` because `Seat.GetBookDestination()` prefers it. That does not make `BookGhost` gameplay authority.

## Known Migration Tensions

These are not bugs by themselves, but future developers must understand them.

### Seat order versus player count

`SeatManager` owns physical Seat traversal.

`TurnManager` currently tracks a count-based player index for the cleaner core loop.

Future work should connect the core loop to physical Seat objects without moving physical-order authority out of `SeatManager`.

### Legacy incantation display versus core phrase authority

`GrowingIncantationManager` is the intended core phrase authority.

`IncantationManager` is still used for current UI, replay feedback, events, and word-by-word validation state.

`CoreRitualLoopBridge` mirrors the core phrase into `IncantationManager` during migration. This is a bridge, not the final architecture.

### Success movement versus next turn movement

The prototype may move the book immediately after ritual acceptance and then skip redundant movement when the next turn begins if the book is already at the pre-moved seat.

This exists to keep the current prototype feeling responsive while orchestration is still coroutine-based.

### Failure and timeout

Retry and timeout behavior exists in prototype form.

Timeout failure now waits for the absorption, book aftermath, and Book Prison handoff chain before eliminating the failed Seat.

After `RitualController.CompleteCurrentFailedPlayerElimination()` runs, `SeatManager` frees the failed Seat and the ritual loop selects the next alive occupied Seat in physical table order.

Last-player-remaining presentation is still a TODO; when only one alive Seat remains, the ritual stops instead of starting another timed turn.

Do not assume current timeout behavior is final Last Priest Standing balance.

### Whisper timing work

Whisper contains several timing, VAD, cropping, and empty-transcript guards from previous experimentation.

This does not change the current default: `WordByWordRealtime` with `WindowsKeywordVoiceRecognizer`.

## Design Rules That Are Easy To Accidentally Break

- Do not create multiple gameplay books.
- Do not make characters walk around.
- Do not make the phrase grow after every player.
- Do not use player join order as table order.
- Do not use hierarchy order as table order.
- Do not put gameplay scripts on `BookGhost`.
- Do not let voice recognizers decide gameplay success.
- Do not let UI decide phrase authority.
- Do not merge `SpellPhraseLibrary` into ritual words.
- Do not force Whisper as the only validation path.
- Do not reintroduce Unity Dictation.
- Do not add Azure voice services.
- Do not add card gameplay, demon reactions, campaign objectives, or networking polish before the core loop is dependable unless a focused task explicitly scopes that work. The current Spell Hand data and presentation track does not activate spell gameplay.

## Current Manual Validation Expectations

For documentation-only tasks:

- Do not modify scenes, prefabs, assets, or gameplay code.
- Run a documentation diff check when possible.

For code tasks:

1. Open the relevant Unity scene.
2. Confirm compile succeeds.
3. Enter Play Mode.
4. Confirm local debug occupants can fill seats.
5. Confirm the single real book moves to an occupied active seat.
6. Confirm the book follows configured physical order, not seat numbering.
7. Confirm the phrase starts with one word.
8. Confirm all active players see the same phrase.
9. Confirm `WordByWordRealtime` accepts expected words immediately.
10. Confirm wrong words reject and allow retry while time remains.
11. Confirm `FullPhrase` remains available when explicitly selected.
12. Confirm the phrase grows only after a full active table rotation.
13. Confirm timeout behavior does not restart duplicate listeners, duplicate hourglasses, or duplicate book moves.
14. Confirm paused systems were not changed unless explicitly scoped.

## Next Development Shape

The next major milestone is lobby.

Recommended lobby sequence:

1. Add a local lobby model first.
2. Add ready state.
3. Decide minimum and maximum player counts for the prototype.
4. Assign ready lobby players to physical Seats through a clear seating service or setup step.
5. Hand occupied Seats into the existing ritual loop.
6. Preserve debug occupant testing until lobby seating is stable.
7. Only then start replacing debug occupants.
8. Only after that, add real networking flow.

Do not begin with production networking.

## Historical Decisions That Must Survive

The project intentionally narrowed scope to protect the playable vertical slice.

Preserve these decisions unless the source-of-truth docs are deliberately updated:

- Incantation is a party game first and a horror game second.
- The demon and lore are important atmosphere, but not the current core loop.
- The book is the main character.
- The hourglass is the pressure system.
- Voice is the primary interface.
- The table is the game space.
- The ritual should create stories, not just complete mechanics.
- The prototype should stay playable while systems migrate.
- New systems should earn their place by strengthening social chaos around the table.

## If You Are Unsure

Ask:

1. Does this change make the book, table, hourglass, voice, or shared phrase stronger?
2. Does this change create tension, laughter, betrayal, stress, surprise, or a story?
3. Does an existing system already own this responsibility?
4. Would this make the lobby milestone harder?
5. Can this be validated in Play Mode today?

If the answer is unclear, update documentation or ask for clarification before implementing.
