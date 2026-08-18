# Book System

Purpose: define ownership and boundaries for the single cursed book.

Questions answered here:

- What is the real gameplay book?
- How does the book relate to Seats?
- Which book-related objects are visual references only?

This document does not contain current task planning, full ritual architecture, card behavior, or demon reactions.

Read next: `Docs/BOOK_STATE_MACHINE.md` for state flow or `Docs/TechnicalArchitecture.md` for wider ownership boundaries.

## Current Rule

There is one real cursed book.

The book is the main character.

Do not create one gameplay book per player unless explicitly requested.

## Current Prototype Behavior

- The book starts as the ritual focus.
- The book moves from active seat to active seat.
- The book determines whose turn is active.
- The book follows the physical seat order owned by `SeatManager`.
- After a full active table rotation, the shared ritual phrase gains 1 word.
- After ritual acceptance, the book moves to the next active seat.

## Physical Seat Order

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

The book must not assume Seat1 -> Seat2 -> Seat3 order.

The book must not follow player join order or network player index.

## Scene Boundary

`BookModel` is the real moving book.

`BookTarget` defines where the real book should move.

`BookGhost` is an editor/placement preview only and must not contain gameplay scripts.

Do not put gameplay authority on visual-only book references.

## Network Movement Boundary

`NetworkRitualAuthority` selects only the final active, alive participant and owns turn,
rotation, phrase, and timer rules. `NetworkBookAuthority` converts that semantic destination
into presentation movement for the one shared Book.

Authoritative ritual movement captures the Book's previous physical Seat and walks
`SeatManager`'s complete configured physical Seat order in the synchronized ritual traversal
direction until it reaches the final living target. Dead or empty Seats may be intermediate
presentation waypoints, but they never become destinations, turns, arrivals, or timer starts.
For the first movement of a ritual, lobby and post-game lifecycle intentionally leave no current
Book Seat. If the restored authored Book pose lies within half one average adjacent-Seat distance
of a configured Book destination in the table plane, presentation resolves that nearest Seat as
the physical route origin. This does not create a participant, gameplay destination, or arrival;
an unresolved or non-perimeter pose retains direct movement as the fallback.

`BookMover` executes the ordered Seat destinations as one continuous route. Total travel time is
scaled from route distance so the configured `moveDuration` remains approximately the duration
of one adjacent Seat step. It reports completion once, at the final destination;
`NetworkBookAuthority` produces the one ritual arrival report only from that completion. Replaced
or cancelled movement cannot report stale completion. Direct movement remains the fallback when
no physical Seat route is available, including initial, lobby, and offline-compatible paths.

## Design Direction

The book should feel alive and theatrical.

It may hesitate, slow down, or fake intent later, but those flourishes must still preserve the single-book rule and the Seat system's ownership of physical table traversal.
