# Project Status

This document describes the current reality of Incantation.

Purpose: give contributors a concise status snapshot that is rewritten whenever the project changes.

Questions answered here:

- What version or milestone is the project currently in?
- What works now?
- What is experimental, incomplete, or paused?
- What is the recommended next task?
- What known bugs or technical debt matter today?

This document does not contain historical progress notes, long-term design philosophy, milestone archives, or detailed architecture.

Read next: `Docs/NEXT_TASK.md` for the immediate objective, then `Docs/Roadmap.md` for milestone order.

## Current Version

Incantation is currently a v0.1 playable local prototype of the core seated ritual loop.

## Current Branch

Unknown from documentation. Confirm with `git branch --show-current` before branch-specific work.

## Current Milestone

Networked lobby migration.

The FishNet networking foundation, permanent `NetworkPlayer` architecture, initial runtime validation, and first Book-driven Ritual Creation flow are complete. The next milestone is to migrate the remaining lobby state so `NetworkPlayer` becomes its authoritative multiplayer source.

## Current Goal

Continue migrating lobby systems so `NetworkPlayer` becomes the authoritative source of multiplayer lobby state.

The core vision has not changed: one cursed book, one table, seated players, an hourglass, voice pressure, betrayal, tension, laughter, and memorable social moments.

The book is the main character.

## Current Stable Systems

The current prototype includes:

1. One cursed book.
2. Physical seat traversal.
3. Growing incantation by full active table rotation.
4. Local debug seat occupants.
5. Local lobby foundation in `MainGame`.
6. Explicit Start Ritual handoff from lobby into the existing ritual path.
7. `WordByWordRealtime` validation as the default prototype mode.
8. `FullPhrase` validation as an optional strict mode.
9. `WindowsKeywordVoiceRecognizer` for immediate realtime keyword validation.
10. Whisper retained for full-phrase or experimental recognition paths.
11. Isolated three-card Spell Hand visual foundation with hidden, table, raised, selected, and consumed presentation states.
12. `SpellDefinition` ScriptableObject data for card identity, text, rarity, and optional presentation references.
13. Realtime visual word absorption.
14. Wrong word rejection feedback.
15. Hourglass timer pressure.
16. Book movement after ritual acceptance.
17. Failed-seat elimination after the absorption, book aftermath, and Book Prison handoff completes.
18. Automatic ritual continuation with the remaining alive occupied seats.
19. Ambient audio.
20. Fire flicker.
21. Hourglass light possession effect.
22. Room veil and dark cabin ambience.
23. FishNet `4.7.2` networking foundation.
24. Dedicated Bootstrap scene.
25. One persistent `NetworkManager`.
26. Tugboat transport configured for local diagnostics.
27. Build Profiles configured for the Bootstrap and gameplay scene flow.
28. Networking runtime validation completed.
29. One persistent, owner-assigned `NetworkPlayer` for every connected player.
30. `NetworkPlayer` as the networking identity of every connected player.
31. Replicated `NetworkPlayer` state for Priest Name, Lobby Player State, Ready State, and High Priest.
32. Replicated, server-authoritative `NetworkPlayer.SeatId` assignment with one player per Seat.
33. Automatic FishNet global-scene transition from Bootstrap into `MainGame`.
34. Independent character presentation for every observed `NetworkPlayer`.
35. Book-driven creation and joining through four-character Ritual Seals for Tugboat LAN diagnostics.
36. Server-authoritative player appearance data with local per-slot presentation and late-join synchronization.
37. Server-authoritative Ready toggling and a synchronized Circle Ready counter.

## Ritual Creation

`Bootstrap` now opens `MainGame` as the pre-connection Book interface while preserving the
persistent FishNet manager. The Book's Play page offers `Create Ritual` and `Join Ritual`.
Creating starts the host, generates and displays an uppercase four-character Seal, and waits
in the lobby without starting ritual gameplay. Joining accepts and normalizes a Seal, resolves
the matching LAN host through `RitualSealService`, and supplies the resolved address to FishNet
without displaying it to the player.

Selecting `Create Ritual` begins one genuine Book navigation transition; the redundant
pre-creation confirmation remains bypassed. Host creation starts from the transition's
hidden-content callback, after the Play text disappears and before destination targets are
assigned. Fast authoritative success therefore reveals the final Host lobby directly. If
FishNet startup remains pending, the destination reveals `Creating Ritual...`, then replaces
it silently once the Seal exists and both local server and client are Started.
Its left page contains `Enter the Circle`, `Invite a Priest`, and `Quit Ritual`. The right page
reads the authoritative `RitualSealService.ActiveSeal` and synchronized
`NetworkPlayer.CircleMemberCount`, displaying only the Seal, `X / 8`, and a count-based
waiting message. Roster changes silently refresh those existing entries. `RitualCreated` remains
in the enum only for serialized compatibility and redirects to `HostMenu`.

