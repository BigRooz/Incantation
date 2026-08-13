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
- `Assets/Scenes/Bootstrap.unity` creates the persistent network manager and opens the pre-connection Book interface in `Assets/Scenes/MainGame.unity`.
- FishNet multiplayer is operational through Tugboat for local/LAN diagnostics. Steam transport and a production global Seal directory are not implemented.

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

It also proves:

- Persistent FishNet player identity.
- Server-authoritative networked Seat IDs.
- Independent character presentation.
- Book-driven LAN ritual creation and Seal joining.

It does not yet prove:

- Production lobby readiness synchronization.
- Steam identity, discovery, or transport.
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

### Lobby

- `Assets/Scripts/Networking/NetworkPlayer.cs`
  - Permanent FishNet `NetworkBehaviour` and single source of truth for one connected player's identity and shared state.
  - Exposes a server-generated session Player ID without reusing connection identity, the FishNet
    connection/owner, local-player status, active-player registry, replicated
    high-priest/name/lobby/ready/Seat/appearance values, server-only mutation APIs, and
    value-change events.
  - Contains no UI, Book, Seat, character presentation, ritual, voice, or gameplay behavior.
- `Assets/Scripts/Networking/NetworkCharacterPresentation.cs`
  - One-to-one presentation observer attached to every `NetworkPlayer` prefab.
  - Claims the existing scene character for the local owner, creates one independent visual instance for each remote player, and follows only its owner's synchronized `SeatId`.
  - Releases its previous Seat on Seat changes, removes only its own remote instance on disconnect, and disables remote cameras, audio listeners, and local look input.
  - Does not own network identity, Seat authority, lobby transitions, Book state, or ritual gameplay.
- `Assets/Scripts/Networking/ReadyState.cs`
  - Defines the independent `NotReady` and `Ready` network-player value. Lobby transition rules remain outside the data component.
- `Assets/Scripts/Lobby/LobbyPlayerState.cs`
  - Defines the UI-independent `NotSeated`, `Seated`, and `Ready` player states.
- `Assets/Scripts/Lobby/LobbyPlayerStateController.cs`
  - Owns one local lobby player's current state, guarded transitions, availability queries, and state-change notification.
  - Does not own Book presentation, physical Seat assignment, networking, or ritual flow.
  - Temporary migration boundary: the existing lobby still uses it, but future lobby networking must adapt it to `NetworkPlayer` instead of creating another player-state store.
- `Assets/Scripts/Menu/BookStateController.cs`
  - Observes `LobbyPlayerStateController.StateChanged` while presenting the lobby and centralizes the `NotSeated`, `Seated`, and `Ready` Book page mappings.
  - Reuses the existing Book text-transition and menu-item systems; it does not create a second lobby UI.
- `Assets/Scripts/Lobby/LobbyController.cs`
  - Coordinates physical lobby Seat selection and release with guarded `LobbyPlayerStateController` transitions.
  - A successful chair selection changes `NotSeated` to `Seated`; leaving releases the occupied Seat, restores the local player to the configured lobby waiting position, and returns `Seated` or `Ready` to `NotSeated`.

### Ritual

- `Assets/Scripts/Networking/NetworkRitualAuthority.cs`
  - Server-owned FishNet scene authority for the future multiplayer ritual state.
  - Owns the immutable multiplayer ritual roster copied from approved `NetworkPlayer` identity
    and Seat assignments in `SeatManager` physical traversal order.
  - Resolves roster candidates from FishNet's live authenticated server connections and each
    connection's owned objects. It requires exactly one initialized server `NetworkPlayer` per
    connection and never derives ritual identity from character presentation, join order,
    connection ID, or the process-global presentation registry.
  - Begins the authoritative runtime lifecycle as part of the first validated roster lock:
    ritual sequence zero advances to a real sequence and `Inactive` legally becomes `Preparing`.
    Later detailed phase progression remains on the temporary legacy bridge.
  - Is the sole multiplayer writer for active player and active Seat. Its one deterministic
    commit method traverses the authoritative roster and skips inactive or eliminated entries.
  - Is the sole multiplayer issuer of Book movement commands and the sole gameplay authority
    that validates and accepts Book arrival reports.
  - Replicates value-only ritual, roster, and arrival snapshots and publishes coalesced
    read-only events.
  - Owns ritual timer duration, start/stop, deadline, remaining-time calculation, and expiration.
    Accepted Book arrival starts it automatically; expiration commits its timeout source into the
    single turn-outcome and consequence chain.
  - Accepts recognized-text submissions only through a connection-owned `NetworkPlayer`, derives
    the player from the FishNet connection, validates the authoritative turn and timer window, and
    publishes immutable accepted-submission state.
  - Is the sole multiplayer phrase judge. It retrieves the server's current
    `IncantationManager` phrase, invokes the existing deterministic `PhraseValidator` rules for
    the selected mode, and publishes one immutable validation result per submission.
  - Is the sole multiplayer turn-result writer. A phrase-completing accepted validation or its
    own timer expiration may commit one immutable `TurnOutcomeSnapshot`; duplicate and stale
    sources are rejected.
  - Is the sole multiplayer consequence selector and writer. Each current turn outcome maps to
    exactly one immutable `RitualConsequenceSnapshot`; stale, duplicate, mismatched, or
    client-originated commits are rejected.
  - Lives on the existing `SharedBookNetworkAuthority` scene object but does not interpolate the
    Book, activate voice, start legacy turns, or execute consequence presentation.
