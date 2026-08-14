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
38. Server-authoritative ritual start with synchronized gameplay handoff to every connected
    Circle participant.
39. Multiple simultaneous best-effort LAN Ritual advertisements with per-session identity,
    exact-Seal lookup, and isolated FishNet Host ports.
40. A server-owned `NetworkRitualAuthority` scene foundation that replicates a controlled,
    read-only ritual test snapshot without participating in current gameplay.
41. A server-owned immutable ritual roster that maps stable session player IDs to authoritative
    Seat IDs in `SeatManager` physical traversal order, without migrating turn execution.
42. Server-owned active participant selection that advances through the authoritative roster,
    skips inactive or eliminated entries, and publishes active player/Seat state without yet
    driving legacy ritual execution.
43. Server-owned Book movement request and arrival acceptance that validate stable ritual,
    turn, movement, player, and Seat identity while leaving current timer, voice, phrase, and
    consequence gameplay on the legacy path.
44. A server-authoritative ritual timer that starts from accepted Book arrival, synchronizes
    duration/start/deadline state, computes expiration only on the server, and feeds the legacy
    timer as a presentation and temporary callback consumer.
45. Connection-authenticated ritual voice submissions that preserve local microphone recognition,
    reject unauthorized, stale, duplicate, malformed, or out-of-window candidates on the server,
    and publish only accepted text to the authoritative phrase-validation path.
46. Server-authoritative phrase validation for both realtime-word and full-phrase modes, with one
    immutable result per accepted submission and legacy presentation/consequence compatibility.
47. One server-authoritative immutable turn outcome per turn, committed only from phrase-completing
    validation or authoritative timer expiration and consumed by the legacy consequence bridge.
48. One server-authoritative immutable consequence per turn outcome, selected and published only
    by `NetworkRitualAuthority` while `RitualController` remains the temporary presentation and
    legacy-execution bridge.
49. A multiplayer ritual single-writer boundary covering roster, active participant, Book
    command/arrival, timer, voice submission, phrase validation, turn outcome, and consequence.
    Network ritual start no longer launches `RitualController.RitualLoop` on Host or Client;
    `RitualController` remains the unchanged local orchestrator only for offline play.
50. A minimum server-driven network turn and phrase lifecycle: one-word initialization, physical
    roster traversal, Book arrival and timer gating, active-owner voice submission, authoritative
    validation, success advancement, and exactly one phrase word added per completed rotation.
51. Authoritative ritual timer deadlines and remaining-time reconstruction explicitly use
    FishNet's synchronized approximate server tick (`TickType.Tick`) on Host and Client. The
    server remains the sole timer-start and expiration authority; remaining time is still derived
    locally from the synchronized deadline rather than continuously replicated.
52. Physical hourglass sand presentation derives directly from the existing Timer percentage,
    preserves authored pile width, changes height along the model's local Z axis, compensates the
    imported pivots to keep the top neck and lower base anchored, and safely supports an optional
    falling-sand object. Non-expired stopped/reset state returns to the ready presentation.
53. TopSand preserves its authored horizontal footprint through normal depletion, then uses two
    Inspector controls to narrow X/Y smoothly during the final configurable remaining fraction.
    BottomSand, local-Z height, vertical anchoring, and timer authority remain unchanged.

## Ritual Creation

`Bootstrap` now opens `MainGame` as the pre-connection Book interface while preserving the
persistent FishNet manager. The Book's Play page offers `Create Ritual` and `Join Ritual`.
Creating starts the host, generates and displays an uppercase four-character Seal, and waits
in the lobby without starting ritual gameplay. Joining accepts and normalizes a Seal, resolves
the matching LAN host through `RitualSealService`, and supplies the resolved address to FishNet
without displaying it to the player.

Multiple Hosts may advertise independent Rituals on the same LAN. Each advertisement is keyed
by the Host process's stable session GUID and carries that Host's normalized Seal and selected
FishNet endpoint. Discovery retains separate records per Host instead of collapsing the LAN into
one global Ritual. Different Seals coexist; only another live advertisement with the same
normalized Seal conflicts. Join processing may observe multiple responses during its bounded
lookup window, ignores every non-matching Seal, and starts FishNet only for the matching
advertised endpoint.

