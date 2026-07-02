# Core Gameplay Rules

This document is the constitution of Incantation.

It is not a Game Design Document.

It is not a technical document.

It is not a roadmap.

It defines the immutable gameplay rules that every feature, system, balance change, and future expansion must respect.

When a gameplay decision conflicts with this document, this document wins until it is deliberately updated.

## 1. The Book

The cursed book is the center of the game.

Players do not own turns.

The book owns turns.

The active player is simply the player currently sitting at the seat where the book is located.

The book is the protagonist of the gameplay loop.

Every system that affects turn order, pacing, pressure, voice interaction, or player consequence must preserve the feeling that the book is alive, choosing, demanding, and moving the ritual forward.

## 2. Physical Seats

The Seat System owns the physical table layout.

The Core Ritual Engine never assumes:

- Seat numbering.
- Hierarchy order.
- Player join order.
- Network index.

The Seat System always answers:

> What is the next active seat according to the current traversal rule?

The table is a physical play space, not a list of connected players.

Seat order is a gameplay truth because the book moves through the room, across the table, and into the social pressure between players.

## 3. Traversal Rules

The book follows a traversal rule.

The default traversal rule is the physical clockwise seat order configured by the Seat System.

The prototype clockwise order is:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise is the exact reverse:

1. Seat8
2. Seat4
3. Seat7
4. Seat2
5. Seat6
6. Seat3
7. Seat5
8. Seat1

Future traversal rules may include:

- Reverse rotation.
- Random seat.
- Skip seat.
- Double jump.
- Teleport.
- Cursed patterns.

The traversal rule belongs to the Seat System.

Traversal can become strange, dramatic, unfair, funny, or cursed, but the Core Ritual Engine must still ask the Seat System where the book goes next.

## 4. Active Seats

Only occupied and active seats participate.

Empty seats are skipped.

Eliminated players deactivate their seat.

A rotation is complete only after every active seat has been visited exactly once.

Not every physical seat.

This means phrase growth, rotation completion, and turn progression are based on the living ritual circle, not on the maximum number of chairs around the table.

## 5. Turn Lifecycle

A turn is:

1. Book arrives.
2. Player speaks.
3. Timer ends or player succeeds.
4. Consequences happen.
5. Seat state updates.
6. Traversal rule determines next active seat.
7. Short breathing delay.
8. Book moves.
9. Next turn begins.

The book never becomes permanently stuck on one player.

A player may panic, fail, retry, or be punished, but the ritual must continue unless the game mode has reached a valid ending condition.

## 6. Consequences

Success and failure are separate from traversal.

Success may:

- Preserve the player.
- Grow the phrase.
- Award bonuses.

Failure may:

- Eliminate the player.
- Apply curses.
- Trigger demon events.

Neither success nor failure owns the traversal rule.

Consequences decide what happened to the player, the phrase, or the game state.

Traversal decides where the book goes next.

Those responsibilities must remain separate so future effects can create chaos without breaking the ritual's structure.

## 7. Pacing

The game intentionally breathes.

Players need time to:

- Laugh.
- React.
- Accuse.
- Bluff.
- Plan.

Book movement timing is gameplay.

Possible modifiers include:

- Faster book.
- Slower book.
- Hesitation.
- Dramatic pause.

The space between turns is not dead time.

It is where tension spreads around the table and where players turn a rule into a story.

## 8. Future Proofing

Future spell cards should modify rules instead of replacing systems.

Examples:

- Reverse Rotation.
- Random Destination.
- Skip Next Seat.
- Double Jump.
- Frozen Book.

The Seat System remains the authority.

Future mechanics may bend traversal, pacing, pressure, or consequences, but they must do so by changing the current rule set through the proper authority.

They must not bypass the table, create private turn ownership, or replace the book as the driver of the ritual.

## 9. Design Philosophy

Every gameplay feature should answer:

> Does this strengthen the feeling that the cursed book is alive?

If not, reconsider the feature.

Incantation is built to create tension, laughter, betrayal, stress, surprise, and stories players remember.

The book, the table, the hourglass, the visible phrase, and the players' voices are the heart of the game.

Everything else must serve that heart.