- `Assets/Scripts/Ritual/RitualController.cs`
  - Current offline prototype orchestrator and network presentation/compatibility consumer.
  - Selects occupied seats through `SeatManager`.
  - Moves the book.
  - Starts and stops the hourglass.
  - Starts and stops voice recognition.
  - In network sessions forwards raw recognized text through the locally owned `NetworkPlayer`
    and applies only the authority's accepted/rejected validation result. Offline sessions retain
    direct evaluation and application through the same validator.
  - In network sessions never starts its legacy `RitualLoop`; Host and remote Client therefore
    cannot independently select Seats, move the Book, grow phrases, or advance turns.
  - Applies `WordByWordRealtime` or `FullPhrase` validation behavior.
  - Bridges into `CoreRitualLoopBridge` when available.
  - Its old unused `IsRecognizedPhraseValid` decision helper was retired after network validation
    moved to `NetworkRitualAuthority`; offline validation continues through the live
    `IncantationManager` paths.

### Multiplayer Ritual Single-Writer Boundary

The current boundary has one synchronized writer for each migrated decision:

- Roster, active participant, semantic Book command, accepted Book arrival, timer lifecycle,
  accepted voice submission, phrase validation, turn outcome, and consequence are written only
  by `NetworkRitualAuthority`.
- `NetworkPlayer` transports owner speech with FishNet-authenticated sender identity.
- `NetworkBookAuthority` executes accepted movement and reports arrival.
- `NetworkGameOverPresentationController` consumes only authoritative completed/game-over
  snapshots. It resolves the stable winner identity, shows local result UI, and lets only the
  server request the Book's distinct winner-presentation movement.
- `BookMover`, `HourglassController`, `Timer`, `IncantationManager`, and `RitualController` retain
  offline implementations and network presentation/compatibility responsibilities only.

`NetworkRitualAuthority.TryStartAuthoritativeRitual()` uses existing decisions to lock the
roster, initialize the one-word phrase from `GrowingIncantationManager`, select the first
participant, and issue the first Book command. Accepted Book arrival starts the authoritative
timer and changes the phase to `AwaitingRecitation`. A successful phrase outcome advances inside
the authority, resets turn-scoped arrival/timer/voice/validation state, selects the next entry in
physical roster order, and issues exactly one new Book command. Index wrap completes a rotation
and appends exactly one configured phrase word.

`RitualController` applies synchronized phrase snapshots and enables its local recognizer only
when `NetworkPlayer.LocalPlayer` is the owner whose `PlayerId` equals `ActivePlayerId` during
`AwaitingRecitation`. `NetworkPlayer` repeats the same identity/phase gate before creating a
submission. `IncantationManager.incantationLength` remains offline-only legacy random generation
length and never controls network phrase length.

After timeout presentation completes, `RitualController` forwards only the committed ritual,
turn, and consequence sequence values on the Host. `NetworkRitualAuthority` validates those
values against its current timeout outcome, derives the failed player from server state, and
mutates that fixed roster entry to inactive/dead. More than one survivor reuses the normal
physical next-participant and wrap path; one survivor commits `Completed`, game over, and the
winner's stable string Player ID. Remote callbacks clear presentation state only.

After that commit, REALIGN-004 presentation does not create another ritual turn. The Book
authority's winner movement has its own synchronized sequence and completion path, so it cannot
report gameplay arrival or start timer/voice/phrase/rotation behavior. The result Canvas is
created locally beneath the existing shared Book authority and is visible over both normal and
Death Cameras.

### Post-Game Return To Lobby

REALIGN-005 keeps the FishNet session and player identities alive between matches. The Host-owned
`NetworkPlayer` is the request transport; `NetworkRitualAuthority` is the only ritual reset
writer; `NetworkPostGameLifecycleController` is the peer-local presentation coordinator. A reset
clears the old dead roster rather than reviving its entries. The next Host start therefore locks
a fresh all-alive roster and advances the existing ritual sequence. Ready returns to NotReady,
while PlayerId, Circle membership, PriestName, and appearance persist. REALIGN-007.1 deliberately
supersedes Seat persistence: every Circle member returns to `SeatId = -1`, `NotReady`, and
`LobbyPlayerState.NotSeated`, then chooses a Seat again for the next match.
`BookMenuController.PresentConnectedLobbyAfterMatch` sends every connected Circle member,
including the Host, to the canonical `BookState.Lobby` page. The Host's existing Circle Back
action still calls `ReturnToActiveHostLobby` to reach `BookState.HostMenu`; post-game return does
not bypass the fresh pre-seat Character and Take My Seat flow.