Repeated Host creation uses the same silent presentation path as initial creation. On shutdown,
`RitualSealService.Changed` performs the one intentional `HostMenu` to `PlayMenu` navigation.
`BookMenuController.QuitHostedRitual()` no longer repeats `ChangePage(PlayMenu)` in the same
frame. That redundant same-state call previously refreshed and re-enabled Create while the Quit
transition still displayed Host content, allowing a new Host success to rewrite content during
the active Play reveal. Create remains unavailable until the Quit transition completes, after
which later Host success updates the existing page silently.

`Enter the Circle` now separates Circle membership from physical Seat selection. It navigates
to the existing `THE CIRCLE` Book page while keeping the Book camera and `BookInteraction`
context active. `Take My Seat` is the sole action that enables Seat interaction and moves the
local menu camera to the Lobby viewpoint. A player can therefore remain an unseated Circle
member, review Book options, and continue contributing to the authoritative Circle count.
The Circle exit action uses authoritative `RitualSealService` role state. A Host sees `Back`,
which returns to the active Host lobby without disconnecting or changing membership. A joined
non-Host sees `Quit Ritual`, which uses the generic role-aware dispatcher to leave only that
local client and return to Play. The Host lobby's separate `Quit Ritual` remains destructive:
`RitualSealService` clears the Seal and pending state, uses
`FishNetFoundationController.Disconnect()` to stop both local client and server, and the Book
restores the Play page. Connected clients follow FishNet's existing disconnect/despawn cleanup
and cannot retain the stopped Host's Circle roster entry.

The current Seal directory is deliberately a Tugboat LAN diagnostic implementation. It is not
authentication and does not replace the planned Steam lobby metadata or external production
directory. The diagnostic FishNet HUD remains available for debug mode only.

Selecting `Join Ritual` now opens the focused four-character Seal form immediately. The left
page contains only the `JOIN RITUAL` title and dedicated `Back` action. On the right page, only
the Seal field and enabled `Validate Seal` button are interactive; title and status labels are
presentation-only. Uppercase normalized input, button state, and concise join status refresh
silently without replaying the Book page transition. This presentation remains a consumer of
`RitualSealService`; discovery and FishNet connection behavior are unchanged.
Join Seal entry now holds the process-local `TextEntry` input context until it exits. Normal
Book, movement, page, and spell-card shortcuts cannot react to Space, E, H, or typed Seal
characters; Enter confirms once and Escape cancels once. Page changes, successful exit,
cancellation, Book close, controller disable, and connection-loss navigation release the
context so normal Book interaction is restored.

## Networking Foundation

The following foundation milestones are complete:

- FishNet is installed.
- The Bootstrap scene is configured.
- One persistent `NetworkManager` owns the networking lifecycle.
- Tugboat is configured as the current diagnostic transport.
- Build Profiles are configured.
- Runtime validation is complete.

## NetworkPlayer

One persistent `NetworkPlayer` exists for every connected player.

`NetworkPlayer` is the networking identity of every player and should become the authoritative multiplayer source for lobby and gameplay consumers wherever possible.

The replicated state currently includes:

- Priest Name.
- Lobby Player State.
- Ready State.
- High Priest.
- Seat assignment.
- Appearance slot/value choices.

`NetworkPlayer.SeatId` is the single authoritative multiplayer Seat assignment. `SeatManager`
maps the configured clockwise physical order to stable zero-based Seat IDs and resolves
occupancy by reading active `NetworkPlayer` instances. `NetworkCharacterPresentation`
observes one `NetworkPlayer`, owns exactly one local visual character for it, and moves only
that character when its synchronized Seat ID changes. The owning player reuses the existing
scene character so character preview, local camera, offline mode, and debug mode remain
compatible; non-owning players receive independent visual instances with local camera and
look input disabled. Character appearance uses a server-owned FishNet SyncList of compact
slot/value entries. `CharacterAppearancePresentation` applies initial snapshots and changed
slots through the existing customization components without synchronizing visual objects.

Ready state is server-owned on each `NetworkPlayer`. Owners request a toggle, the server validates
Circle membership, and every peer calculates the Book counter from synchronized Circle members
whose Ready state is Ready. Late join, disconnect, and reconnect follow the `NetworkPlayer`
replication lifecycle; reconnect starts Not Ready.

The seated Circle page presents readiness by local ritual role. The Host sees `RITUAL STATUS`,
`Waiting for Priests to Ready Up`, and `Ready: X / Y`, where `Y` is the current connected
Circle-member count rather than maximum capacity. When the non-empty Circle is fully Ready, the
message becomes `All Priests are Ready` and `Start Ritual` appears as the final right-page
action. Any Ready, join, or disconnect change recalculates the condition and silently removes
or restores that action. Joined clients never receive Start authority; a Ready client continues
to see `Waiting for Host to Start the Ritual...`. The action still delegates to the existing
Book-to-`LobbyController.StartLobbyRitual()` flow without changing gameplay startup.

Priest Name is likewise server-owned on each `NetworkPlayer`: owners submit validated names by
ServerRpc and all peers consume its SyncVar, including spawn state for late join. The Circle Book
supports a silent right-page editor and no longer displays the unused High Priest label.
The Host can edit its active Seal through a best-effort LAN collision window; the old Seal stays
active until validation succeeds, and FishNet connections are not restarted.