Before starting FishNet, a Host keeps the configured Tugboat UDP port when it is locally
available or selects the next available port from a bounded local range. Hosts on different
machines may therefore use the same port, while multiple local diagnostic Hosts remain isolated.
Quitting broadcasts a withdrawal for only that Host session before stopping its FishNet server;
unexpectedly stopped advertisements expire from other processes' bounded discovery memory.

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

The current Seal directory is deliberately a best-effort Tugboat LAN diagnostic implementation.
Its duplicate detection is a bounded observation window, not an atomic reservation. It is not
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

- Stable session Player ID.
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

Ritual start is also server-authoritative. `BookMenuController.StartRitualFromBook()` only asks
the local Host-owned `NetworkPlayer` to start. The server rejects requests that do not come from
its local Host connection, confirms FishNet is still running as Host, and evaluates the current
connected Circle roster so disconnected players are not retained by the Ready gate. A non-empty
roster must be entirely Ready. On success, one FishNet ObserversRpc notifies every participant,
and each peer executes the existing Book-text and `LobbyController.StartLobbyRitual()` presentation
handoff. On the server, `NetworkRitualAuthority` locks the roster, selects the first participant,
and issues the first Book command. `RitualController.StartRitual()` does not start its local
`RitualLoop` in a network session, and `LobbyController`'s locally selected Seat is ignored as a
network starting-participant preference. Offline startup is unchanged.

The seated Circle page presents readiness by local ritual role. The Host sees `RITUAL STATUS`,
`Waiting for Priests to Ready Up`, and `Ready: X / Y`, where `Y` is the current connected
Circle-member count rather than maximum capacity. When the non-empty Circle is fully Ready, the
message becomes `All Priests are Ready` and `Start Ritual` appears as the final right-page
action. Any Ready, join, or disconnect change recalculates the condition and silently removes
or restores that action. Joined clients never receive Start authority; a Ready client continues
to see `Waiting for Host to Start the Ritual...`. The action submits the authoritative request;
only the server broadcast delegates every peer to the existing
Book-to-`LobbyController.StartLobbyRitual()` flow.
The Host status message uses right line 1, right line 2 remains clear, and the Ready counter uses
right line 3 so the two visible entries do not overlap. Joined-client line placement is unchanged.

Priest Name is likewise server-owned on each `NetworkPlayer`: owners submit validated names by
ServerRpc and all peers consume its SyncVar, including spawn state for late join. The Circle Book
supports a silent right-page editor and no longer displays the unused High Priest label.
The Host can edit its active Seal through a best-effort LAN collision window; the old Seal stays
active until validation succeeds, and FishNet connections are not restarted.

`Quit Ritual` is role-aware. Hosts retain the existing Seal-release plus server/client shutdown
path. Joined non-hosts clear only their local join state, stop their local FishNet client, and
return to Play while the remote Host and other clients continue.

## Authoritative Shared Book

The physical `BookModel` in `MainGame` is the one persistent Book presentation. It remains an
ordinary active scene hierarchy before FishNet starts, so the Main Menu, Create/Join flow,
Circle, customization, and local Book interaction never depend on a network spawn. No offline
copy or per-player copy exists.

`SharedBookNetworkAuthority` is a separate invisible FishNet scene object. FishNet may deactivate
that proxy before networking without deactivating `BookModel`. When a Host/server initializes
the proxy, `NetworkBookAuthority` binds to the existing `BookMover` and presentation Transform,
preserves their current world pose, and begins server-authoritative synchronization. It never
spawns or replaces the visible Book.

Existing ritual and Seat selection behavior remains available through a compatibility bridge.
`BookMover` converts the legacy destination to a stable Seat ID and forwards it to
`NetworkRitualAuthority`, which is the sole multiplayer issuer of movement commands.
`NetworkBookAuthority` executes the accepted command, synchronizes its stable Seat ID plus
movement sequence/active state, and runs the existing `BookMover` interpolation. At the end of
the authoritative movement duration it reports stable completion data back to ritual authority.
Only `NetworkRitualAuthority` validates and accepts the arrival, publishes the immutable arrival
snapshot, and raises its read-only notification. The existing `BookController.OnArrived` callback
remains temporarily intact to preserve the legacy ritual without making it authoritative.
A server-authoritative FishNet `NetworkTransform` replicates the resulting world position and
rotation on the invisible proxy. The Host copies the persistent Book pose into that proxy;
client-only observers apply the proxy pose to their existing local `BookModel` presentation.
Observers also update `SeatManager.currentBookSeat` from the synchronized target Seat ID; they
never select a destination or simulate a competing Book path.