The synchronized unassigned Seat reuses existing observers. `NetworkCharacterPresentation`
releases its occupied Seat and hides non-owner clones. `LobbyController` moves only the local
owner to its serialized `lobbyWaitingPosition` through `SeatManager`'s focused presentation
helper, keeping that owner active for pre-seat Character/customization view. No broad Seat clear
is performed, so offline/debug occupancy remains separate.

Death restoration must call both `BookPrisonSpectatorController.ResetSpectatorView` and
`PlayerAbsorptionController.ResetAbsorption`. The former also restores the exact enabled states
captured for Cameras, AudioListeners, and PlayerMovement. Book lobby restoration is a separate
pose reset and never enters the gameplay movement/arrival pipeline.

### Network Character Look Pose

`PlayerMovement` remains enabled only for the locally owned presentation and remains the sole
reader of mouse input. Its pitch/yaw values and input-free procedural bone application are reused
by `NetworkCharacterLookPose` on the `NetworkPlayer` prefab. The owner sends two floats through
FishNet; the server validates/clamps and buffers the latest observer pose. Remote
`PlayerMovement` components stay disabled, but their shared pose method may be called directly
after `NetworkCharacterPresentation` binds or recreates the visual instance. Look state is
connection-scoped cosmetic presentation, not ritual or alive/dead state.

The local living winner keeps locked relative mouse-look while the game-over overlay is visible;
the Host's existing Return button is selected for normal UI submit input. Return to Lobby, rather
than `Completed`, is the neutral-pose boundary. `PlayerMovement.ResetLookPose` clears accumulated
pitch/yaw and can restore cached authored bone rotations immediately even while disabled.
`NetworkCharacterLookPose.ResetPoseForLobby` then sends a reliable zero through the existing
buffered observer RPC before lobby input is disabled, preventing Match 1 pose state from returning
when hidden remote presentations become visible in Match 2.

### Local Lobby Versus Shared Ritual Book

There is still one visible scene `BookModel` per process. `NetworkBookAuthority` explicitly leaves
that presentation under local ownership during menu, Circle, seated lobby, Character view, and
Return to Lobby. In local mode its `LateUpdate` copies neither the Host presentation into the
proxy nor the proxy into a Client presentation. Before ritual authorization the Host reconciles
the visible Book and proxy to the authored lobby pose; every authorized peer then enters the same
shared mode before local ritual presentation begins. Existing Book commands, NetworkTransform
replication, arrival reports, winner presentation, and server lobby-pose reset remain unchanged.

The Character action is one canonical local transition: it captures the originating Book state
once, enters `CharacterMenu`, rotates the local Book, and moves the existing menu camera. The old
Show Character callback aliases that transition for serialized compatibility. Seated Circle UI
also exposes Character without changing the authoritative Seat.

The detailed method-level ownership table and the rationale for every retained bridge live in
`Docs/TechnicalArchitecture.md`.

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
  - Its duration-based arrival remains a temporary legacy gameplay callback.

- `Assets/Scripts/Networking/NetworkBookAuthority.cs`
  - Owns the invisible FishNet transform proxy for the one persistent visible Book.
  - Aligns the proxy to `BookModel` in `Awake`, before FishNet scene-object initialization, so a
    joining client never receives the proxy's serialized origin as the visible Book pose.
  - Copies pose only: it never copies proxy activation into `BookModel` or creates another Book.

- `Assets/Scripts/Book/BookFeedbackController.cs`, `BookTextMagicEffect.cs`, and related book visual scripts
  - Visual or feedback support.
  - Must not choose traversal, mutate phrases, validate speech, or own turn results.

### Hourglass

- `Assets/Scripts/Hourglass/HourglassController.cs`
  - Offline timer controller and network compatibility bridge.
  - In network sessions it consumes `NetworkRitualAuthority` timer snapshots, forwards
    server-side stop requests, and never starts or expires gameplay time.

- `Assets/Scripts/Core/Timer.cs`
  - Owns the local countdown only in offline mode.
  - In network sessions it presents authoritative remaining time and translates synchronized
    lifecycle changes for existing visuals and temporary legacy callbacks without deciding
    expiration.
  - Retains authoritative `TimerSequence` and `IsExpired` presentation state. Hourglass visuals
    render that durable state on enable, force terminal sand at expiration, reset from the full
    authoritative duration for a new sequence, and render the exact synchronized remainder when
    a successful turn stops the timer.
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

During a network timeout, the authoritative consequence's stable player ID is retained through
that presentation chain. Book Prison camera ownership is resolved by exact
`PlayerId -> NetworkPlayer` matching and requires `NetworkPlayer.IsOwner`; transform ancestry is
diagnostic only. This keeps shared remote death presentation intact while only the eliminated
player's client enters its prison camera. Offline rituals retain the original local camera behavior.

Camera ownership applies to indirect transform movement as well as `Camera.enabled`. The active
gameplay camera comes from the local character prefab. Because absorption and the Book Prison
handoff move the failed character root, remote elimination presentation snapshots and restores
active camera world poses at both movement boundaries. This prevents an ancestor transform from
dragging a survivor's viewpoint while preserving the remote character's physical presentation.

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
