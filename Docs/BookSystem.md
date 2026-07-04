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

## Design Direction

The book should feel alive and theatrical.

It may hesitate, slow down, or fake intent later, but those flourishes must still preserve the single-book rule and the Seat system's ownership of physical table traversal.