`Quit Ritual` is role-aware. Hosts retain the existing Seal-release plus server/client shutdown
path. Joined non-hosts clear only their local join state, stop their local FishNet client, and
return to Play while the remote Host and other clients continue.

## Runtime Validation

The following networking behaviors have been successfully validated:

- Host startup.
- Client connection.
- `NetworkPlayer` spawning.
- Local player ownership.
- Remote player replication.
- Disconnect.
- Shutdown.

## Network Startup Scene Flow

`Bootstrap` is the dedicated networking launcher. When the server reaches FishNet's Started
state, `FishNetFoundationController` requests one global `MainGame` load through FishNet's
scene manager with `ReplaceOption.All`. The host and server leave Bootstrap, the persistent
network manager survives, and connected or later-authenticated clients automatically follow
the server into the same `MainGame` scene. No gameplay scene transition uses Unity's
`SceneManager` directly.

## Experimental Or Incomplete Systems

- A local lobby player state component still duplicates part of the lobby state during migration. The existing Living Book lobby page observes this local state and refreshes its actions and status text through the existing Book text-transition system. It must be converted to consume authoritative `NetworkPlayer` state.
- `CoreRitualLoop` is the cleaner logic direction, but migration from `RitualController` is incomplete.
- `CoreRitualLoopBridge` mirrors core phrase state into legacy display paths during migration.
- Whisper remains available for full-phrase or experimental recognition paths, but it is not the default realtime path.
- Timeout and retry behavior exist in prototype form.
- Failed players can be eliminated after the Book Prison transition, and the ritual can continue with remaining alive seats.
- The last-player-remaining end-of-game presentation is not implemented yet; the ritual stops with one warning TODO log when only one alive seat remains.
- Debug occupants are useful local testing support, not the final player model.

## Known Missing Product Systems

- Synchronized Lobby UI.
- Removal of duplicated local lobby state.
- Book systems consuming `NetworkPlayer`.
- Multiplayer gameplay synchronization beyond the validated connection and player-identity foundation. Book, ritual, voice, and gameplay state remain unsynchronized.
- Production end-of-game flow for the last surviving player.
- Interference cards.
- Spell Hand gameplay, spell execution, drawing, inventory, voice activation, card replacement, and networking. The visual hand and definition-driven presentation data are present.
- Demon reactions.
- Campaign objectives.

Networked lobby migration is the next major milestone.

## Known Bugs

No active known bugs are documented here today.

When a bug becomes part of the current project state, add it here briefly and remove it when fixed. Historical bug narratives belong in `Docs/LESSONS_LEARNED.md` or `CHANGELOG.md`.

## Technical Debt

- Runtime orchestration is still split between the working `RitualController` surface and cleaner core-loop direction.
- Core-loop turn indexing must eventually reconcile with physical Seat objects while leaving physical order authority in `SeatManager`.
- Legacy incantation display paths still depend on `IncantationManager`.
- `BookController` arrival is duration-based because `BookMover` does not expose a true completion callback.
- Failed-seat elimination still lives in the prototype `RitualController` flow rather than a dedicated production game-mode rules layer.
- Inspector reference coverage is incomplete for some camera and production audio mixer values.
- `LobbyPlayerStateController` still duplicates lobby state while the Living Book flow is preserved; it must become an adapter/consumer of authoritative `NetworkPlayer` state.

## Voice State

The prototype supports two voice validation modes:

- `WordByWordRealtime`: default prototype mode. Uses keyword recognition for immediate visual word validation.
- `FullPhrase`: optional strict mode. Uses full phrase transcript validation.

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.

Whisper is kept in the project but should not be forced as the only validation path.

Do not reintroduce Unity Dictation or Azure.

## Seat State

`SeatManager` owns physical seat order.

The current clockwise order is:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise order is the exact reverse.

Debug occupants are for local testing only.

For connected players, `NetworkPlayer.SeatId` owns Seat assignment. `Seat` occupant fields
remain only for offline/debug compatibility and presentation binding; they are not a second
network Seat authority. `NetworkCharacterPresentation` writes those presentation fields only
for the visual instance owned by its `NetworkPlayer`.

## Phrase State

The phrase starts with 1 word.

Every active player speaks the same visible phrase.

The phrase grows by 1 word after every full active table rotation.

The phrase does not grow after every player.

`SpellPhraseLibrary` is separate from ritual words.

## Hard Boundaries

- Do not create multiple gameplay books.
- Do not make characters walk around.
- Do not move gameplay authority into visual-only objects.
- Do not put gameplay scripts on `BookGhost`.
- Do not modify paused systems unless explicitly requested.

## Recommended Next Task

Read `Docs/NEXT_TASK.md`.

Continue migrating lobby systems so `NetworkPlayer` becomes the authoritative source of
multiplayer lobby state. Synchronized Seat assignment and appearance are complete; the remaining
sequence is Lobby UI, removal of duplicated local lobby state, and transition of Book systems to
read `NetworkPlayer`.

## Last Reviewed

2026-07-25 after sequencing Host creation with the Book transition.