On client disconnect or before any later session, the proxy can return to FishNet's inactive
scene-object state while the presentation stays visible and retains the appropriate local Book
menu state. At every lifecycle stage there is exactly one visible Book hierarchy.

`BookPresentationState` is the replicated high-level presentation channel. It currently owns
`Closed` and `Open`, exposes server-only mutation through `SetPresentationState`/`SetOpen`, and
publishes observer change events. Future page turns, demon-hand beats, mood animations, ritual
reactions, and VFX should extend or consume focused state on this same Book authority instead
of creating per-client gameplay state. The current presentation components are not rewritten
by this task.

## Runtime Validation

The following networking behaviors have been successfully validated:

- Host startup.
- Client connection.
- `NetworkPlayer` spawning.
- Local player ownership.
- Remote player replication.
- Disconnect.
- Shutdown.
- Authoritative synchronized ritual start still requires Host/client multiplayer Play Mode
  validation.
- The shared Book code compiles against the Unity 6/FishNet project assemblies. Host plus
  standalone-client movement, rotation, target Seat, late-turn drift, and open/closed
  presentation still require multiplayer Play Mode validation.
- Persistent pre-network Book visibility and the offline-to-Host-to-client lifecycle still
  require manual Unity Play Mode validation.

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
- Network authoritative progression now covers successful turns and timeout elimination.
  After the existing absorption, aftermath, and Book Prison presentation barrier, the server
  marks the failed fixed-roster entry inactive/dead and either advances to the next physical
  survivor or completes the ritual with a stable string winner Player ID. Physical wrap through
  eligible survivors remains the only source of phrase growth.
- Whisper remains available for full-phrase or experimental recognition paths, but it is not the default realtime path.
- Timeout and retry behavior exist in prototype form.
- Failed players can be eliminated after the Book Prison transition, and the ritual can continue with remaining alive seats.
- Network game-over state and winner identity are authoritative; winner presentation, final
  absorption, rematch, and lobby-return UX are not implemented. The offline last-player flow
  still retains its warning-only prototype behavior.
- Debug occupants are useful local testing support, not the final player model.

## Known Missing Product Systems

- Synchronized Lobby UI.
- Removal of duplicated local lobby state.
- Book systems consuming `NetworkPlayer`.
- Multiplayer gameplay synchronization beyond the authoritative start handoff, shared physical
  Book, ritual roster/active participant, ritual timer, voice-submission ingress, phrase
  validation, turn outcome, and consequence selection. Phrase progression, consequence
  execution, and later ritual state remain on compatibility paths.
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
- Network arrival detection and legacy `BookController` arrival remain duration-based because
  `BookMover` does not expose a true completion callback.
- The legacy Timer still translates authoritative expiration into its existing `OnFinished`
  callback so current multiplayer consequence behavior survives until failure migration.
- `RitualController` applies authoritative validation snapshots to legacy phrase presentation and
  waits for authoritative consequence snapshots before entering the compatibility flow.
  Consequence presentation/execution and phrase progression remain bridged.
- The remaining ritual bridges are intentional rather than duplicate authorities: `BookMover`
  adapts stable Seat requests and executes interpolation, `NetworkBookAuthority` transports the
  one Book and reports arrival, `HourglassController`/`Timer` present time, and
  `IncantationManager` applies phrase feedback. Their offline implementations remain required.
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

REALIGN-004 now provides the first authoritative game-over presentation implementation. A
`NetworkGameOverPresentationController` consumes only synchronized `Completed` snapshots,
resolves the winner by stable `PlayerId`, displays a local result overlay, and asks
`NetworkBookAuthority` for a server-only presentation movement to the winner's physical Seat.
That movement has its own session/ritual/presentation identity and completes without creating a
`RitualBookArrivalReport`, timer, voice turn, phrase change, rotation, or active participant.
Host/client result presentation is functionally validated. Winner Book presentation movement is
still not visibly returning the Book to the winner; that non-blocking REALIGN-004 limitation is
intentionally outside REALIGN-005.

