# Book System

## Current Rule

There is one real cursed book.

Do not create one gameplay book per player unless explicitly requested.

## Current Behavior Goal

- The book starts as the ritual focus.
- The book moves from player to player.
- The book determines whose turn is active.
- After a full table rotation, the shared ritual phrase gains 1 word.

## Scene Boundary

`BookModel` is the real moving book.

`BookTarget` defines where the real book should move.

`BookGhost` is an editor/placement preview only and must not contain gameplay scripts.
