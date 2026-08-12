# Inspector Reference

This document centralizes recommended Inspector setup for the current v0.1 prototype.

Purpose: preserve current Inspector values and scene-reference expectations that matter to the prototype.

Questions answered here:

- Which Inspector references are intentional?
- Which prototype tuning values should be preserved?
- Which scene setup assumptions affect Play Mode validation?

This document does not contain gameplay law, task planning, code architecture, or permission to modify scenes, prefabs, assets, or scripts.

Read next: `Docs/UNITY_SCENE_SETUP.md` for scene expectations and `Docs/PROJECT_STATUS.md` for current prototype state.

It is documentation only. Do not treat it as permission to modify scenes, prefabs, assets, or scripts.

Values marked `Prototype tuning` are current or recommended prototype values that may change after playtesting.

## Scene

- Current playable scene: `Assets/Scenes/MainGame.unity`.
- Scene purpose: local lobby foundation plus seated ritual prototype.
- Production networking and ready-based seating are not implemented yet.
- Lobby seat selection is local/prototype seating only. It reuses `Seat`, `SeatManager`, and `ChairClick`; it is not a second gameplay seating system.

## LobbyController

Current `MainGame` setup:

- Add `LobbyController` to the existing `Managers` GameObject.
- `ritualController`: assign the scene `RitualController`.
- `seatManager`: assign the scene `SeatManager`.
- `localLobbyPlayer`: assign the existing visible local player prefab root, not a ghost, marker, preview object, or parent container. This is the real lobby character whose cosmetics/skins should be visible before ritual start. `LobbyController` does not search for a tagged Player fallback.
- `localPlayerCamera`: assign the Camera that should become active when Start Ritual is pressed. This is an explicit Inspector reference; `LobbyController` does not search under the local player hierarchy or the scene. Its GameObject may be inactive before ritual start.
- `cameraTransitionManager`: assign the scene `CameraTransitionManager` that owns and controls `MenuTransitionCamera`. Its `activeCamera` must reference the one rendering menu camera.
- `lobbyCameraTarget`: assign the authored Lobby viewpoint Transform. This may be the existing `LobbyCamera` Transform, but that camera must be treated as a static destination reference only and must not render during menu navigation.
- `bookMenuReturnInteractable`: assign the `BookMenuReturnInteractable` on the existing physical Book. This explicit reference lets lobby state own whether menu-only Book hover and click interaction is available; no scene lookup is performed.
- `lobbyCanvasRoot`: optional. Leave empty to let `LobbyController` create a minimal runtime `LobbyCanvas`.
- `startRitualButton`: optional when runtime UI creation is enabled. If a hand-authored `LobbyCanvas` is added later, assign the Start Ritual button here.
- `optionsButton`: optional when runtime UI creation is enabled. Current foundation only logs that Options are not implemented yet.
- `quitGameButton`: optional when runtime UI creation is enabled.
- `createLobbyUiIfMissing`: `true` for the current local lobby foundation.
- `autoDisablePlayerMovementDuringLobby`: `true` so local seated mouse-look scripts do not consume UI mouse input while the lobby is open.
- `lockCursorWhenRitualStarts`: `true` to restore the existing locked/hidden gameplay cursor behavior after Start Ritual.
- `gameplayInputBehaviours`: optional manual list of gameplay input behaviours to disable during lobby. Leave empty to let `LobbyController` automatically include `PlayerMovement` instances.

Runtime behavior:

- On scene start, `LobbyController` shows the lobby and keeps the game in `Lobby` state.
- If no lobby UI is assigned, it creates a simple `LobbyCanvas` with title `Incantation` and buttons `Start Ritual`, `Options`, and `Quit Game`.
- While in lobby, the cursor is forced visible and unlocked, and configured gameplay input behaviours are disabled.
- Entering the Circle Book page does not move the camera or enable Seat interaction. `LobbyController.BeginSeatSelection()` runs only from `Take My Seat`: it enables the Book return interaction, enables Seat selection, and requests `cameraTransitionManager.MoveTo(lobbyCameraTarget)`. It does not enable or disable `BookMenuCamera`, `LobbyCamera`, or other menu destination cameras.
- While in lobby, chair clicks are routed through `LobbyController.TrySelectLobbySeat(seat)`, which asks `SeatManager.TryLobbySit(...)` to seat or move `localLobbyPlayer`.
- `SeatManager.TryLobbySit(...)` frees the previous lobby Seat for that same player, keeps the real `localLobbyPlayer` active, moves it to `selectedSeat.playerSpawn.position`, rotates it to `selectedSeat.playerSpawn.rotation`, and occupies the selected Seat. No lobby ghost or duplicate player prefab is created.
- The selected lobby Seat is stored as normal Seat occupancy: `SeatManager.GetLobbySeatForPlayer(localLobbyPlayer)` returns the chosen Seat because `SeatManager.TryLobbySit(...)` occupies that Seat with the real local player.
- `Start Ritual` requires `SeatManager.GetLobbySeatForPlayer(localLobbyPlayer)` to return a selected Seat. Without one, it logs `Cannot start ritual: the local player has not selected a seat.` and leaves menu interaction, text, cameras, lobby state, and the ritual unchanged.
- With a selected Seat, `Start Ritual` keeps using the real local player already occupying that Seat, reapplies that player root to `selectedSeat.playerSpawn`, immediately calls `bookMenuReturnInteractable.DisableInteraction()`, disables lobby seat selection through `SeatManager.SetLobbySeatSelectionEnabled(false)`, hides the lobby canvas, restores disabled gameplay input behaviours, calls `cameraTransitionManager.DisableRendering()`, activates the assigned `localPlayerCamera` GameObject, enables that Camera, asks `RitualController` to prefer that selected Seat as the first active ritual Seat, optionally locks/hides the cursor for gameplay, and calls `RitualController.StartRitual()`.
- `CameraTransitionManager.ActiveCamera`, `EnableRendering()`, and `DisableRendering()` keep menu-camera ownership inside the transition manager. `LobbyController` never searches for or directly identifies `MenuTransitionCamera`.
- The lobby does not generate incantations, start the hourglass, move the book, duplicate ritual initialization, change elimination logic, or create a separate seating system.
- `Options` is a placeholder button for this foundation task only.
- If `localPlayerCamera` is not assigned, `Start Ritual` logs `LobbyController localPlayerCamera is not assigned. Ritual will continue but camera switching will be skipped.` and still starts the ritual.
- `HasSelectedLobbySeat()` is the read-only seat requirement query used by the Living Book and the lobby start flow. It reuses `SeatManager.GetLobbySeatForPlayer(localLobbyPlayer)` and does not duplicate seat ownership.

## SeatManager

Intentional setup:

- `clockwisePhysicalSeatOrder`: assign exactly 8 unique Seat references in this order:
  1. Seat1
  2. Seat5
  3. Seat3
  4. Seat6
  5. Seat2
  6. Seat7
  7. Seat4
  8. Seat8
- Counter-clockwise traversal is the exact reverse.
- `bookMover`: assign the single real `BookMover` on `BookModel`.
- `currentBookSeat`: runtime state; do not author gameplay logic around a fixed initial value.
- `lobbySeatSelectionEnabled`: runtime lobby gate. `LobbyController` enables it when showing the lobby and disables it before starting the ritual.
- `localLobbyPlayer`: runtime/local prototype player used by `TryLobbySit(seat)` when no explicit player argument is supplied. Prefer assigning this through `LobbyController.localLobbyPlayer`, and make sure it is the real visible player prefab root.
- `showLobbyOccupiedMarkers`: `true` for lightweight runtime feedback that a chair is occupied in the lobby.
- `lobbyOccupiedMarkerColor`, `lobbyOccupiedMarkerSize`, and `lobbyOccupiedMarkerOffset`: prototype-only runtime marker tuning. These markers are created in Play Mode and are not final VFX.
- `allowMultipleDebugOccupants`: `false` unless intentionally testing multiple local debug seats.
- `enableDebugLogs`: `false` by default.

Notes:

- The configured physical order is authoritative.
- Do not rely on hierarchy order, seat numbering, player join order, or network index.
- Lobby seat selection methods:
  - `TryLobbySit(seat, player)` seats the real local player in an available Seat, freeing that player's previous lobby Seat first, keeping the player active, and moving/rotating that same GameObject to `Seat.playerSpawn`.
  - `TryLobbySit(seat)` uses the assigned `localLobbyPlayer`.
  - `LeaveLobbySeat(player)` frees the Seat currently occupied by that player.
  - `IsLobbySeatAvailable(seat)` returns true only when lobby selection is enabled and the Seat is empty and not eliminated.
  - `SetLobbySeatSelectionEnabled(false)` locks chair selection for the ritual handoff.
- Occupied lobby chairs cannot be selected by another player. Clicking another empty chair moves the local player and frees the previous Seat.
- Existing ritual seating continues to read occupied Seats from `SeatManager.GetOccupiedSeats()`.

## Seat

Each Seat is logical. Chair meshes are visual.

Intentional references:

- `PlayerSpawn`: where a seated local/debug player is placed.
- `realPlayerTransform`: runtime-bound visible player root/model for this Seat. `Seat.Occupy()` assigns this automatically from the occupying player GameObject; normal gameplay should not require manual assignment.
- `BookTarget`: gameplay destination when used by the current setup.
- `BookGhost`: visual/editor placement reference.
- `LookTarget`: seated attention reference.
- `LeftHand` and `RightHand`: seated hand references.
- `ChairClickZone`: seat interaction/debug selection.

Notes:

- `Seat.GetBookDestination()` currently prefers `BookGhost` when present, otherwise `BookTarget`.
- `Seat.Occupy(player)` assigns both `currentPlayer` and `realPlayerTransform` for real visible player objects.
- `Seat.Free()` clears both `currentPlayer` and `realPlayerTransform`.
- `Seat.GetRealPlayerTransform()` returns the automatically bound `realPlayerTransform` when valid. If that is empty, it can fall back to `currentPlayer.transform` only for non-debug occupants with visible renderers.
- Runtime debug occupants named `DebugOccupant_*` or `SimulatedPlayer_*` are local testing placeholders, not visible player models for absorption.
- For absorption to work, the active Seat must be occupied through `Seat.Occupy()` with the actual visible player root or a player root that contains visible renderers.
- `BookGhost` must never contain gameplay scripts.

## ChairClick

Intentional setup:

- Place `ChairClick` on the chair click-zone object and assign its `seat` reference to the owning logical `Seat`.
- During `Lobby`, `ChairClick` only reports the clicked Seat to `LobbyController.TrySelectLobbySeat(seat)`.
- Outside `Lobby`, `ChairClick` keeps the existing debug/testing path by calling `SeatManager.TrySit(seat)`.

Notes:

- Do not put lobby seating rules inside `ChairClick`.
- `ChairClick` should stay an interaction relay; `SeatManager` owns whether a Seat can be occupied, freed, or rejected.

## Lobby Seat Setup Validator

Menu tools:

- `Tools/Incantation/Validate Lobby Seat Setup`
- `Tools/Incantation/Repair Lobby Seat Setup`

Required lobby seat setup:

- The open scene should contain one `LobbyController` and one `SeatManager`.
- `LobbyController.seatManager` should reference the scene `SeatManager`.
- A local lobby player must be assigned before Play Mode seating can work. Assign `LobbyController.localLobbyPlayer` to the real visible player prefab root; `LobbyController` forwards that reference to `SeatManager` for lobby seating.
- Assign `LobbyController.localPlayerCamera` to the Camera that should be enabled after Start Ritual. Assign `LobbyController.cameraTransitionManager` to the menu transition manager and `LobbyController.lobbyCameraTarget` to the authored Lobby viewpoint Transform.
- Every `Seat` must have `PlayerSpawn` assigned. Lobby seating uses this Transform to place the local player when a chair is selected.
- Every selectable chair click-zone should have `ChairClick.seat` assigned to its owning logical `Seat`.
- `SeatManager.showLobbyOccupiedMarkers` may remain enabled for the current lightweight occupied-chair feedback. Per-seat player ghost or preview objects are optional and are not required for the current prototype.

