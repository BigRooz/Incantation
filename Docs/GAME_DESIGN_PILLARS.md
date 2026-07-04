# Game Design Pillars

This document explains the fundamental design philosophy behind Incantation.

For strict gameplay rules, read `Docs/GAMEPLAY_TRUTH.md`. For durable decision rationale, read `Docs/WHY.md` and `DECISIONS.md`.

## Core Fantasy

Incantation is a competitive social party game set in a dark fantasy horror atmosphere.

Players are corrupted priests seated around one ritual table. A cursed book moves from player to player and demands forbidden words before the hourglass runs out. Everyone believes they can claim the demon's power, but the book is manipulating the table.

The fantasy is not walking through a haunted house. The fantasy is being chosen by a cursed object while everyone else watches, interrupts, pressures, laughs, and waits for you to crack.

## Emotional Experience

Incantation should create memorable table moments:

- A player panics and forgets a word everyone else knows.
- Someone shouts the next word too early and distracts the active player.
- The book moves past one player and lands on another, creating relief and dread in the same second.
- A wrong word is rejected immediately while the hourglass keeps draining.
- The table collectively realizes the phrase just grew and nobody is ready.
- A future betrayal feels funny, stressful, and personal because everyone is trapped in the same ritual.

The game should feel tense, readable, noisy, and theatrical. Horror supports the pressure, but social chaos is the product.

## Design Pillars

### The Book Is The Main Character

There is one real cursed book. It chooses who plays next, focuses the room, and gives the ritual its personality.

The book should feel like a shared threat. It can hesitate, drift, snap into place, or fake out players later, but it must remain one central object. Multiple gameplay books would weaken the ritual by splitting attention into separate personal tasks.

### The Table Is The Game Space

Players stay seated. Characters do not walk around.

The important action is in faces, voices, hands, posture, the hourglass, and the book. Movement systems should not compete with the ritual. Animation should support seated tension: head turns, mouth movement, eye focus, subtle body motion, hand reactions, and table presence.

### Voice Is The Primary Interface

The act of speaking forbidden words is the core interaction.

Voice recognition should make the table listen. It should create pressure because players must perform out loud, in sequence, under time. Input alternatives may be useful for testing or accessibility later, but they should not replace voice as the default fantasy.

### Immediate Feedback Is Mandatory

Players must quickly understand what happened.

Correct words should be absorbed or marked immediately in `WordByWordRealtime`. Wrong words should sting immediately. Time pressure should remain visible or felt. The game should avoid delayed judgment when the prototype can provide a faster emotional response.

### Gameplay Comes Before Visual Spectacle

Atmosphere matters, but visual spectacle is subordinate to the playable ritual.

Fire flicker, room veil, possession lighting, ambient audio, and cabin mood are valuable because they make the table feel cursed. They should not become systems that distract from book movement, hourglass pressure, voice validation, retries, timeouts, or elimination.

### Shared Escalation Beats Personal Complexity

All active players recite the same visible phrase during a rotation. The phrase grows by exactly one word after a full active table rotation.

This creates shared memory pressure. Everyone watches the same burden grow. Difficulty should escalate around the table, not through hidden personal phrases or one-player-only complexity.

### Social Tension Is The Feature Filter

Every mechanic should increase tension, laughter, betrayal, stress, surprise, or story value.

If a mechanic does not make the book, table, hourglass, voice, or player relationships stronger, it should wait. This is especially important for cards, demon reactions, lore, campaign objectives, and networking polish.

## Intended Player Behavior

Players should:

- Watch the book and react when it chooses someone.
- Listen to the current phrase because they may need it next.
- Talk over each other in ways that create pressure or comedy.
- Try to help, mislead, distract, or psych out the active player.
- Feel relief when skipped and dread when chosen.
- Remember specific failures, saves, betrayals, and last-second recitations.

The game should make players perform for each other, not just operate UI.

## Why The Game Exists

Incantation exists to turn a simple ritual into a social pressure machine.

The core loop is intentionally easy to explain:

1. The book chooses a seated player.
2. The player speaks the visible phrase.
3. The hourglass drains.
4. Correct words are accepted.
5. Wrong words are rejected.
6. The phrase grows after the table survives a full rotation.

The value comes from what happens between people while those rules run.

## What Makes Incantation Unique

Incantation combines:

- A single shared cursed object as turn driver.
- Seated party-game pressure instead of character navigation.
- Voice performance as the central skill.
- Realtime word absorption and rejection.
- Shared phrase growth by physical table rotation.
- Dark fantasy horror atmosphere serving a competitive social game.
- Future betrayal and interference systems built around the same table ritual.

The game should not drift into a standard horror exploration game, a generic spell-card battler, or a lobby-first networking demo. The ritual is the identity.

## Long-Term Vision

The long-term product can support multiple modes, including Last Priest Standing and Campaign, but both should share the same core systems:

- One table.
- One main cursed book.
- Seated players.
- Hourglass pressure.
- Voice incantation.
- Shared phrase progression.
- Social interference.
- Clear failure and elimination rules.

Last Priest Standing should emphasize betrayal, pressure, and being the final corrupted priest absorbed by the book.

Campaign should emphasize cooperation against the demon while preserving the same table, book, hourglass, and voice loop.

Demon reactions, cards, lore delivery, and campaign objectives should return only after the core ritual can create memorable moments on its own.
