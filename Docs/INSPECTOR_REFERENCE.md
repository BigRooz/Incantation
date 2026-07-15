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
- While in lobby, `LobbyController` calls `bookMenuReturnInteractable.EnableInteraction()`, calls `cameraTransitionManager.EnableRendering()`, requests `cameraTransitionManager.MoveTo(lobbyCameraTarget)`, and disables only the assigned `localPlayerCamera`. Book interaction therefore remains available while moving between the Lobby and Book Menu views. It does not enable or disable `BookMenuCamera`, `LobbyCamera`, or other menu destination cameras.
- While in lobby, chair clicks are routed through `LobbyController.TrySelectLobbySeat(seat)`, which asks `SeatManager.TryLobbySit(...)` to seat or move `localLobbyPlayer`.
- `SeatManager.TryLobbySit(...)` frees the previous lobby Seat for that same player, keeps the real `localLobbyPlayer` active, moves it to `selectedSeat.playerSpawn.position`, rotates it to `selectedSeat.playerSpawn.rotation`, and occupies the selected Seat. No lobby ghost or duplicate player prefab is created.
- The selected lobby Seat is stored as normal Seat occupancy: `SeatManager.GetLobbySeatForPlayer(localLobbyPlayer)` returns the chosen Seat because `SeatManager.TryLobbySit(...)` occupies that Seat with the real local player.
- `Start Ritual` resolves the selected lobby Seat, keeps using the real local player already occupying that Seat, reapplies that player root to `selectedSeat.playerSpawn`, immediately calls `bookMenuReturnInteractable.DisableInteraction()`, disables lobby seat selection through `SeatManager.SetLobbySeatSelectionEnabled(false)`, hides the lobby canvas, restores disabled gameplay input behaviours, calls `cameraTransitionManager.DisableRendering()`, activates the assigned `localPlayerCamera` GameObject, enables that Camera, asks `RitualController` to prefer that selected Seat as the first active ritual Seat, optionally locks/hides the cursor for gameplay, and calls `RitualController.StartRitual()`.
- `CameraTransitionManager.ActiveCamera`, `EnableRendering()`, and `DisableRendering()` keep menu-camera ownership inside the transition manager. `LobbyController` never searches for or directly identifies `MenuTransitionCamera`.
- The lobby does not generate incantations, start the hourglass, move the book, duplicate ritual initialization, change elimination logic, or create a separate seating system.
- `Options` is a placeholder button for this foundation task only.
- If `localPlayerCamera` is not assigned, `Start Ritual` logs `LobbyController localPlayerCamera is not assigned. Ritual will continue but camera switching will be skipped.` and still starts the ritual.
- If no selected lobby Seat exists when Start Ritual is pressed, the existing ritual seating fallback is used and a warning is logged.

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
- `hourglassDuration`: `30` seconds. `Prototype tuning`.
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

## BookMover

Current `MainGame` setup:

- `moveDuration`: `1.2` seconds. `Prototype tuning`.
- `enableDebugLogs`: `false`.

Notes:

- `BookMover` moves the one real book transform.
- It should not choose traversal, validate phrases, or own turn rules.

## BookController

Current setup:

- `bookMover`: assigned to the same object's `BookMover`.

Notes:

- `BookController` is an adapter around `BookMover`.
- Arrival is currently duration-based using `BookMover.moveDuration`.
- Future improvement: replace duration-based arrival with a true movement completion callback.

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
- `ritualController`: assign the scene `RitualController`. The spectator controller reads `RitualController.CurrentFailedPlayer` after failure and does not search for player objects.
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
- Before activating the assigned slot camera, the controller deactivates every other prison slot camera.
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

- `RitualController` currently starts the hourglass with its own `hourglassDuration` value, currently `30` seconds in `MainGame`.

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

## Camera

Camera transition foundation:

- `CameraTransitionManager` lives at `Assets/scripts/Camera/CameraTransitionManager.cs`.
- `MenuTransitionCamera` is the only rendering camera for menu navigation.
- `BookMenuCamera`, `LobbyCamera`, and future menu viewpoints such as `CharacterCamera` are static destination Transforms only. They must not render during menu navigation.
- The manager moves only the assigned active viewing Camera, currently `MenuTransitionCamera`, between authored camera-viewpoint Transforms. It does not create cameras, delete cameras, rename cameras, change camera references, or move the viewpoint Transforms.
- `transitionDuration`: time in seconds for a smooth camera move. `Prototype tuning`.
- `transitionCurve`: curve evaluated from 0 to 1 across the move. Use it to shape ease-in, ease-out, or theatrical camera travel without changing target Transforms.
- `activeCamera`: assign `MenuTransitionCamera`.
- `MoveTo(target)` and `MoveToImmediate(target)` are the only camera movement API. Menu, lobby, and book interactions pass destination Transforms into this generic mover.
- Expected menu flow: `MenuTransitionCamera` starts at the `BookMenuCamera` Transform, Play requests movement to the `LobbyCamera` Transform, and clicking the physical Book requests movement back to the `BookMenuCamera` Transform.

Book menu controller:

- Add `BookMenuController` to the existing scene object that owns menu coordination. Do not create a second Book, camera, or menu UI object for this component.
- `cameraTransitionManager`: assign the scene `CameraTransitionManager` that owns `MenuTransitionCamera`.
- `lobbyCameraTarget`: assign the authored Lobby viewpoint Transform.
- `characterCameraTarget`: assign the authored Character viewpoint Transform when one is available. Character customization is not implemented; this action currently moves the menu camera only. If the target is unassigned, `OpenCharacter()` logs one clear warning.
- `bookMenuCameraTarget`: assign the authored Book Menu viewpoint Transform.
- `lobbyController`: assign the scene `LobbyController`. `OpenLobby()` asks it to keep the existing Lobby interaction active, then requests the validated camera transition. It does not start the ritual, move the player, or lock seat selection.
- `OpenOptions()` is a placeholder. It only logs `Book options page is not implemented yet.` and does not create UI or modify `LobbyController`.
- `ReturnToBookMenu()` only requests movement to `bookMenuCameraTarget`; the physical Book return interaction may keep its existing transition wiring for now.

Manual Book menu UnityEvent wiring:

- `TMP_Play` > `BookMenuItem` > `On Click`: drag the `BookMenuController` component into the event target and select `BookMenuController.OpenLobby()`.
- `TMP_Character` > `BookMenuItem` > `On Click`: drag the `BookMenuController` component into the event target and select `BookMenuController.OpenCharacter()`.
- `TMP_Options` > `BookMenuItem` > `On Click`: drag the `BookMenuController` component into the event target and select `BookMenuController.OpenOptions()`.
- `TMP_Quit` > `BookMenuItem` > `On Click`: drag the `BookMenuController` component into the event target and select `BookMenuController.QuitGame()`.
- Do not wire a `BookMenuItem` directly to `CameraTransitionManager`. `BookMenuItem` remains a hover/click relay and all Book menu actions route through `BookMenuController`.
- `BookMenuController.ReturnToBookMenu()` is exposed for UnityEvents but does not need to replace the current physical Book return interaction in this task.

Book return interaction:

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

## Audio

Current documentation gap:

- Mixer group routing is not yet documented or apparently finalized.

Guidance:

- Preserve ambient audio as mood support.
- Avoid audio changes that obscure voice recognition feedback or player understanding.
- Record intentional mixer and volume decisions here when production audio mixing begins.