REALIGN-005 adds the reusable post-game lifecycle. Only the authenticated Host can request the
shared return. `NetworkRitualAuthority` transitions `Completed -> Inactive`, clears match-scoped
state and the dead roster, while connected `NetworkPlayer` identity, Circle membership, Seat,
name, and appearance persist. Ready resets to NotReady. Peers restore prison/absorption and exact
Camera/AudioListener/movement component states, return to lobby presentation, hide results, and
the server restores the one Book to its captured authored lobby pose without gameplay arrival.
Host/client two-match runtime validation is complete. REALIGN-005.1 also enforces exactly one
local `AudioListener` through lobby, ritual, local death, return, and the second ritual.

REALIGN-006 adds compact cosmetic look-pose replication. The owning player's existing
`PlayerMovement` retains immediate mouse response; `NetworkCharacterLookPose` sends bounded
pitch/yaw at a modest rate and remote presentations reuse the same procedural bone formula
without enabling remote input. The state follows the persistent `NetworkPlayer` across death,
lobby return, and later matches and is independent from ritual authority. Host/client runtime
validation is complete.

REALIGN-007 separates the existing visible Book's presentation ownership by lifecycle. Lobby,
Circle, seated customization, and Character view are local to each process; ritual and game-over
presentation continue through the existing server-authoritative SharedBook proxy. Character now
enters its logical page, local Book pose, and local menu-camera view in one action, remains
available while seated, and join-Seal input stores at most four normalized characters without a
hidden overflow state. Host/client runtime validation remains required.

REALIGN-007.1 makes completed-match return begin a fresh seating phase. The Host-authoritative
reset now clears every Circle member's Seat, Ready state, and replicated lobby state while
preserving connection, identity, name, appearance, and Circle membership. Existing Seat observers
release presentation occupancy; each local owner returns to the configured lobby waiting
position while remote unseated clones remain hidden. Ready and ritual start both require a valid
Seat assignment. After that reset, both Host and Client land on the canonical `BookState.Lobby`
Circle page; the Host reaches `HostMenu` only through the existing Circle Back action. Host/client
runtime validation remains required.

REALIGN-007.4 gives result UI priority at game over. The local winner's procedural look is
immediately settled to its authored neutral pose, the unlocked visible cursor prevents further
relative look input, and the Host can use the existing Return button by mouse or UI submit.
Actual Return to Lobby still performs REALIGN-007.3's separate reliable buffered network-zero
reset, so fresh Character preview and Match 2 cannot inherit Match 1's final pose. The MainGame
death vignette now begins absorption `0.25` seconds after the hand's GrabMoment. Temporary
SeatManager and BookOrbit Space shortcuts remain available offline but are suppressed during an
initialized network session; ritual authority also rejects legacy Book requests and participant
commits before they can repopulate active-player state after `Completed`.

REALIGN-007.5 makes fresh-lobby placement the final local character-root write after completed
match teardown. Prison and absorption systems still restore their cached presentation state,
then `LobbyController.ReturnToLobbyPresentation` reuses the canonical local-only unseated helper
to place the scene character at `lobbyWaitingPosition`. This removes synchronization-order
dependence for eliminated owners while remote unseated clones remain hidden.

REALIGN-007.6 scopes death-presentation camera world-pose preservation to cameras whose Transform
is the moved eliminated-character root or one of its descendants. Remote absorption and Book
Prison placement no longer rewrite an unrelated survivor gameplay camera, preventing a stale
camera-local offset from carrying through Return to Lobby into a later match.

LOBBY-002 separates full Circle/Ritual exit presentation from completed-match lobby return.
`LobbyController` captures the local scene character's authored startup position and rotation
before any Seat placement. Host Circle Back now requests a server-authoritative NotReady,
NotSeated, unassigned-Seat reset while preserving the hosted Ritual, and Host/Client full Quit
restores the surviving local scene character before network teardown. Completed-match return
continues to use the distinct `lobbyWaitingPosition`; remote unseated clones remain hidden.

Continue migrating lobby systems so `NetworkPlayer` becomes the authoritative source of
multiplayer lobby state. Synchronized Seat assignment and appearance are complete; the remaining
sequence is Lobby UI, removal of duplicated local lobby state, and transition of Book systems to
read `NetworkPlayer`.

## Last Reviewed

2026-08-13 after adding Inspector-tunable late-stage TopSand horizontal narrowing without changing
local-Z height, anchoring, BottomSand, or timer authority.
