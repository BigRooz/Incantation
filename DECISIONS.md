# Decisions

This file records durable project decisions that should survive individual task conversations.

For onboarding, read `Docs/START_HERE.md`.

For authoritative gameplay rules, read `Docs/GAMEPLAY_TRUTH.md`.

## Decision Format

Each decision should explain:

1. What was decided.
2. Why it was decided.
3. What it means for future work.

When a decision changes, update this file and the relevant source-of-truth document in the same task.

## Game Identity

Decision:

Incantation is a competitive social party game in a dark fantasy horror atmosphere.

Why:

The goal is memorable player interaction: tension, laughter, betrayal, stress, surprise, and stories players retell. Horror supports that goal, but it is not the whole product.

Implication:

Features should be judged by the moments they create, not by how much lore, realism, or mechanical complexity they add.

## One Cursed Book

Decision:

There is one real gameplay book. Do not create one gameplay book per player unless explicitly requested and the core rule is deliberately changed.

Why:

The book is the main character. One shared object moving around the table makes players feel chosen, threatened, skipped, spared, or doomed by the same cursed presence.

Implication:

Turn ownership, tension, pacing, and future traversal effects should preserve the single-book ritual. Visual helpers such as `BookGhost` must not become gameplay authorities.

## Seated Ritual

Decision:

Players stay seated for the ritual. Characters do not walk around.

Why:

The game should focus attention on faces, voice, the table, the book, the hourglass, and social pressure. Movement systems would distract from the core social ritual and increase implementation scope.

Implication:

Animation work should focus on head, mouth, eyes, body posture, hands, and seated reactions. Do not introduce walking mechanics as part of core loop work.

## Physical Seat Order

Decision:

`SeatManager` owns physical seat order.

Current clockwise order:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise order is the exact reverse.

Why:

The table is a physical play space. The book moves through the room, not through a sorted list of GameObjects, player joins, or network IDs.

Implication:

Never infer traversal from sequential seat numbers, hierarchy order, player join order, or network index. Future cards may bend traversal, but they must do so through the Seat system's authority.

## Active Seats And Debug Occupants

Decision:

Only occupied active seats participate. Local debug occupants are a Play Mode testing aid, not the final lobby or networking model.

Why:

The prototype needs reliable local testing before production lobby and networking exist.

Implication:

Do not build permanent player identity, lobby, networking, or balance assumptions around debug occupants. Lobby is the next major milestone.

## Rotation-Based Phrase Growth

Decision:

The ritual phrase starts with one word and grows by exactly one word after a full active table rotation.

Why:

Every active player should face the same visible phrase during a rotation. This creates shared escalation and table-wide memory pressure without making difficulty spike after every individual turn.

Implication:

Do not grow the phrase after each player. Do not fork the phrase per player. Do not let cards, UI, voice systems, networking, or lore systems secretly mutate the current ritual phrase.

## Voice Validation Modes

Decision:

`WordByWordRealtime` is the default prototype validation mode. `FullPhrase` remains available as an optional strict mode.

Why:

Realtime word acceptance creates immediate visual absorption and wrong-word rejection, which better supports the prototype's pressure and table feel. Full-phrase validation is still useful for strict recitation, experiments, and fallback testing.

Implication:

Preserve both modes. Do not remove `FullPhrase`, and do not make full-phrase recognition the only supported path.

## Current Voice Recognizer Direction

Decision:

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay. Whisper remains in the project for full-phrase or experimental recognition paths.

Why:

The prototype needs immediate word-level feedback. Whisper can be useful, but forcing it into the realtime core path would undermine the current prototype feel and add unnecessary dependency risk.

Implication:

Do not force Whisper as the only validation path. Keep `WhisperSandbox` as sandbox/reference code unless explicitly asked to change it. Do not use Unity Dictation or Azure voice services in the current plan.

## Spell Phrase Boundary

Decision:

`SpellPhraseLibrary` is separate from ritual words.

Why:

Core ritual words and future spell/card phrases serve different systems. Merging them would blur ownership and let secondary systems interfere with the main spoken phrase.

Implication:

Do not merge spell/card phrases into the core ritual vocabulary unless explicitly requested.

## Paused Systems

Decision:

Notebook, cards, lore delivery, demon reactions, campaign objectives, networking polish, and broad visual changes are paused during core ritual work unless explicitly requested.

Why:

The core loop must be fun before secondary systems add complexity. The game should work with only seated players, one moving book, the hourglass, a shared phrase, voice validation, retry, timeout, and eventual elimination.

Implication:

Do not touch paused systems while implementing core ritual tasks unless the task explicitly scopes them.

## Next Milestone

Decision:

Lobby is the next major milestone.

Why:

The project needs to move from local debug occupants to a real pre-ritual player flow before networking polish, cards, demon reactions, or campaign objectives can be evaluated cleanly.

Implication:

Next work should focus on lobby entry, ready check, automatic seating, and handoff into the existing ritual loop.

## Task Discipline

Decision:

Use small validated tasks. One task should become one reviewed, compiling, tested, committed, and pushed change.

Why:

The project is a Unity prototype with many scene and system boundaries. Small tasks reduce regression risk and make it easier to preserve the core identity.

Implication:

Run `git status` before editing. Do not modify more than necessary. Documentation-only tasks must not change gameplay code, scenes, prefabs, or assets.