Expected Seat child naming:

- If `Seat.playerSpawn` is empty, `Repair Lobby Seat Setup` only auto-assigns direct child Transforms with these exact names:
  - `SeatPoint`
  - `SitPoint`
  - `PlayerSeatPoint`
  - `SpawnPoint`
- If none of those direct children exist, repair creates a direct child named `LobbySeatPoint` under that `Seat`, places it at the Seat transform position, and assigns it to `Seat.playerSpawn`.
- Repair does not guess random scene objects, create final art, duplicate player prefabs, or modify ritual gameplay flow.

Validation behavior:

- Validate reports whether `LobbyController` and `SeatManager` exist.
- Validate reports whether a local lobby player can be resolved for the current setup.
- Validate lists every `Seat`, its spawn assignment, optional occupied visual/ghost status, linked `ChairClick`, and whether it can be used for lobby seating.
- Validate lists every `ChairClick` and whether it is linked to a `Seat`.
- Validate ends with an explicit `PASS` or `FAIL` and concrete repair or assignment reasons.
- After repair, run validate again before Play Mode. A ready setup should pass, and lobby clicks should move the local player between assigned Seat points.

## RitualController

Current `MainGame` setup:

- `seatManager`: assigned.
- `bookMover`: assigned to the single real book mover.
- `bookController`: assigned when present on the book.
- `hourglassController`: assigned.
- `incantationManager`: assigned.
- `incantationTextDisplay`: assigned.
- `coreRitualLoopBridge`: assigned when available.
- `demonHandController`: assign the `DemonHandController` under the single `BookModel` so ritual failure can trigger the demon hand visual sequence.
- `voiceRecognizerBehaviour`: assigned to `WindowsKeywordVoiceRecognizer` for the default prototype feel.
- `voiceValidationMode`: `WordByWordRealtime`.
- `voicePhraseNormalizer`: assigned.
- `speechAliasWordLibrary`: assigned to the ritual word library.
- `autoStart`: `false` in `MainGame` so the local lobby appears first. Use `LobbyController.StartLobbyRitual()` or the lobby Start Ritual button to call the existing ritual start flow.
- `hourglassDuration`: `90` seconds in `MainGame`. This is the single configured ritual turn
  duration used by both offline play and the server-authoritative network deadline.
- `ritualAcceptancePauseSeconds`: `0.75` seconds. `Prototype tuning`.
- `enableLearningMode`: `false` unless intentionally collecting speech aliases.
- `debugAbsorptionPlayerOverride`: temporary local/debug absorption testing only. Leave empty for normal gameplay and multiplayer; assign a visible player root here only when testing absorption with debug-only Seat occupants.
- `enableDebugLogs`: `false` by default.

Notes:

- `WordByWordRealtime` is the default prototype mode.
- `FullPhrase` is optional strict mode.
- `LobbyController` may call `RitualController.SetPreferredStartingSeat(selectedSeat)` during Start Ritual so the first ritual turn and book movement begin from the seat selected in lobby instead of the first occupied Seat in physical order.
- `debugAbsorptionPlayerOverride` is used only if the active Seat cannot provide a real player Transform. A real Seat-bound player is always preferred over this override.
- Do not assign Unity Dictation or Azure voice services.

## VoiceAmplitudeProvider

Component placement:

