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
- Scene purpose: local seated ritual prototype.
- Production lobby, networking, and elimination are not implemented yet.

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
- `allowMultipleDebugOccupants`: `false` unless intentionally testing multiple local debug seats.
- `enableDebugLogs`: `false` by default.

Notes:

- The configured physical order is authoritative.
- Do not rely on hierarchy order, seat numbering, player join order, or network index.

## Seat

Each Seat is logical. Chair meshes are visual.

Intentional references:

- `PlayerSpawn`: where a seated local/debug player is placed.
- `BookTarget`: gameplay destination when used by the current setup.
- `BookGhost`: visual/editor placement reference.
- `LookTarget`: seated attention reference.
- `LeftHand` and `RightHand`: seated hand references.
- `ChairClickZone`: seat interaction/debug selection.

Notes:

- `Seat.GetBookDestination()` currently prefers `BookGhost` when present, otherwise `BookTarget`.
- `BookGhost` must never contain gameplay scripts.

## RitualController

Current `MainGame` setup:

- `seatManager`: assigned.
- `bookMover`: assigned to the single real book mover.
- `bookController`: assigned when present on the book.
- `hourglassController`: assigned.
- `incantationManager`: assigned.
- `incantationTextDisplay`: assigned.
- `coreRitualLoopBridge`: assigned when available.
- `voiceRecognizerBehaviour`: assigned to `WindowsKeywordVoiceRecognizer` for the default prototype feel.
- `voiceValidationMode`: `WordByWordRealtime`.
- `voicePhraseNormalizer`: assigned.
- `speechAliasWordLibrary`: assigned to the ritual word library.
- `autoStart`: `true` for local Play Mode prototype.
- `hourglassDuration`: `30` seconds. `Prototype tuning`.
- `ritualAcceptancePauseSeconds`: `0.75` seconds. `Prototype tuning`.
- `enableLearningMode`: `false` unless intentionally collecting speech aliases.
- `enableDebugLogs`: `false` by default.

Notes:

- `WordByWordRealtime` is the default prototype mode.
- `FullPhrase` is optional strict mode.
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

Current documentation gap:

- No single authoritative camera Inspector profile has been confirmed in documentation.

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
