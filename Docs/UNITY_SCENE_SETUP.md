# Unity Scene Setup

Purpose: record current scene setup expectations for the v0.1 playable prototype.

Questions answered here:

- What scene pieces must exist for the ritual?
- What should Seats contain?
- What scene boundaries must not be crossed?

This document does not contain gameplay law, current task planning, source-code architecture, or permission to modify scenes.

Read next: `Docs/INSPECTOR_REFERENCE.md` for tuning values and `Docs/PROJECT_KNOWLEDGE.md` for scene knowledge.

## Startup Presentation

The player startup presentation is:

1. Unity splash screen.
2. `StudioIntro` on black, playing the One More Game Studio video once.
3. `MainGame` after completion, skip, or safe playback failure fallback.

`StudioIntro` is the first enabled build scene. It also instantiates the existing persistent
network-manager prefab so the initialization previously provided by `Bootstrap` is preserved.
`Bootstrap` remains enabled in the build for compatibility, but is not part of the normal startup
transition.

## Purpose

This document records the current scene setup expectations for the v0.1 playable prototype.

It is documentation only. Do not treat it as permission to modify scenes, prefabs, assets, or scripts.

## Required Ritual Pieces

The current ritual scene depends on:

- One real `BookModel`.
- Seat objects managed by `SeatManager`.
- Per-seat book destinations such as `BookTarget` or `BookGhost`.
- Local debug occupants for Play Mode testing.
- Ritual orchestration components.
- Voice recognizer component assigned for the desired validation mode.
- `HourglassController`.
- Incantation display and feedback components.
- Ambience and lighting components.

## Seat Setup

Each Seat is logical.

The chair is visual.

A Seat may contain:

- PlayerSpawn.
- BookTarget.
- BookGhost.
- LookTarget.
- LeftHand.
- RightHand.
- ChairClickZone.

`SeatManager` owns the physical seat order.

The current clockwise order is:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Do not rely on hierarchy order or sequential seat numbers.

Debug occupants are for local testing only.

## Book Setup

There is one real gameplay book.

`BookModel` is the moving book.

`BookTarget` defines a gameplay destination.

`BookGhost` is an editor or visual placement reference only.

Do not add gameplay scripts to `BookGhost`.

Do not create one gameplay book per player.

## Voice Setup

For the current default prototype feel:

- Use `WordByWordRealtime`.
- Use `WindowsKeywordVoiceRecognizer`.

For optional strict validation:

- Use `FullPhrase`.
- Use a full transcript recognition path such as Whisper when explicitly testing that mode.

Do not use Unity Dictation.

Do not use Azure voice services.

## Atmosphere Setup

The current scene includes ambience and lighting support:

- Ambient audio.
- Fire flicker.
- Hourglass light possession effect.
- Room veil.
- Dark cabin ambience.

These elements support the ritual mood but are not the gameplay authority.

For current recommended Inspector values and prototype tuning notes, read `Docs/INSPECTOR_REFERENCE.md`.

## Current Missing Production Setup

The scene does not yet represent the full production flow.

Still needed:

- Lobby.
- Ready check.
- Automatic seating from lobby players.
- Networking.
- Production end-of-game presentation.

Until those exist, local debug occupants remain a testing aid only.