- Add one `VoiceAmplitudeProvider` to the local player root (or a dedicated enabled child that shares the local player's lobby lifetime).
- Keep the component enabled from local lobby entry onward. With `startOnEnable` enabled, microphone amplitude capture starts immediately and does not wait for the ritual.
- Do not add this provider to remote player characters. Do not create another microphone amplitude session for each consumer.

Voice Settings:

- `inputDevice`: optional preferred microphone name. Leave empty to use Unity's default microphone. `SetInputDevice(...)` is the runtime hook for future Voice Settings UI.
- `sampleRate`: `16000` by default. The provider warns and does not start when the value is invalid or unsupported by the selected device.
- `sampleWindow`: `256` by default. Smaller windows react faster but fluctuate more; larger windows are steadier but add response latency. The sample buffer is reused and is not allocated every frame.
- `silenceThreshold`: `0.02` by default. RMS input at or below this level is exposed as `0`, which prevents room noise from continuously driving character responses. `Prototype tuning`.
- `smoothingSpeed`: `15` by default. Higher values track changes faster; lower values soften movement. Smoothing uses unscaled frame time. `Prototype tuning`.
- `startOnEnable`: keep `true` for the local lobby player so recording begins at lobby entry.

Runtime behavior and ownership:

- `CurrentAmplitude` is the continuous smoothed normalized voice level from `0` to `1`.
- `IsRecording` reports whether this provider owns an active microphone recording session.
- `CurrentDevice` reports the microphone actually being recorded. `SelectedInputDevice` reports the preferred Inspector/runtime selection.
- `RefreshDevices()` refreshes the cached device list. If the active microphone disappeared, recording stops safely; a later `RefreshDevices()` automatically retries a previously requested recording after a device becomes available.
- `VoiceLipController` consumes `CurrentAmplitude` from this provider as its only automatic amplitude source and does not read microphone samples itself.
- Future Voice Chat may share the selected-device policy and amplitude reading, but networking and audio transport remain separate responsibilities.
- Future Voice Settings should call `RefreshDevices()` and `SetInputDevice(...)`; it must not create a competing microphone amplitude recorder.
- Future VU meters and character reactions should read `CurrentAmplitude` and must not access `Microphone` directly.
- This provider does not recognize speech, validate ritual phrases, integrate Whisper, use `WindowsKeywordRecognizer`, or implement voice chat networking.

## BookMover

Current `MainGame` setup:

- `moveDuration`: `3` seconds. `Prototype tuning`.
- `enableDebugLogs`: `false`.

Notes:

- `BookMover` moves the one real book transform.
- In a FishNet session it delegates to `NetworkBookAuthority`. Only the server starts the
  movement coroutine; clients receive the authoritative transform.
- Offline Play Mode keeps the existing local movement path when the Book scene object is not
  network-initialized.
- It should not choose traversal, validate phrases, or own turn rules.

## NetworkBookAuthority

Required persistent presentation setup:

- `BookModel` remains the one always-active visible presentation object.
- `BookModel` contains the existing `BookMover` and presentation components.
- Do not put `NetworkObject` or `NetworkTransform` on `BookModel`; FishNet deactivates unstarted
  scene network objects, which would hide the menu Book before Host/client startup.
- Do not create an offline Book, network Book visual, or per-player Book.

Required `SharedBookNetworkAuthority` scene-object setup:

- One separate, invisible root GameObject named `SharedBookNetworkAuthority`.
- One FishNet `NetworkObject` with no client owner, configured as a `MainGame` scene object.
- One FishNet `NetworkTransform` on the proxy GameObject.
- `NetworkTransform` client authority: disabled.
- `NetworkTransform` synchronize position: enabled.
- `NetworkTransform` synchronize rotation: enabled.
- `NetworkTransform` synchronize scale: disabled.
- `NetworkTransform` interpolation: `3`.
- `NetworkTransform` extrapolation: disabled.
- One `NetworkBookAuthority` on the proxy GameObject.
- `bookMover`: assign the existing `BookMover` on the persistent `BookModel`.
- `presentationTransform`: assign the existing visible `BookModel` Transform.
- One `NetworkRitualAuthority` on the same proxy GameObject.
- `seatManager`: assign the existing `SeatManager` on the `SeatSystem` scene object. This is a
  local source registry only; no `Seat` or scene reference is synchronized.
- REALIGN-004 presentation components are installed at runtime by `NetworkBookAuthority` on this
  same proxy object. `NetworkGameOverPresentationController` consumes its sibling authorities;
  `RitualGameOverPresenter` creates the local screen-space `GameOverCanvas/GameOverPanel/ResultText`
  hierarchy. No additional authored scene references are required.
- `NetworkPostGameLifecycleController` is also installed at runtime on this proxy. It consumes
  sibling authority state and resolves existing local lobby/death presentation components only
  when a completed ritual synchronizes back to `Inactive`; no new scene references are required.
- `NetworkRitualAuthority` owns semantic Book movement requests and authoritative arrival
  acceptance, but does not interpolate the Book or start existing ritual gameplay.

Runtime ownership:

- Offline: FishNet may deactivate only the invisible proxy. `BookModel` remains visible,
  interactive, and locally usable.
- The Host/server is the only Book simulation and destination authority.
- Host: `NetworkBookAuthority` copies `BookModel` position/rotation into the network proxy.
- Client: `NetworkBookAuthority` applies the authoritative proxy position/rotation to its
  existing `BookModel`; it never creates another presentation.
- The proxy aligns to `BookModel` in `Awake`, before FishNet captures initial scene-object
  transform state. Keep the serialized references assigned; do not rely on the proxy's origin
  transform as the initial Book pose.
- `TargetSeatId` is replicated from `SeatManager`'s stable configured physical-order ID.
- `MovementSequence` identifies every authoritative move, including consecutive redirects, and
  `IsMoving` synchronizes the server-owned start/arrival lifecycle.
- Winner presentation uses a separate `PresentationMovementSequence` plus presented ritual
  session/sequence and winner identity. Its completion never reports gameplay Book arrival.
- The visible Book's authored initial position/rotation is captured in `NetworkBookAuthority.Awake`
  as the lobby pose. Host return cancels movement and restores that pose through the existing
  server-owned proxy; do not add a fake lobby Seat or gameplay command.
- Clients do not call a movement coroutine, choose a Seat, or write the transform.
- `BookPresentationState` synchronizes `Closed`/`Open`; presentation consumers subscribe to
  `PresentationStateChanged`.
- Future demon-hand, page-turn, mood, ritual-reaction, and VFX state belongs on or behind this
  same authoritative Book boundary. Do not add local gameplay state to `BookGhost` or create
  another physical/network Book.
- Disconnect: the proxy may deactivate with FishNet, but `BookModel` stays active so normal
  local Book navigation can resume without an orphaned or duplicate object.

`NetworkRitualAuthority` replicates a read-only ritual snapshot and a locked ritual roster. The
server-only `Build Authoritative Ritual Roster` Context Menu action validates current Circle
members and their authoritative `NetworkPlayer.SeatId` assignments, then copies them in
`SeatManager` physical traversal order. Existing ritual systems do not consume the roster yet.
After a roster is locked, the server-only `Commit Next Active Participant` action advances
circularly through that immutable order, skipping entries that are inactive or not alive.
It changes only synchronized active-player, active-Seat, and turn-sequence values.
Accepted Book commands are executed by `NetworkBookAuthority`, which reports duration-based
completion to `NetworkRitualAuthority`. The authority validates the movement, ritual, turn, and
Seat identifiers before publishing its immutable arrival state and read-only notification.
Accepted arrival starts the timer automatically using the assigned `RitualController`'s
`hourglassDuration`. `NetworkRitualAuthority` owns the resulting deadline; it does not expose a
second duration setting. Clients render from the synchronized start/deadline snapshot and cannot
expire or stop gameplay time.
The separate deterministic snapshot action remains development-only and is never automatic.

## BookController

Current setup:

- `bookMover`: assigned to the same object's `BookMover`.

Notes:

- `BookController` is an adapter around `BookMover`.
- Its legacy arrival callback remains duration-based using `BookMover.moveDuration`.
- Future improvement: replace duration-based arrival with a true movement completion callback.

## Ritual Timer Compatibility

- Offline: `HourglassController` and `Timer` retain the existing local countdown.
- Network server: `NetworkRitualAuthority` owns start, stop, duration, deadline, and expiration.
- Network client: `HourglassController` mirrors read-only snapshots into `Timer`.
- Existing visual, warning-audio, lighting, and temporary ritual callbacks may continue reading
  `Timer`; they do not decide authoritative expiration.

## DemonHandController

Recommended setup:

- Exact correct `MainGame` hierarchy:
  - `Book`
  - `BookModel`
  - `DemonHandAttackV1`
  - `DemonHandModel`
- The only gameplay-driving demon hand setup must live under `Book > BookModel > DemonHandAttackV1`.
- `BookGhost` objects may exist as visual/editor placement references, but they must not contain gameplay-driving `DemonHandController` components.
- Use `Tools/Incantation/Validate Demon Hand Setup` to inspect all `DemonHandController` components in the open scene, including whether they are under `BookModel`, whether their references are assigned, whether their Animator has a controller and avatar, whether `Attack` and `Reset` triggers exist, and whether any controllers are under `BookGhost`.
- Use `Tools/Incantation/Repair Demon Hand Setup` to repair the expected `BookModel` setup automatically. The repair assigns `DemonHandModel` as `handRoot`, assigns the Animator on `DemonHandAttackV1`, restores the expected Animator Controller and imported avatar, sets safe visibility/debug values, removes `DemonHandController` components from `BookGhost` children only, and marks the scene dirty.
- Place `DemonHandController` on an active object under `BookModel` that can remain enabled while `handRoot` is hidden.
- The preferred setup is to keep the controller on the same GameObject as the hand Animator only when `handRoot` is a child visual root, not the controller's own GameObject.
- `handRoot`: assign the demon hand visual root GameObject that should be hidden at scene start and shown during the sequence.
- `handAnimator`: assign the Animator that plays the demon hand attack animation.
- `attackTriggerName`: keep `Attack` unless the Animator Controller uses a different trigger parameter.
- `resetTriggerName`: keep `Reset` unless the Animator Controller uses a different trigger parameter.
- `hideOnAwake`: keep `true` so the hand does not appear immediately at scene start.
- `deactivateWhenIdle`: keep `true` unless the hand must remain active after the animation for visual debugging.
- `fallbackSequenceDuration`: set to the expected attack animation length when Animation Events are not yet configured.
- `playOnStartForDebug`: keep `false` except when intentionally testing the hand in Play Mode.

Animator Controller:

- Use `Assets/assets/Models/DemonHand/DemonHandController.controller` on the demon hand Animator unless the asset is intentionally replaced.
- Required trigger parameters:
  - `Attack`
  - `Reset`
- State machine:
  - `Entry` -> `Idle`.
  - `Idle` is the default state and has no motion.
  - `Idle` -> `Attack` uses the `Attack` trigger.
  - `Idle` -> `Attack` has `Has Exit Time` off and `Transition Duration` set to `0`.
  - `Attack` uses the imported demon hand attack animation clip.
  - `Attack` -> `Idle` has `Has Exit Time` on, `Exit Time` set to `1`, and `Transition Duration` set to `0`.

Animation Events:

- Add an Animation Event at the exact grab/contact frame that calls `AnimationEvent_GrabMoment`.
- Add an Animation Event on the last frame that calls `AnimationEvent_SequenceFinished`.
- Animation Events are delivered to components on the animated Animator GameObject. If `DemonHandController` is not on that GameObject, add a relay component later or move the controller to the Animator object while keeping `handRoot` as a child visual root.

Notes:

- This controller is visual-only.
- It does not eliminate, move, damage, absorb, or otherwise mutate player gameplay state.
- Future elimination or absorption systems should call `PlaySequence()` and listen to `onSequenceStarted`, `onGrabMoment`, and `onSequenceFinished`.
- If the animation events are missing, `fallbackSequenceDuration` finishes the sequence safely and hides `handRoot` when `deactivateWhenIdle` is enabled.
- The Animator must support trigger parameters named by `attackTriggerName` and `resetTriggerName`; missing triggers are logged once and skipped safely.
- Do not put this controller on `BookGhost`.

## PlayerAbsorptionController

Recommended setup:

- Place `PlayerAbsorptionController` on a real book-side scene object, preferably under `Book > BookModel` or on a nearby book visual/controller object.
- Do not place it on `BookGhost`.
- `absorptionTarget`: assign a small empty Transform near or slightly inside the visible `BookModel`, positioned where the failed player should be pulled into the book.
- `absorptionDuration`: `0.75` seconds by default. `Prototype tuning`.
- `movementCurve`: tune the pull-in motion. Default should move from 0 to 1 over the sequence.
- `scaleCurve`: tune shrink amount. Default should evaluate from 1 to 0 so the target scales down into the book.
- `deactivateTargetOnComplete`: keep `true` for the prototype so the absorbed player disappears visually at the end.
- Debug:
  - `skipPlayerHide`: development only. Prevents the absorbed player from being hidden/deactivated after the absorption sequence so local tuning can hear audio, inspect particles, and verify timings. Keep disabled during normal gameplay.
  - `skipPlayerDeactivate`: development only. Prevents the absorbed player from being deactivated after the absorption sequence while preserving the rest of the death sequence. Keep disabled during normal gameplay.
- `onAbsorptionStarted`: optional visual/audio-only hooks.
- `onAbsorptionFinished`: optional visual/audio-only hooks.

Notes:

- This controller is visual-only.
- It moves and scales the assigned target Transform during absorption, then optionally deactivates that target GameObject.
- The Debug section is only for local tuning of death audio, smoke particles, aftermath timing, and death vision timing. It must remain disabled during normal gameplay.
- It does not decide ritual failure, timeout, elimination, seating, voice validation, turn order, or book traversal.
- `ResetAbsorption()` restores the absorbed target to its original position, rotation, scale, and active state for Play Mode testing.
- If `BeginAbsorption` receives a null target, the controller logs one warning and the game continues.

## DeathVisionVignetteController

Recommended setup:

- Place `DeathVisionVignetteController` on a full-screen UI overlay object, preferably `DeathVisionCanvas > DeathVisionVignette`.
- Use `Tools/Incantation/Repair Death Vision Setup` to create a simple setup automatically when missing.
- `vignetteCanvasGroup`: assign the full-screen black overlay CanvasGroup.
- `vignetteRoot`: assign the same full-screen overlay RectTransform.
- `initialDelayAfterGrab`: `0.0` seconds by default. This lets the vignette begin immediately when the demon hand grab event fires.
- `absorptionDelayAfterGrab`: `0.25` seconds by default. `Prototype tuning`.
- `fullBlackDelayAfterGrab`: `0.85` seconds by default. `Prototype tuning`.
- `totalDuration`: `1.0` second by default. `Prototype tuning`.
- `opacityCurve`: controls how quickly the black overlay blocks vision.
- `scaleCurve`: controls the staged closing scale of the overlay root.
- `jumpPulseScale`: `1.08` by default for a short jump pulse at each closing stage.
- `jumpPulseDuration`: `0.045` seconds by default. `Prototype tuning`.
- `hideOnAwake`: keep `true` so the overlay starts invisible.
- `onVignetteStarted`: optional visual/audio-only hooks.
- `onAbsorptionMoment`: invoked after `absorptionDelayAfterGrab`; the bridge also listens to this moment in code to start `PlayerAbsorptionController.BeginAbsorption(CurrentFailedPlayer)`.
- `onFullBlack`: invoked after `fullBlackDelayAfterGrab`.
- `onVignetteFinished`: optional visual/audio-only hooks after the overlay reaches full black.

Runtime behavior:

- This component is visual-only.
- It does not decide ritual failure, timeout, elimination, seating, turn order, or player movement.
- `Play()` prevents double-play while the vignette sequence is already running.
- The current prototype uses a simple full-screen black UI Image and CanvasGroup. The art can be improved later without changing the failure-to-absorption timing contract.
- The vignette should be mostly blocked before visible absorption clipping occurs, and fully black before the absorbed player disappears.

## RitualFailureAbsorptionBridge

Recommended setup:

- Add `RitualFailureAbsorptionBridge` to the same book-side object as `PlayerAbsorptionController`, or to another small scene helper object near the real `BookModel`.
- `ritualController`: assign the scene `RitualController`.
- `playerAbsorptionController`: assign the `PlayerAbsorptionController` that should run the visual pull-in.
- `deathVisionVignetteController`: optional. Assign the scene `DeathVisionVignetteController` to delay absorption behind the death-vision overlay.
- Connect `DemonHandController.onGrabMoment` to `RitualFailureAbsorptionBridge.AbsorbCurrentFailedPlayer()`.

Runtime behavior:

- `RitualController` still decides when ritual failure happens and calls `DemonHandController.PlaySequence()`.
- When ritual failure happens, `RitualController` stores `CurrentFailedPlayer` from the current active Seat by calling `Seat.GetRealPlayerTransform()`.
- For temporary local testing only, if the active Seat has no real player Transform and `debugAbsorptionPlayerOverride` is assigned, `RitualController` stores that override and logs: `Using debugAbsorptionPlayerOverride for absorption test.`
- `DemonHandController` still controls only the demon hand animation and fires `onGrabMoment` at the grab/contact frame.
- `RitualFailureAbsorptionBridge` reads the stored failed player from `RitualController.CurrentFailedPlayer` when the grab event fires.
- If `deathVisionVignetteController` is assigned, the bridge calls `DeathVisionVignetteController.Play()` first. The vignette begins at GrabMoment, then its absorption moment starts `PlayerAbsorptionController.BeginAbsorption(CurrentFailedPlayer)` after `absorptionDelayAfterGrab`.
- If `deathVisionVignetteController` is missing, the bridge falls back to the previous immediate behavior and calls `PlayerAbsorptionController.BeginAbsorption(CurrentFailedPlayer)` directly at GrabMoment.
- Default timed sequence: failure starts the demon hand animation, GrabMoment occurs around `2.708` seconds in the demon hand clip, the vignette starts immediately at GrabMoment, absorption begins about `0.25` seconds after GrabMoment, full black occurs about `0.85` seconds after GrabMoment, and the vignette finishes at about `1.0` second after GrabMoment.
- If no failed player can be resolved, the bridge logs one warning and the failure sequence continues.
- If the active Seat has no real player Transform, `RitualController` logs: `Cannot absorb failed player because the active seat has no real player Transform assigned.`

Editor validation:

- Use `Tools/Incantation/Validate Absorption Setup` to inspect only the open scene absorption gameplay wiring.
- The absorption report checks `Book`, `BookModel`, `BookAbsorptionTarget`, `BookFailureSequence`, `PlayerAbsorptionController`, `RitualFailureAbsorptionBridge`, bridge gameplay references, `PlayerAbsorptionController.absorptionTarget`, the `DemonHandController` under `BookModel`, and whether `DemonHandController.onGrabMoment` calls `RitualFailureAbsorptionBridge.AbsorbCurrentFailedPlayer()`.
- The report also lists every `Seat`, whether `currentPlayer` is assigned, whether `realPlayerTransform` has been automatically assigned, and whether `Seat.GetRealPlayerTransform()` returns a valid absorption target.
- Use `Tools/Incantation/Repair Absorption Setup` to create missing `BookAbsorptionTarget` and `BookFailureSequence` objects under `BookModel`, add missing absorption gameplay components, assign their gameplay references, and wire the demon hand grab UnityEvent.
- Use `Tools/Incantation/Validate Death Vision Setup` to inspect only the death vignette UI setup, its timing values, and whether `RitualFailureAbsorptionBridge.deathVisionVignetteController` is assigned.
- Use `Tools/Incantation/Repair Death Vision Setup` to create or repair `DeathVisionCanvas`, `DeathVisionVignette`, the full-screen black Image, CanvasGroup, `DeathVisionVignetteController`, controller references, and the optional bridge vignette reference.
- Repair does not delete objects and does not assign player transforms. Seat-to-player binding happens at runtime through `Seat.Occupy()`.
- If validation reports that an occupied Seat does not expose a real player Transform, make sure that Seat was occupied with the actual visible player root rather than a debug placeholder.

## BookAftermathController

Recommended setup:

- Place `BookAftermathController` on the book-side failure sequence object, preferably `Book > BookModel > BookFailureSequence`.
- Use `Tools/Incantation/Validate Book Aftermath` to inspect aftermath wiring in the open scene.
- Use `Tools/Incantation/Repair Book Aftermath` to add the controller if missing, add an `AudioSource` if missing, preserve existing assignments, wire an obvious smoke or ink `ParticleSystem` when one can be found, and connect `PlayerAbsorptionController.onAbsorptionFinished` to `BookAftermathController.PlayAftermath()`.
- `audioSource`: assign the AudioSource that should play the book burp. Repair adds one on the same object when missing.
- `burpClips`: assign the possible short burp AudioClips. Size is the number of possible burps. One clip is randomly selected each time the Book consumes a player. At least one clip is required for aftermath validation to pass.
- `smokeParticles`: optional. Assign the one-shot dark smoke or black ink ParticleSystem that should puff from the book. This should be a visual-only particle effect near the real `BookModel`.
- `burpDelay`: `0.20` seconds by default. `Prototype tuning`.
- `onAftermathStarted`: optional visual/audio-only hooks when the aftermath coroutine begins.
- `onBurpPlayed`: optional visual/audio-only hooks after the burp and smoke are triggered.
- `onAftermathFinished`: optional visual/audio-only hooks immediately after the aftermath trigger moment.

Runtime behavior:

- This component is visual/audio-only.
- It does not decide ritual failure, timeout, elimination, seating, turn order, book traversal, voice validation, or player absorption.
- `PlayAftermath()` starts a coroutine, waits `burpDelay`, randomly selects one assigned `burpClips` entry and plays it once through the assigned AudioSource if both are assigned, plays `smokeParticles` once if assigned, invokes `onBurpPlayed`, and finishes immediately.
- The intended sequence hook is `PlayerAbsorptionController.onAbsorptionFinished -> BookAftermathController.PlayAftermath()`, so the burp and smoke happen after the player absorption and BOOM punctuation have completed.
- `onAftermathFinished` should invoke `BookPrisonSpectatorController.SendCurrentFailedPlayerToBookPrison()` first, then `RitualController.CompleteCurrentFailedPlayerElimination()` so the failed Seat is eliminated only after the Book Prison transition has run.
- If no smoke particle system is assigned, the aftermath finishes gracefully without blocking gameplay. Validation reports missing smoke as optional only.

## BookPrisonSpectatorController

Recommended setup:

- Place `BookPrisonSpectatorController` on the book-side failure sequence object, preferably `Book > BookModel > BookFailureSequence`.
- Use `Tools/Incantation/Validate Book Prison Spectator` to inspect spectator wiring in the open scene.
- Use `Tools/Incantation/Repair Book Prison Spectator` to add the controller if missing, preserve existing assignments, populate an empty `prisonSlots` array from clearly named numbered objects when they exist, assign the scene `RitualController` when found, connect the portal RenderTexture references when possible, and wire `BookAftermathController.onAftermathFinished` to `BookPrisonSpectatorController.SendCurrentFailedPlayerToBookPrison()`.
- `ritualController`: assign the scene `RitualController`. The spectator controller reads `RitualController.CurrentFailedPlayer` after failure. In a network ritual it also uses the retained authoritative `CurrentFailedPlayerId` to resolve the exact `NetworkPlayer`; it activates the prison camera only for that player's owning client.
- `prisonSlots`: assign up to 8 fixed Book Prison slots. Each slot needs a `spawnPoint` and a `spectatorCamera`.
- `prisonSlots[x].spawnPoint`: assign the fixed position for a consumed player in the DeathZone/Book Prison room.
- `prisonSlots[x].spectatorCamera`: assign the fixed camera for that dead player slot.
- `bookPortalCamera`: assign the manually placed `BookPortalCamera`. This camera should look back at the ritual table or book area for the portal view.
- `bookPortalRenderTexture`: assign a manually created RenderTexture, recommended path `Assets/RenderTextures/RT_BookPortal.renderTexture`.
- `portalScreenRenderer`: assign the renderer on the manually placed `BookPortalScreen`.
- `movePlayerToPrison`: keep `true` when the consumed player's Transform should be moved to the first open prison slot spawn.
- `activateSpectatorCamera`: keep `true` when the consumed player's view should switch by disabling the other prison slot cameras and enabling the assigned slot camera.
- `deactivateTablePlayerModel`: keep `false` unless the consumed player's table model should be hidden entirely after the spectator transition.
- `onSpectatorStarted`: optional visual/UI/audio-only hooks after the local spectator transition begins.

Manual scene objects to create:

- `BookPrisonSpawnPoint1` through `BookPrisonSpawnPoint8`: empty Transforms inside the manually built DeathZone/Book Prison room.
- `BookPrisonSpectatorCamera1` through `BookPrisonSpectatorCamera8`: fixed cameras paired with the matching numbered prison spawn.
- `BookPortalCamera`: the camera that renders the table/book view into the portal.
- `BookPortalScreen`: the in-room screen or surface that displays the portal RenderTexture.
- `RT_BookPortal`: a RenderTexture asset, recommended at `Assets/RenderTextures/RT_BookPortal.renderTexture`.

Runtime behavior:

- This component is local/prototype spectator presentation.
- It does not decide ritual failure, timeout, elimination, seating, turn order, book traversal, voice validation, player absorption, or aftermath playback.
- The current intended sequence is `PlayerAbsorptionController.onAbsorptionFinished -> BookAftermathController.PlayAftermath() -> BookAftermathController.onAftermathFinished -> BookPrisonSpectatorController.SendCurrentFailedPlayerToBookPrison() -> RitualController.CompleteCurrentFailedPlayerElimination()`.
- `SendCurrentFailedPlayerToBookPrison()` uses the assigned `RitualController.CurrentFailedPlayer`.
- `SendPlayerToBookPrison(player)` can be called directly by tests or custom scene events with an explicit player Transform.
- `SendPlayerToBookPrison(player)` chooses the first `prisonSlots` entry with an assigned `spawnPoint` that is not already occupied, so the first dead player goes to slot 1, the second dead player goes to slot 2, and so on by Inspector array order.
- Dead players cannot move freely in this MVP. The Book Prison is fixed-position spectator presentation only; do not add movement controls to this controller.
- Before activating the assigned slot camera for the locally owned eliminated player, the controller deactivates every other prison slot camera. Remote elimination presentation does not alter the local prison-camera state.
- When `bookPortalCamera` and `bookPortalRenderTexture` are both assigned, the controller assigns `bookPortalCamera.targetTexture`.
- When `portalScreenRenderer` and `bookPortalRenderTexture` are both assigned, the controller assigns the portal screen material's main texture at runtime.
- `ResetSpectatorView()` marks every prison slot unoccupied, deactivates all prison slot cameras, and restores player Transform/active state for Play Mode testing.

What remains manual:

- The Book Prison room, art, lighting, camera framing, portal screen mesh, and RenderTexture asset are created and placed manually.
- Repair does not create the prison room, move art, guess portal placement, or create a RenderTexture asset.
- Repair only populates an empty `prisonSlots` array from exact numbered objects named `BookPrisonSpawnPoint1`, `BookPrisonSpectatorCamera1`, `BookPrisonSpawnPoint2`, `BookPrisonSpectatorCamera2`, and so on through 8.
- Repair only assigns `BookPortalCamera` and `BookPortalScreen` automatically when objects with those exact names already exist in the open scene.

## Timer

Script defaults:

- `duration`: `10` seconds. `Prototype tuning`.
- `warningThreshold`: `3` seconds. `Prototype tuning`.

Runtime note:

- `RitualController.hourglassDuration` is the shared offline/network turn-duration configuration,
  currently `90` seconds in `MainGame`.

Ownership:

- `Timer` owns countdown state and timer lifecycle events.
- It does not own ritual success, phrase growth, traversal, or elimination.

## HourglassController

Recommended setup:

- `timer`: assign the scene `Timer`.
- Forward timer events to visual/audio feedback only where appropriate.

Notes:

- `HourglassController` wraps timer control for ritual systems.
- It should signal pressure and timeout, not decide phrase validation or traversal.

## HourglassVisualController

Recommended setup:

- `timer`: assign the scene `Timer`.
- `hourglassRoot`: optionally assign the root of the hourglass visual object for organization/reference.
- `topSand`: optionally assign a separate top sand visual Transform.
- `bottomSand`: optionally assign a separate bottom sand visual Transform.
- `topSandFullScale`: set to the authored full local scale of the top sand visual.
- `bottomSandFullScale`: set to the authored full local scale of the bottom sand visual.
- `sandCurve`: tune the visual fill/drain progression only.
- `resetVisualsOnEnable`: keep `true` when the hourglass should start with full top sand and empty bottom sand.

Notes:

- This component is visual-only.
- It reads `Timer` state and timer lifecycle events, but it must not start, stop, reset, or otherwise modify timer gameplay.
- The existing Sablier mesh does not need to be edited or replaced. Use separate Inspector-assigned sand visual objects when configuring this effect.

## HourglassWarningAudio

Recommended setup:

- `timer`: assign the scene `Timer`.
- `audioSource`: assign the AudioSource that should play the warning tick/chime.
- `warningClip`: assign the warning sound clip. If left empty, playback is skipped safely.
- `warningThreshold`: keep `10` seconds for the default final-countdown warning window.
- `playEverySecond`: keep `true` for one warning sound per whole second.
- `resetOnTimerRestart`: keep `true` so each new turn can warn again.
- `fadeOutDuration`: keep `0.35` seconds for a short turn-end fade.
- `fadeOutOnTimerStop`: keep `true` so warning audio fades out when the active timed turn ends.

Notes:

- This component is audio-only.
- It reads `Timer` state and timer lifecycle events, but it must not start, stop, reset, or otherwise modify timer gameplay.
- Fade-out should be driven only by `Timer.OnStopped`, `Timer.OnReset`, or `Timer.OnFinished`, not by individual word validation.
- It should live beside the hourglass audio/visual setup, not on gameplay authority objects like Seats, BookGhost, or ritual phrase systems.

## HourglassLightPossessionController

Current `MainGame` setup:

- `timer`: assigned.
- `affectedLights`: assigned to fire flicker lights around the room.
- `possessionStartRemainingSeconds`: `15`. `Prototype tuning`.
- `usePossessedFlicker`: `true`.
- `possessedFlickerSpeed`: `45`. `Prototype tuning`.
- `possessedFlickerDepth`: `1.5`. `Prototype tuning`.
- `minimumIntensityMultiplier`: `0`. `Prototype tuning`.
- `affectRange`: `true`.
- `minimumRangeMultiplier`: `0`. `Prototype tuning`.
- `recoveryDurationSeconds`: `1.25`. `Prototype tuning`.
- `restoreOnStop`: `true`.

Notes:

- This is a visual pressure effect.
- It depends on `Timer` for remaining-time authority.
- It should not own ritual failure or elimination.

## FireLightFlicker

Current common values:

- `baseIntensity`: commonly `1.5`; some stronger lights use `2.5`. `Prototype tuning`.
- `intensityVariation`: commonly `0.35`; stronger lights use `0.7`. `Prototype tuning`.
- `baseRange`: commonly `4`; stronger lights use `6`. `Prototype tuning`.
- `rangeVariation`: commonly `0.4`; stronger lights use `0.8`. `Prototype tuning`.
- `flickerSpeed`: commonly `8`; some lights use `5`. `Prototype tuning`.
- `randomizeOffset`: `true`.

Notes:

- `targetLight` should be assigned or resolved from the same GameObject.
- External multipliers may be driven by possession lighting.

## AmbientRandomSoundPlayer

Current `MainGame` values include two ambience profiles:

Quiet distant ambience:

- `minDelaySeconds`: `8`. `Prototype tuning`.
- `maxDelaySeconds`: `38`. `Prototype tuning`.
- `minVolume`: `0.07`. `Prototype tuning`.
- `maxVolume`: `0.1`. `Prototype tuning`.
- `minPitch`: `0.92`.
- `maxPitch`: `1.08`.
- `playOnStart`: `true`.

Closer/random ambience:

- `minDelaySeconds`: `8`. `Prototype tuning`.
- `maxDelaySeconds`: `22`. `Prototype tuning`.
- `minVolume`: `0.35`. `Prototype tuning`.
- `maxVolume`: `0.8`. `Prototype tuning`.
- `minPitch`: `0.92`.
- `maxPitch`: `1.08`.
- `playOnStart`: `false` in the current scene sample.

Notes:

- The component configures its `AudioSource` as non-looping, non-play-on-awake, and 2D.
- Future improvement: route ambience through an audio mixer group.

## IncantationManager

Recommended setup:

- `wordLibrary`: assign `IncantationWordLibrary`, not `SpellPhraseLibrary`.
- `incantationLength`: legacy/default value may exist, but the current growing phrase path should come from the core ritual loop when bridged.
- Events should drive display and feedback only.

Notes:

- `IncantationManager` is still used by the current display and event model.
- It should not become the long-term owner of every ritual responsibility.

## IncantationTextDisplay

Script defaults and current intent:

- `emptyText`: `Awaiting incantation...`.
- `completedWordColor`: gray.
- `currentWordColor`: yellow.
- `remainingWordColor`: white.
- `correctFeedbackColor`: green.
- `incorrectFeedbackColor`: red.
- `feedbackDuration`: `0.25` seconds. `Prototype tuning`.
- `incorrectFeedbackDuration`: `0.45` seconds. `Prototype tuning`.
- `replayStepDuration`: `0.22` seconds. `Prototype tuning`.
- `replayPulseScale`: `1.15`. `Prototype tuning`.
- `writingSpeed`: `18`. `Prototype tuning`.

Notes:

- Display reacts to incantation state.
- Display must not own phrase authority.

## WindowsKeywordVoiceRecognizer

Recommended setup:

- `wordLibrary`: assign `IncantationWordLibrary`.
- `logRecognizedPhrases`: `true` during prototype tuning, `false` when logs become noisy.

Notes:

- This is currently preferred for `WordByWordRealtime`.
- It builds keywords from ritual words and speech aliases.
- It is Windows-only.
- It emits recognition candidates; it does not decide gameplay success.

## WhisperVoiceRecognizer

Recommended role:

- Use for `FullPhrase` or experimental recognition paths only.
- Do not force it as the only validation path.

Important references:

- `whisper`: assign `WhisperManager`.
- `microphoneRecord`: assign `MicrophoneRecord`.

Useful defaults:

- `ignoreEmptyTranscripts`: `true`.
- `minimumRecordingLengthSeconds`: `0.35`.
- `enableSpeechEndAutoStop`: `true`.
- `speechEndSilenceSeconds`: `0.45`. `Prototype tuning`.
- `useDynamicSpeechEndSilence`: `true`.
- `oneWordSpeechEndSilenceSeconds`: `0.45`. `Prototype tuning`.
- `twoWordSpeechEndSilenceSeconds`: `0.55`. `Prototype tuning`.
- `threeWordSpeechEndSilenceSeconds`: `0.7`. `Prototype tuning`.
- `fourOrMoreWordSpeechEndSilenceSeconds`: `0.85`. `Prototype tuning`.
- `autoEnableMicrophoneVad`: `true`.
- `enableShortPhraseMaxRecordingDuration`: `true`.
- `oneWordMaxRecordingSeconds`: `2.75`. `Prototype tuning`.
- `twoWordMaxRecordingSeconds`: `3.75`. `Prototype tuning`.
- `enableDebugLogs`: `true` while tuning.
- `logRecognizedPhrases`: `true` while tuning.

## Character Face

Eyelid blink controller:

- Add `EyelidBlinkController` to the character root or an existing character face coordination object. Do not add it separately to either eyelid and do not add an Animator, bones, blend shapes, or `SkinnedMeshRenderer` logic for blinking.
- `leftEyelid`: assign the Transform of the existing left eyelid GameObject that uses its separate Mesh Filter and Mesh Renderer.
- `rightEyelid`: assign the Transform of the existing right eyelid GameObject that uses its separate Mesh Filter and Mesh Renderer.
- `openScaleZ`: `0`. This is the local Z scale restored when the eyes are open.
- `closedScaleZ`: `-2.2`. This is the local Z scale reached at full closure.
- `closeDuration`: `0.08` seconds. `Prototype tuning`.
- `openDuration`: `0.12` seconds. `Prototype tuning`.
- `minimumBlinkInterval`: `3` seconds. `Prototype tuning`.
- `maximumBlinkInterval`: `8` seconds. `Prototype tuning`.
- `blinkOnEnable`: keep disabled for normal random blinking. Enable it only when an immediate blink on component enable is desired for testing.
- To test manually in Play Mode, open the `EyelidBlinkController` component's context menu in the Inspector and select `Blink`. The same public `Blink()` method is available for future callers. Requests made during an active blink are safely ignored.
- The controller uses unscaled time and modifies only each assigned eyelid Transform's `localScale.z`. It preserves current local X/Y scale and does not modify position, rotation, renderers, materials, Colliders, or parent Transforms.

Voice lip controller:

- Add `VoiceLipController` from `Assets/scripts/Character/Face/VoiceLipController.cs` to the character root or an existing character face coordination object. It animates the two existing Mesh Renderer lip objects through their Transforms and does not use an Animator, clips, blend shapes, or `SkinnedMeshRenderer` manipulation.
- `upperLip`: assign the Transform of the existing upper-lip Mesh Renderer object.
- `lowerLip`: assign the Transform of the existing lower-lip Mesh Renderer object. If either lip is unavailable, the assigned lip still animates; if neither is assigned, the component disables itself.
- `voiceAmplitudeProvider`: assign the local player's `VoiceAmplitudeProvider`. The lip controller reads only its normalized `CurrentAmplitude` and never starts, stops, selects, or configures microphone recording. Without this reference, automatic mode keeps the lips closed and logs one warning.
- `useManualOpenAmount`: keep disabled for automatic voice-driven movement. Enable it when `SetOpenAmount(float amount)` should supply the mouth openness for debugging or a future animation system.
- `closedAngle`: `0` degrees.
- `upperOpenAngle`: `-20` degrees.
- `lowerOpenAngle`: `20` degrees.
- `smoothingSpeed`: `15`. Increase it for a faster response or decrease it for softer motion. `Prototype tuning`.
- `SetOpenAmount(float amount)` clamps the supplied value to `0` through `1` and immediately applies it. With `useManualOpenAmount` enabled, that value remains the smoothed target for debugging and future animation systems.
- The controller preserves each assigned lip's original local Y and Z rotation, animates only local X, smoothly returns to the closed angle during silence, and restores the complete original local rotations when disabled.

## Character Appearance

Player appearance is data-driven and requires no NetworkPlayer prefab cosmetic references.

- `CharacterSkinPalette` owns the local Skin catalog and material application.
- Each `CharacterSelectionGroup` must set `appearanceSlot` to the stable slot represented by
  that group. The current Hair group uses `Hair`; the moustache/beard group uses `Beard`.
- Entry ordering is network data. Keep the same entry index mapped to the same cosmetic on every
  client and build.
- `CharacterAppearancePresentation` is added and initialized at runtime by
  `NetworkCharacterPresentation`. Do not add renderer, material, mesh, or cosmetic GameObject
  fields to `NetworkPlayer`.
- The current scene-authored local character is the source for observed character clones so the
  existing customization components and their configured catalogs are preserved. Non-owner
  camera, audio listener, and movement controls remain disabled.
- New GameObject-based cosmetic slots should normally reuse `CharacterSelectionGroup`, set its
  `appearanceSlot`, and keep its entry ordering stable. New material/palette data should extend
  `CharacterAppearancePresentation` while continuing to synchronize only compact IDs.

## Camera

Camera transition foundation:

- `BookMenuController` registers its existing `bookMenuCameraTarget` with `CameraTransitionManager` at runtime. Moving the local menu camera to that target selects the local-only `BookInteraction` input context; moving to any other target restores `Gameplay`. No additional scene reference is required.
- `LocalInputContextGate` contains `Gameplay`, `BookInteraction`, and `TextEntry`. It is process-local and is never synchronized. Focused Priest Name, hosted Seal, and Join Seal editors acquire `TextEntry`; leaving the editor, changing page, closing the Book, disconnecting, or disabling the controller releases it. Controller/camera teardown resets the gate to `Gameplay`.
- `CameraTransitionManager` lives at `Assets/scripts/Camera/CameraTransitionManager.cs`.
- `MenuTransitionCamera` is the only rendering camera for menu navigation.
- `BookMenuCamera`, `LobbyCamera`, and future menu viewpoints such as `CharacterCamera` are static destination Transforms only. They must not render during menu navigation.
- The manager moves only the assigned active viewing Camera, currently `MenuTransitionCamera`, between authored camera-viewpoint Transforms. It does not create cameras, delete cameras, rename cameras, change camera references, or move the viewpoint Transforms.
- `transitionDuration`: time in seconds for a smooth camera move. `Prototype tuning`.
- `transitionCurve`: curve evaluated from 0 to 1 across the move. Use it to shape ease-in, ease-out, or theatrical camera travel without changing target Transforms.
- `activeCamera`: assign `MenuTransitionCamera`.
- `MoveTo(target)` and `MoveToImmediate(target)` are the only camera movement API. Menu, lobby, and book interactions pass destination Transforms into this generic mover.
- Expected menu flow: `MenuTransitionCamera` starts at the `BookMenuCamera` Transform. `Enter the Circle` changes only the Book page and keeps the `BookInteraction` context. `Take My Seat` calls `BeginSeatSelection()` and requests movement to the `LobbyCamera` Transform. Clicking the physical Book requests movement back to the `BookMenuCamera` Transform.

Book state controller:

- Add `BookStateController` to the existing scene object that coordinates the Living Book. Do not create another Book, Canvas, panel, or TextMeshPro object.
- The left Book page owns navigation, state choices, and Back actions. The right Book page owns contextual information and contextual actions. Never share TextMeshPro objects, `BookMenuItem` components, or Colliders between the pages.
- `title`: assign the existing title `TMP_Text` on the Book.
- `line1` through `line4`: assign the four existing line `TMP_Text` entries in their displayed order.
- `menuItem1` through `menuItem4`: assign the `BookMenuItem` beside the matching line. The line and item numbers must match.
- `line5`: assign a separately placed optional fifth left-page `TMP_Text`. Current states clear it; it remains available for future page layouts.
- `menuItem5`: assign the fifth left-page line's own `BookMenuItem` and Collider. Do not overload another Collider or reuse a right-page component for future fifth-line actions.
- `bookMenuController`: assign the existing `BookMenuController` that owns menu actions.
- `rightPageController`: assign the independent `BookRightPageController`. This reference is optional so the left page can still operate while manual right-page setup is incomplete.
- `characterBookPageController`: assign `CharacterBookPageController` so Character category actions and the default Character right page can be refreshed when entering `CharacterMenu`.
- `voiceBookPageController`: assign the `VoiceBookPageController` on the same Living Book coordination object. It supplies Voice-page values and controls without owning navigation or page transitions.
- `textTransitionController`: assign the shared `BookTextTransitionController` used by both Book pages. `BookStateController` combines left- and right-page targets into one coordinated transition so a state change produces one paper sound, not one sound per page or line.
- On `Start`, the controller displays the appropriate initial page. `ChangePage(BookState)` is the navigation path: it reuses the assigned text entries, prepares their click actions, and plays the coordinated left/right page transition. Calling it for the page that is already open delegates to `RefreshCurrentPageContent()` instead of replaying the transition.
- `RefreshCurrentPageContent()` is the data-refresh path. It writes synchronized lobby counts, Ready/Unready labels and button state, Ritual Seal status, and other current-page values directly into the existing TMP entries. It does not call `BookTextTransitionController`, rebuild layout, replay fades or paper audio, reset focus, or disturb an unchanged hover state.
- `PlayMenu` displays `THE RITUAL` with `Create Ritual`, `Join Ritual`, `Back`, and an empty fourth line on the four existing left-page lines. `Create Ritual` calls `CreateNetworkRitual()` immediately; it does not open a pre-creation confirmation page.
- While Host startup is pending, the existing Play page silently shows `Creating Ritual...`, disables that action, and presents the service status on the right page. Authoritative success means `RitualSealService.IsHostingRitual` is true: the Seal exists and both FishNet server and local client are Started.
- Selecting `Create Ritual` is genuine navigation from `PlayMenu` to the Host destination and plays exactly one normal Book transition. `BookTextTransitionController` invokes its reusable hidden-content callback after disappearance and before target assignment; only then does `BookMenuController` start Host creation.
- The hidden destination is rebuilt from the authoritative result before reveal: fast success reveals the final Host lobby directly, while slower startup reveals `Creating Ritual...`. Success after reveal silently changes that prepared content to `Enter the Circle`, `Invite a Priest`, and `Quit Ritual`; it never starts a second transition.
- Recreating the Book controller while an authoritative Host is already running restores that same active Host presentation through the silent content path. After `Quit Ritual`, `RitualSealService.Changed` performs the single intentional navigation to `PlayMenu`; `BookMenuController` must not issue a duplicate same-state `ChangePage(PlayMenu)` in that frame. A duplicate call would refresh and re-enable Create while the Quit transition is still revealing, allowing new Host content to be written into an active transition. Once the intentional Quit transition completes, every later `Create Ritual` cycle follows the silent success path.
- Empty page lines remain assigned but display an empty string and have no click action. `Create Ritual` and `Join Ritual` are connected to the TASK-042 Book-owned LAN flow.
- States include `MainMenu`, `PlayMenu`, `HostMenu`, `JoinMenu`, `JoinSealEntry`, `RitualCreated`, `CharacterMenu`, `OptionsMenu`, `VoiceMenu`, and `Lobby`. `RitualCreated` remains in the serialized enum for compatibility but is obsolete as a displayed page; requests for it resolve to `HostMenu`. Voice detail selection stays inside `VoiceMenu` and does not trigger another state transition.
- Selecting `Join Ritual` opens `JoinSealEntry` directly. The left page shows only `JOIN RITUAL` and the dedicated bottom-left `Back` line. `Back` clears input and join status and returns to `PlayMenu` without disconnecting an established client.

Book right page controller:

- The normal Circle right page displays the local synchronized Priest Name, player count, and
  Invite action. The obsolete `High Priest` presentation is not displayed.
- Selecting `Priest Name` silently replaces only the right-page content with the name editor;
  Confirm submits one owner ServerRpc and Cancel restores synchronized content without a request.
- On the active Host page, `Seal: XXXX` is selectable only through the local Host presentation.
  Its editor uses the existing four-character input rules. Replacement status occupies the
  reserved line between Seal and player count; a conflict displays exactly `Seal already used`.
- LAN discovery records advertisements by stable Host-session GUID and normalized Seal. Multiple
  Hosts with different Seals remain independently discoverable; an edit conflicts only with a
  live advertisement for the same normalized Seal.
- The discovery window is best-effort rather than an atomic reservation. Quitting sends a
  withdrawal for only the local Host session; other Host records and FishNet sessions are not
  changed.

- The existing Book page layout is artist-authored and must be preserved. Functional tasks may update text content, visibility, interactivity, collider state, and state-based colors, but must not change TMP alignment, RectTransform anchors or pivots, positions, dimensions, margins, font sizing, autosizing, spacing, or visual hierarchy unless a task explicitly requests a layout redesign.
- `BookRightPageController` uses the serialized scene and prefab layout directly. It must not center, reposition, resize, or otherwise restore layout values at runtime when changing Book states; avoiding those mutations also prevents placement flashes during transitions.
- Add `BookRightPageController` to the existing Living Book/menu coordination object. It presents context for the state owned by `BookStateController`; it does not own or change Book state.
- Manually create and position six right-page TextMeshPro objects: one title and five lines. Assign them to `rightTitle` and `rightLine1` through `rightLine5` in displayed order.
- Add five new, independent `BookMenuItem` components for the five right-page lines. Assign them to `rightMenuItem1` through `rightMenuItem5` in matching order. Do not reuse `menuItem1` through `menuItem4` from the left page.
- Give each of the five right-page line objects its own manually sized and positioned `BoxCollider`, paired with its independent `BookMenuItem`. A left-page and right-page entry must never share a Collider. The controller does not create, move, or resize Colliders.
- On each right-side `BookMenuItem`, assign its matching right-side TextMeshPro component and preserve manually chosen hover color and hover scale values. Remove persistent On Click listeners because actions are supplied by `BookRightPageController` at runtime.
- Assign the existing `BookMenuController` to `bookMenuController` so intentional right-page actions such as `Leaderboard` and `Discord` use the existing menu behavior.
- `textTransitionController`: assign the same shared `BookTextTransitionController` assigned to `BookStateController`. State-driven updates are coordinated by `BookStateController`; independent contextual right-page updates use this reference directly.
- `voiceBookPageController`: assign the same `VoiceBookPageController` used by `BookStateController`. In `VoiceMenu`, it prepares the right-page values as part of the existing coordinated transition.
- `MainMenu` clears the right title, displays `Leaderboard` on right line 1 and `Discord` on right line 2, and clears right lines 3 through 5. These actions use the existing independently assigned right-page `BookMenuItem` components and Colliders.
- `Leaderboard` calls `BookMenuController.OpenLeaderboard()` and logs `Leaderboard is not implemented yet.` as a placeholder. `Discord` calls `BookMenuController.OpenDiscord()`.
- `PlayMenu` and `OptionsMenu` clear the right-page text and actions.
- `HostMenu` exists only after authoritative Host creation succeeds. Its right side shows only `Seal: XXXX`, the synchronized `X / 8` Circle membership count, and the count-based waiting message. No inactive Seal placeholder, Host Name, duplicate Seal, or local player counter is stored by the Book.
- Host-lobby waiting text is presentation-only and centralized in `BookRightPageController`: 0–1 members use `Waiting for other priests...`; 2–3 use `The circle begins to form...`; 4–6 use `More priests are gathering...`; 7 uses `One priest remains...`; and 8 uses `The circle is complete.`.
- Seal availability, joins, disconnects, and waiting-message/count changes use `RefreshCurrentPageContent()` and `RefreshHostPageSilently()`. They update existing TMP entries without invoking `BookTextTransitionController`.
- `Invite a Priest` remains a safe placeholder that logs its unimplemented status. It is reserved for future invitation integration and does not add clipboard, platform, or external social behavior.
- `BookStateController` no longer has editable current/max lobby-count fields. `THE CIRCLE`
  renders `Players (current / 8)` from `NetworkPlayer.CircleMemberCount` and
  `NetworkPlayer.MaximumCircleMembers`; do not add a scene-local player-count override.
- A joined client switches from `JoinSealEntry` to `Lobby` only after its owner-assigned
  `NetworkPlayer.IsCircleMember` value is synchronized true. `BookStateController` observes
  `NetworkPlayer.CircleRosterChanged` and queries current membership on `Start` and re-enable,
  so no additional Inspector reference is required and late UI subscription is supported.
- The Host keeps the explicit `Enter the Circle` action. It calls `OpenCirclePage()`, changes
  only to the existing `Lobby`/`THE CIRCLE` Book page, and leaves the Book camera and
  `BookInteraction` input context active. Connected members are counted before `Take My Seat`;
  Circle membership must not be derived from Seat occupancy.
- `Take My Seat` is the only Book action that calls `BeginSeatSelection()`. That path enables
  Seat interaction and moves the local menu camera to the Lobby viewpoint. It does not change
  another player's camera and does not assign a Seat until the existing Seat click path succeeds.
- The Circle exit action is role-aware before and after local Seat selection. An authoritative
  Host or Host-in-creation sees `Back`, which calls `ReturnToActiveHostLobby()` and changes only
  Book state while preserving FishNet, the Seal, Circle membership, Ready state, appearance,
  roster count, Book camera, and disabled Seat-selection state. A joined non-Host sees
  `Quit Ritual`, which calls the generic `BookMenuController.QuitRitual()` dispatcher and uses
  `LeaveJoinedRitual()` to disconnect only that local client and return it to `PlayMenu`.
  Role changes refresh this line directly without replaying a page transition.
- `JoinSealEntry` opens immediately when `Join Ritual` is selected and uses the existing artist-authored right-page TMP lines as one focused Book form: right line 1 shows the non-interactive `Seal` title, right line 2 is the interactive four-character field, right line 3 is the `Validate Seal` Book button, and right line 4 is the non-interactive status. Right line 5 remains empty and non-interactive. It never displays player count or duplicates the entered Seal outside the field.
- Seal entry is focused as soon as the page opens. Input is normalized to the readable uppercase Seal alphabet, spaces and unsupported characters are ignored, and Backspace removes a character. `Validate Seal` is grey and non-interactive until exactly four characters are present, then fades to its authored gold color over `0.18` seconds. Enter is an optional shortcut and submits only while the button is enabled.
- Join Seal focus acquires the local `TextEntry` input context before the page opens. While held,
  Space, E, H, physical Book interaction, gameplay/debug Book movement, page shortcuts, and
  spell-card shortcuts cannot consume typed keys. Enter submits at most one request, Escape or
  `Back` cancels once, and the pending-join state disables repeated confirmation. The context is
  released on success/navigation, cancellation, Book close, controller disable, or connection
  loss; a failed join that keeps the form open keeps text focus so the Seal can be corrected.
- Validate Seal's disabled color and fade are temporary `JoinSealEntry` state visuals only. Leaving Join stops any active fade and restores right line 3's serialized color and shared material before the next page is prepared, so reused content such as `Players: 1 / 8` never inherits Join styling.
- Typing, deleting, status changes, and Validate state changes update the Join form immediately without replaying the Book page transition. Full text transitions run only when entering or leaving a Book state.
- Join status text is concise: `Enter a ritual seal.`, `Ready to join.`, `Joining ritual...`, `Ritual joined.`, `Ritual not found.`, `Ritual is full.`, `Connection rejected.`, or `Connection timed out.` The discovery and FishNet client lifecycle remain owned by `RitualSealService`.
- On the persistent `IncantationNetworkManager`, `RitualSealService.directoryLookupTimeout`
  defaults to `5` seconds and covers only Seal discovery. `connectionAttemptTimeout` defaults to
  `15` seconds and covers the resolved Tugboat connection, FishNet global-scene synchronization,
  and owner-assigned local `NetworkPlayer` spawn. Keep both values at or above `1` second.
- A complete-connection timeout stops only the incomplete client attempt, clears the resolved
  ritual, reports `Connection timed out.`, and restores Validate interaction. FishNet stop or
  rejection reports immediately and must not wait for this timeout.
- Standalone builds must start from `Assets/Scenes/Bootstrap.unity`; `MainGame` remains the second
  enabled Build Settings scene. Tugboat begins at UDP port `7770`; Host creation keeps it when
  locally available or selects the next available port in a bounded 100-port range. The
  host machine/firewall must permit that range plus LAN directory UDP port `47742`. The Book
  never displays an endpoint.
- `SetSealText`, `SetPlayerCount`, and `SetPlayerNames` remain presentation hooks for the older right-page lobby layout. TASK-042's Seal state is owned by `RitualSealService`; Steam matchmaking and production player-list ownership remain future work.
- Clearing a right-page entry sets its text to empty, removes its click action, restores its non-hover appearance, and disables only its assigned interaction Collider. The GameObject stays active, and no Collider is moved or resized.

Voice Book page controller:

- Add one `VoiceBookPageController` to the existing Living Book/menu coordination object. Do not add a second microphone component or a second page-transition controller.
- `voiceAmplitudeProvider`: assign the local player's existing `VoiceAmplitudeProvider`. The page reads `CurrentAmplitude`, lists its cached `AvailableDevices`, and calls `SetInputDevice(...)`; it never calls `Microphone.Start` or `Microphone.End` directly.
- `outputDeviceProvider`: assign the `WindowsAudioOutputDeviceProvider` on the same Living Book coordination object. The Voice controller only requests refreshes and reads `CurrentDeviceName`; Windows endpoint discovery stays outside the menu layer.
- The Voice left page contains exactly `Input Device`, `Output Device`, `Microphone Test`, and `Back` on lines 1 through 4. The first three entries select their matching right-page detail without changing `BookState`; Back calls the existing `BookMenuController.ReturnToOptions()` navigation action and uses the current Book transition.
- The right page displays exactly one selected Voice detail at a time. The controller clears every unused right-page text entry, disables its interaction, hides the optional meter GameObject, and then enables only the controls belonging to the selected detail.
- Input Device detail: keeps the current microphone on `TMP_Right1`, clears `TMP_Right2` and `TMP_Right3`, displays `Previous` on `TMP_Right4`, and displays `Next` on `TMP_Right5`. The two navigation controls call `SelectPreviousInputDevice()` and `SelectNextInputDevice()` and apply the result immediately through `VoiceAmplitudeProvider.SetInputDevice(...)`.
- Output Device detail: displays `WindowsAudioOutputDeviceProvider.CurrentDeviceName`. It refreshes when the Voice page opens, when Output Device is selected, and when the provider receives `AudioSettings.OnAudioConfigurationChanged`. This panel is display-only and does not change Unity or operating-system audio routing.
- Microphone Test detail: displays only the live meter text and `microphoneLevelMeter`, when assigned. Both read `VoiceAmplitudeProvider.CurrentAmplitude` every frame only while this detail is selected. Neither starts nor stops recording.
- Keep the meter GameObject under the existing Living Book UI. `microphoneLevelMeter` is shown only for Microphone Test. Do not create it from code and do not place gameplay scripts on it.

Windows audio output device provider:

- Add one `WindowsAudioOutputDeviceProvider` to the existing Living Book coordination object and assign it to `VoiceBookPageController.outputDeviceProvider`.
- `CurrentDeviceName` is a public read-only property containing the friendly name of the current Windows default multimedia rendering endpoint, such as `Headphones (SteelSeries Arctis Nova 7)`.
- On Windows Player and Windows Editor builds, the provider uses the operating system's built-in Core Audio COM interfaces to read the default render endpoint and its `PKEY_Device_FriendlyName` property. No NAudio or other external package is required.
- The provider subscribes to `AudioSettings.OnAudioConfigurationChanged`, refreshes its cached name after Unity audio configuration changes, and emits `DeviceNameChanged` only when the displayed name actually changes. It does not poll or log warnings every frame.
- Detection failures and all non-Windows builds safely expose `System Default`. Windows COM declarations are excluded from non-Windows compilation.
- This provider never selects an endpoint, changes the Windows default, or changes Unity audio routing.

Book text transition controller:

- Add one `BookTextTransitionController` to the existing Living Book/menu coordination object. Do not create, move, reparent, or replace any TextMeshPro objects; callers pass the existing manually placed entries into the controller at runtime.
- `audioSource`: assign one existing, dedicated AudioSource for Book paper transitions. Keep `Play On Awake` disabled and `Loop` disabled. The controller does not create an AudioSource or change its mixer routing, volume, spatial settings, or other authored configuration.
- `paperTransitionClip`: after manually importing `makigai_maimai-paper-245786.mp3`, assign that clip here. A coordinated Book page transition calls `PlayOneShot` once. Missing AudioSource or clip references suppress only the sound; the text animation still runs.
- `disappearDuration`: `0.25` seconds. Old text loses visible characters from end to beginning using `TMP_Text.maxVisibleCharacters`. `Prototype tuning`.
- `delayBetweenPhases`: `0.05` seconds between disappearance and rewrite. `Prototype tuning`.
- `revealDuration`: `0.9` seconds. New text reveals from beginning to end. `Prototype tuning`.
- `revealCurve`: use the default linear curve for an even writing pace, or author an easing curve without changing page state logic. The curve affects only the reveal phase. `Prototype tuning`.
- All timing uses unscaled time, so menu rewriting continues when gameplay time scale is paused.
- During a transition, affected `BookMenuItem` Colliders are disabled and hover visuals return to their authored baseline. Click actions are prepared before the rewrite but cannot fire until the reveal completes. After completion, non-empty entries become interactive and empty entries remain disabled, preventing invisible Colliders from intercepting input.
- A newer transition cancels and replaces the active animation. It restores complete visibility before starting the replacement and stops the dedicated transition AudioSource before playing the new clip, preventing stacked animations and overlapping paper sounds.
- Do not attach `BookTextMagicEffect` to clickable Book menu entries. `BookTextTransitionController` is the only rewrite animation for those entries and preserves their existing `BookMenuItem` hover behavior.

Character Book page controller:

- Add `CharacterBookPageController` to the existing Living Book/menu coordination object. It owns only the active Character subcategory presentation; `BookStateController` remains the global Book-state owner.
- `rightPageController`: assign the same `BookRightPageController` used for Host and Join contextual content.
- `bookMenuController`: assign the existing `BookMenuController`. `CharacterBookPageController.ShowCharacter()` delegates the camera request to `BookMenuController.ShowCharacter()` and does not move a camera directly.
- Configure `colorOptions` as the active placeholder text array. Its first four entries fill right lines 1 through 4 when `Color` is selected. The dormant horn, hat, and tattoo arrays and methods remain available for future use but do not appear on the current Character page. These strings do not apply cosmetics, check inventory, save data, or network selections.
- Assign the player character's Hair `CharacterSelectionGroup` to `hairSelectionGroup`. Its ordered entry display names populate all five right-page lines when Hair is selected, and each line immediately calls `Select(index)` on that group.
- Entering `CharacterMenu` leaves the camera on the Book. The left page displays `Color`, `Hair`, `Beard`, and `Back` on lines 1 through 4. Hair is functional; Beard remains a placeholder with no action.
- The default Character right page displays `CHARACTER`, `Select Color`, and the existing `Show Character` action. Selecting `Color` changes only the independent Character right page to the configured color placeholder texts; right line 5 remains `Show Character`.
- `Show Character` calls `BookMenuController.ShowCharacter()`, which validates `cameraTransitionManager` and `characterCameraTarget`, then moves the existing active menu camera to the authored Character viewpoint. It does not change Book state or modify player customization.
- `Color`, `Back`, and every right-side entry keep their own manually placed `BookMenuItem` and `BoxCollider`. Do not duplicate, share, resize, or reposition Colliders through scripts.
- Returning to `MainMenu` changes only Book state. Camera return remains an explicit action and is not coupled to the Back state transition.
- Hair selection updates the configured local character immediately. Cosmetic inventory, persistence, lobby integration, and networking are not implemented.

Character selection group:

- Attach `CharacterSelectionGroup` to the character that owns the mutually exclusive cosmetic objects.
- Configure its reorderable `entries` list with a display name and optional target GameObject for each choice. A targetless first entry represents options such as Bald.
- Keep each group focused on one category. Add separate components later for Beard, Horns, Face Paint, or Scars rather than mixing categories into one list.
- `Awake()` applies the serialized current selection. `Select(index)` disables every configured target before enabling only the selected target; selecting a targetless entry leaves every target disabled.

Network Ready presentation:

- No new Inspector reference is required. `NetworkPlayer` owns the synchronized Ready state.
- `BookStateController` uses `NetworkPlayer.LocalPlayer` for the Ready/Unready action and
  `NetworkPlayer.ReadyCircleMemberCount` for the Ready numerator. The Host status denominator is
  `NetworkPlayer.CircleMemberCount`, so disconnected, destroyed, and never-joined players are
  excluded. The joined-client counter retains its existing maximum-capacity presentation.
- Keep the existing `lobbyPlayerStateController` reference for local Seat-page presentation
  during migration. It does not own, toggle, or count multiplayer Ready state.
- Do not wire Ready UI directly to `LobbyPlayerStateController.TryReady()` or `TryUnready()`.
  The Book action must call the owner request on `NetworkPlayer`.
- Ready presentation refreshes from `NetworkPlayer.CircleRosterChanged`; do not add Update
  polling, hierarchy searches, serialized counts, or a second roster.
- On the seated Circle page, an authoritative Host sees right title `RITUAL STATUS`. An
  incomplete Ready check displays `Waiting for Priests to Ready Up` on right line 1, leaves
  right line 2 empty, and displays `Ready: X / Y` on right line 3 with no Start action. A
  non-empty fully Ready Circle displays `All Priests are Ready` on right line 1, keeps the same
  counter on right line 3, and exposes `Start Ritual` only on right line 5 through
  `BookMenuController.StartRitualFromBook()`.
- Joined clients never receive the Start action. A Ready client continues to display
  `Waiting for Host to Start the Ritual...`; its existing Ready/Unready interaction remains
  owned by its local `NetworkPlayer`.
- Ready changes, joins, disconnects, reconnects, and late joins refresh the status, counter, and
  Start visibility directly through `NetworkPlayer.CircleRosterChanged`. These synchronized
  content changes never replay the Book page transition.
- Synchronized appearance remains presented by `NetworkCharacterPresentation` through
  `NetworkPlayer.AppearanceSlotChanged`; it does not request a Book page change or transition.
- Reconnect defaults to Not Ready. No Inspector default or saved Book text may override it.

Authoritative ritual start:

- No new Inspector reference is required. The existing scene `BookMenuController.lobbyController`
  assignment remains the local handoff target on every peer.
- `Start Ritual` is presentation only until the Host-owned `NetworkPlayer` submits its
  `RequestRitualStart()` request. Do not wire the Book action directly to
  `LobbyController.StartLobbyRitual()`.
- The server accepts the request only from its local Host connection while
  `FishNetFoundationController.IsHostRunning` is true and every member of the current non-empty
  synchronized Circle roster is Ready.
- The accepted request is sent to all observers through one FishNet ObserversRpc. Every peer then
  calls the same `BookTextModeController.ShowRitualTexts()` and
  `LobbyController.StartLobbyRitual()` path. Do not add a second ritual initialization,
  Book-closing, timer, incantation, or gameplay-start path.
- The start RPC is not buffered. Players who join after gameplay has started are not introduced
  into the current gameplay flow by this handoff.

Book menu controller:

- Add `BookMenuController` to the existing scene object that owns menu coordination. Do not create a second Book, camera, or menu UI object for this component.
- `bookStateController`: assign the `BookStateController` on the Living Book coordinator.
- `bookTextModeController`: assign the same `BookTextModeController` used by `BookStateController`.
- `cameraTransitionManager`: assign the scene `CameraTransitionManager` that owns `MenuTransitionCamera`.
- The former `BookMenuController.lobbyCameraTarget` value remains serialized but hidden for scene compatibility. It is not read at runtime; `LobbyController` owns the active Seat-selection camera target.
- `characterCameraTarget`: assign the authored Character viewpoint Transform. `ShowCharacter()` uses this target; `OpenCharacter()` does not move the camera.
- `bookMenuCameraTarget`: assign the authored Book Menu viewpoint Transform.
- `lobbyController`: assign the scene `LobbyController`. `OpenCirclePage()` changes only Book state. `BeginSeatSelection()` delegates the single physical Seat-selection entry to `LobbyController`; camera and Seat-selection activation are not duplicated in `BookMenuController`.
- `discordUrl`: optionally assign the full Discord destination URL. `OpenDiscord()` logs `Discord URL is not assigned.` and does nothing when this field is empty; otherwise it passes the assigned value to `Application.OpenURL(...)`. No Discord URL is hardcoded.
- `StartRitualFromBook()` requires `lobbyController.HasSelectedLobbySeat()` before requesting
  anything. If no Seat is selected, it logs `Cannot start ritual: the local player has not
  selected a seat.` and does not switch text, cameras, interaction, or ritual state. With a
  selected Seat, it asks the local Host-owned `NetworkPlayer` for an authoritative start.
  `HandleAuthorizedRitualStart()` is the only response path: after the server broadcast reaches
  each peer, it calls `BookTextModeController.ShowRitualTexts()` and delegates to
  `LobbyController.StartLobbyRitual()`.
- `OpenHostMenu()` remains as a serialized compatibility entry point but now delegates to `CreateNetworkRitual()` instead of displaying the obsolete pre-creation Host page.
- `QuitHostedRitual()` is destructive and distinct from Back. It delegates shutdown and Seal cleanup to `RitualSealService.QuitHostedRitual()`, disables local Seat selection, returns the camera to the Book, and restores `PlayMenu` with Create/Join actions.
- Wire visible `Quit Ritual` actions to `BookMenuController.QuitRitual()`. It dispatches Hosts
  to the existing hosted shutdown and joined clients to client-only leave. Client leave never
  releases the remote Host Seal or stops a server.
- `OpenCharacter()` only displays `CharacterMenu`, keeping the camera on the Book. `ShowCharacter()` is the explicit Character-camera transition. `OpenOptions()` displays `OptionsMenu`, `OpenVoiceOptions()` displays `VoiceMenu`, and `ReturnToOptions()` returns from Voice to Options without moving a camera.
- `OpenLeaderboard()` is a placeholder that logs `Leaderboard is not implemented yet.`. `OpenDiscord()` opens only the URL authored in `discordUrl`.
- `ReturnToBookMenu()` calls `BookTextModeController.ShowMenuTexts()` and requests movement to `bookMenuCameraTarget`; the physical Book return interaction may keep its existing transition wiring for now.

Book text mode controller:

- Add `BookTextModeController` to the existing Living Book/menu coordination object. It only toggles two manually authored text roots; it does not create, move, rename, or restyle TextMeshPro objects.
- `menuTextRoot`: assign `BookMenuTextRoot`, containing all Living Book menu TMP objects.
- `ritualTextRoot`: assign `RitualTextRoot`, containing the existing ritual/incantation TMP objects.
- On `Awake`, the component calls `ShowMenuTexts()` so the authored menu text is the initial mode before other startup behavior runs.
- `ShowMenuTexts()` activates `menuTextRoot` and deactivates `ritualTextRoot`. This is the startup and return-to-Book-menu mode.
- `ShowRitualTexts()` deactivates `menuTextRoot` and activates `ritualTextRoot`. This runs only after the local lobby player has selected a Seat and immediately before the existing lobby ritual-start flow.
- Create or identify the two roots manually in the Inspector and parent the appropriate existing TMP objects manually. Do not use scripts to move existing TMP transforms.

Book menu item wiring:

- Keep one `BookMenuItem` on each of the four already placed line objects and preserve its existing `TextMeshPro`, hover color, hover scale, and Collider setup.
- `interactionCollider`: assign the line object's existing Collider. When the Collider is on the same GameObject as `BookMenuItem`, the component caches it automatically and no new Inspector assignment is required. Assign this field manually only when the intended interaction Collider is on a different GameObject.
- `enableHoverDebugLogs`: keep `false` normally. Temporarily enable it on a specific entry to log mouse enter, exit, click, interaction changes, baseline refreshes, Collider state, text, scale, and color.
- Assign those four components to the matching `menuItem1` through `menuItem4` fields on `BookStateController`.
- Page actions are replaced at runtime by `BookStateController`; no manual per-page UnityEvent listeners are required. Existing persistent On Click wiring should be removed in the Inspector so the initial serialized setup is unambiguous before Play Mode.
- Do not wire a `BookMenuItem` directly to `CameraTransitionManager`, lobby gameplay, ritual logic, or seating. `BookMenuItem` remains a hover/click relay and Book actions route through `BookMenuController`.
- `BookMenuItem` captures one stable authored color and scale during `Awake`. `SetInteractionEnabled` and ordinary Book-state refreshes never overwrite that authored baseline.
- `SetInteractionEnabled(false)` restores the authored non-hover visual and disables only Collider hit testing for empty entries. `SetInteractionEnabled(true)` restores the authored visual and reapplies hover only when the item is currently hovered.
- State refreshes call `RefreshVisualBaseline()` for visible left- and right-page entries. Despite its compatibility name, this method now restores the stable authored baseline without recapturing current runtime color or scale.
- `CaptureAuthoredBaseline()` is the explicit opt-in API for another system that deliberately changes an entry's normal authored style. Do not call it during ordinary page or hover refreshes.
- `BookMenuController.ReturnToBookMenu()` is exposed for UnityEvents but does not need to replace the current physical Book return interaction in this task.

Book return interaction:

- While `LocalInputContextGate` reports `BookInteraction`, the physical Book ignores hover and click callbacks. Existing Book UI colliders remain active and unaffected. Moving the camera away from the registered Book target restores physical Book hover and click automatically.
- `BookMenuReturnInteractable` lives at `Assets/scripts/Book/BookMenuReturnInteractable.cs`.
- Add it to the existing physical Book object that should be clickable from the Lobby camera view. Do not add it to `BookGhost`, and do not create a second gameplay book.
- The Book must have an existing manually placed `BoxCollider` or other appropriate Collider on the same clickable object so Unity mouse events can reach the component. Size and position this Collider by hand in the scene; the script does not create, resize, enable, or repair Colliders.
- `cameraTransitionManager`: assign the scene `CameraTransitionManager`.
- `bookMenuTarget`: assign the authored Book Menu viewpoint Transform, such as the existing `BookMenuCamera` or its target Transform. Do not modify that Transform's position or rotation for this wiring task.
- `highlightRenderers`: assign the visible Book/BookModel Renderer components that should receive the hover glow. Leave unrelated room, player, hourglass, and UI renderers out of this list.
- `hoverMode`: keep `Automatic` by default. `Automatic` uses `_EmissionColor` when the assigned Renderer material supports active emission, otherwise it falls back to brightening `_BaseColor`. Use `Emission` only when the material emission path is already enabled and visible; use `BaseColor` for the most reliable URP hover readability.
- `hoverEmissionColor`: choose a subtle book-appropriate glow color.
- `hoverEmissionIntensity`: keep near `0.35` for the current prototype unless tuning the hover readability. `Prototype tuning`.
- `hoverBaseColorMultiplier`: keep near `1.2` for a small temporary brighten when emission is unavailable or disabled. `Prototype tuning`.
- `hoverLight`: manually create and position a Light for the existing physical Book, then assign that Light here. Keep the Light intensity at `0` when not hovered. Do not create or configure the Light at runtime.
- `hoverLightIntensity`: `1.5`. Target intensity reached while the mouse is over the Book. `Prototype tuning`.
- `hoverLightFadeDuration`: `0.12` seconds. Fade time for both entering and leaving hover; the fade uses unscaled time. `Prototype tuning`.
- `interactionEnabled`: keep `true` when the Lobby view should allow clicking the Book to return to the Book Menu camera.
- `EnableInteraction()` allows hover and click handling for Book Menu and Lobby navigation. `LobbyController.ShowLobby()` calls it whenever lobby/menu state is established.
- `DisableInteraction()` blocks hover and clicks, immediately restores the original material state, and fades `hoverLight` to `0`. `LobbyController` calls it after applying the selected Seat and before starting the ritual.
- Hover feedback uses `MaterialPropertyBlock` and restores each Renderer to its original property-block state on exit or disable. Shared Materials are not modified.
- The optional material hover and `hoverLight` work independently. `highlightRenderers` may be left empty when only the Light effect is wanted, and `hoverLight` may be left unassigned without affecting material hover or click interaction.
- If assigned materials do not support visible emission, the hover effect falls back to `_BaseColor` when available. If neither supported property exists, material hover feedback is skipped gracefully and click interaction still works.

Guidance:

- Keep the table, book, active player, phrase display, and hourglass readable.
- Do not add camera behavior that implies characters walk around.
- Mark future camera values here once intentionally tuned.

## Spell Hand Visual Foundation

Add one `SpellHand` child beneath each authored Player that needs the local presentation prototype. Create three table slot Transforms, three raised pose Transforms, and one inspect pose Transform. Add `SpellHandController` to the `SpellHand` root.

- Temporary spell-hand gameplay shortcuts run only in the local `Gameplay` input context. While the local camera controls the Book, `E`, card selection, visibility, and consume shortcuts are ignored without disabling Book UI keyboard input.
- `NotebookInput` consumes the same local gameplay gate, preventing its `E` shortcut from competing with Book UI interaction while preserving its existing behavior in `Gameplay`.
- `cardViews`: assign exactly three authored child cards in left, center, right order. Each card requires `SpellCardView`.
- `cardSlots`: assign exactly three table resting poses in the same order.
- `raisedPoses`: assign exactly three reading poses in the same order. These poses provide the base fan layout.
- `inspectPose`: assign the shared close-reading pose. If omitted, selection offsets from that card's raised pose.
- `animationDuration`: `0.35` seconds. `Prototype tuning`.
- `cardLiftHeight`: `0.05` world units added above each raised pose. `Prototype tuning`.
- `fanAngle`: `6` degrees between adjacent cards. `Prototype tuning`.
- `selectionOffset`: `(0, 0, 0.08)` in inspect/raised-pose local space. Confirm the sign points toward the seated player's camera for the authored pose orientation. `Prototype tuning`.
- `selectionScale`: `1.08`. `Prototype tuning`.
- `openCurve`, `closeCurve`, `consumeCurve`: author smooth Inspector curves; defaults are ease-in/ease-out.
- `consumeDuration`: `0.2` seconds. `Prototype tuning`.
- `visibleOnAwake`, `openOnAwake`: leave disabled for lobby-first setup; invoke the public presentation methods explicitly later.
- `enableDebugInput`: enables temporary `H` visibility, `E` open/close, `1`/`2`/`3` selection, and `Space` consume controls. Disable when production input owns these calls.

Create card data through `Assets > Create > Incantation > Spell Definition`:

- `stableIdentifier`: enter a durable lowercase identifier such as `petit_fantome`; keep it separate from the asset filename and display name.
- `displayName`, `spokenIncantation`, `description`: enter designer-approved player-facing content. Do not invent unfinished incantations.
- `rarity`: presentation-only `Common`, `Uncommon`, `Rare`, `Epic`, or `UltraRare`.
- `cardArtwork`, `audioClip`, `visualPrefab`: optional references. Audio and visual prefabs are stored only and are not played or instantiated.
- `overrideRarityGlowColor`: disabled uses the default white, green, blue, purple, or bright-red rarity color. Enable it to author a per-definition override.

On each `SpellCardView`:

- `definition`: assign a `SpellDefinition` directly for Inspector testing. Future hand management may call `SetDefinition(SpellDefinition)` without changing this view.
- Assign the card mesh Renderer and name/description/incantation TMP components where available.
- Assign either `artworkImage` for Canvas UI artwork or `artworkRenderer` for a SpriteRenderer target. Both are optional and safely hide when the definition has no artwork.
- `glowLight`: assign a real Unity Light parented to or positioned with the physical card. Leave the Light disabled in the authored setup; `SpellCardView` controls it.
- `selectedLightIntensity`: author the selected-card illumination strength for the scene and render pipeline.
- `selectedLightRange`: author a range large enough to reach the seated player's face, table, nearby objects, and Book without washing out the full room.
- `fadeInDuration`: start at `0.25` seconds for a smooth magical awakening. The fade begins from the Light's current intensity. `Prototype tuning`.
- `fadeOutDuration`: start at `0.15` seconds for a quicker release after deselection or hand close. The Light disables only after reaching zero. `Prototype tuning`.
- `fadeInCurve`, `fadeOutCurve`: use the default ease-in/ease-out curves, then tune the response without changing selection logic.
- `enableSelectedFlicker`: disabled by default. Enable only when a subtle living-light effect improves the selected card.
- `flickerAmount`: start near `0.05` intensity units. Keep this restrained. `Prototype tuning`.
- `flickerSpeed`: start near `1.5`. Flicker uses layered deterministic sine waves and begins only after fade-in completes. `Prototype tuning`.
- The assigned definition's rarity glow color drives `glowLight.color`. A visible defined card fades toward full intensity when selected and fades to zero when deselected or the hand closes.
- Consumption, hand hiding, component disable, inactive GameObjects, null definitions, and missing Light references cancel transitions and shut down immediately. Each card owns at most one Light transition coroutine, and replacements continue from current intensity.
- Assign the optional world-space Canvas.
- Missing optional visual references are supported. A null definition clears card text/artwork and disables the rarity Light without affecting the hand animation.

The approved card names do not all have finalized spoken incantations, so no production `SpellDefinition` assets are created automatically by this task. Designers can create and assign definitions after approving their text.

This foundation and its definition data must remain independent from `RitualController`, `BookMover`, `IncantationManager`, `Timer`, `SeatManager`, spell effects, voice activation, inventory, drawing, and networking.

## Audio

Current documentation gap:

- Mixer group routing is not yet documented or apparently finalized.

Guidance:

- Preserve ambient audio as mood support.
- Avoid audio changes that obscure voice recognition feedback or player understanding.
- Record intentional mixer and volume decisions here when production audio mixing begins.
