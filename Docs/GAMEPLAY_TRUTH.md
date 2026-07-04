# Gameplay Truth

This document defines the sacred gameplay rules of Incantation.

Purpose: protect the gameplay identity and hard laws of the table ritual.

Questions answered here:

- What is Incantation at its core?
- What gameplay rules must not be accidentally broken?
- Which systems are paused until the core loop is reliable?
- What counts as the current v0.1 prototype truth?

This document does not contain implementation details, current task planning, milestone history, or Inspector tuning.

Read next: `Docs/GAME_DESIGN_PILLARS.md` for design philosophy, then `Docs/PROJECT_STATUS.md` for today's project state.

When implementation details conflict with this document, this document wins until it is deliberately updated.

## Core Gameplay Truth

Incantation is a competitive social party game set in a dark fantasy horror atmosphere.

The game is built around one cursed book, one visible ritual phrase, seated players, voice pressure, and the stories created when people succeed, panic, betray, laugh, or fail together.

The core game must be fun without cards, demon reactions, lore delivery, or campaign systems.

The ritual is the game.

## The Laws Of Incantation

1. Players stay seated for the ritual.
2. Characters do not walk around.
3. There is one real cursed book.
4. The book is the main driver of tension.
5. The hourglass is the pressure system.
6. Voice interaction is central to play.
7. All active players recite the same current visible phrase.
8. The ritual phrase starts with exactly 1 word.
9. Exactly 1 word is added after every full active table rotation.
10. Phrase growth is rotation-based, not turn-based.
11. A player must satisfy the selected validation mode to progress.
12. Players may retry failed recitations while time remains.
13. Timeout can lead to elimination.
14. Fun comes before realism.
15. Every mechanic must support tension, laughter, betrayal, stress, surprise, or a story players will remember.

## Current v0.1 Prototype Truth

The current prototype is a playable local ritual slice.

It includes:

- One cursed book.
- Physical seat traversal.
- Local debug seat occupants.
- Shared phrase growth by full active table rotation.
- `WordByWordRealtime` validation as the default feel.
- `FullPhrase` validation as an optional strict mode.
- Realtime visual word absorption.
- Wrong word rejection feedback.
- Hourglass timer pressure.
- Book movement after ritual acceptance.
- Ambient audio and dark cabin ambience.
- Fire flicker.
- Hourglass light possession effect.
- Room veil.

Lobby is the next major milestone.

Networking is not implemented yet.

Debug occupants are local testing tools only.

## Physical Seat Rotation Truth

The cursed book always travels according to the physical seating order around the table.

The physical seat order is explicitly configured and owned by `SeatManager`.

The physical seat order is not determined by:

- GameObject names.
- Seat numbers.
- Hierarchy order.
- Player join order.
- Network player index.

The current prototype uses this clockwise physical order:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise traversal is the exact reverse of that configured physical order.

The Core Ritual Engine must never assume sequential numbering. Seat1 does not imply Seat2 is next.

The book advances according to the current traversal rule, not according to player connection order.

Future spell cards may temporarily change the traversal rule, such as reverse rotation, random next player, skip occupied seat, double jump, or other table-order effects. These effects change how the configured physical seat order is traversed; they do not replace Seat system ownership of physical order.

## Voice And Validation Truth

Voice recognizers provide candidates. They do not own gameplay success.

The current validation modes are:

- `WordByWordRealtime`: default prototype mode. Spoken keywords are validated immediately against the next expected word, giving realtime visual word absorption and wrong word rejection.
- `FullPhrase`: optional strict mode. A full phrase transcript is validated against the current visible phrase.

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.

Whisper is kept in the project for full-phrase and experimental recognition paths, but must not be forced as the only validation path.

Unity Dictation and Azure voice services must not be reintroduced.

`PhraseValidator`, `IncantationManager`, and the selected validation mode define whether spoken text satisfies the current ritual phrase.

`GrowingIncantationManager` and incantation phrase systems own the current ritual phrase.

No UI, card, spell, voice chat, networking, book, timer, player, demon, or lore system may independently decide what the current ritual phrase is.

## Ritual Phrase Rules

The ritual phrase starts with 1 word.

All active players recite the same phrase.

The phrase remains unchanged during a table rotation.

Exactly 1 word is added after every full active table rotation.

The phrase must not grow after every individual player turn.

The phrase must not fork per player.

The phrase must not be secretly changed by secondary systems.

Spell Cards never directly modify the ritual phrase.

`SpellPhraseLibrary` is separate from the ritual phrase vocabulary.

## Voice Chat Boundary

Voice Chat is independent from ritual recognition.

Voice Chat may let players talk, panic, negotiate, deceive, or distract each other.

Ritual recognition listens for ritual recitation and sends recognition candidates into validation.

Voice Chat must not validate ritual speech, mutate the phrase, advance turns, or eliminate players.

## Design Principles

Build moments, not features.

The book, hourglass, table, and spoken phrase are the center of the experience.

Prefer simple rules that create social chaos over complex systems that distract from the ritual.

Players should understand what they are supposed to say, who is under pressure, and why the moment is tense.

The best features make players look at each other, listen to each other, doubt each other, pressure each other, or remember what happened afterward.

Do not add mechanics that pull attention away from the seated table ritual unless the core loop already works and the new mechanic clearly strengthens that loop.

## Out Of Scope For The Current Milestone

The current milestone is the clean core ritual loop and reliable voice interaction, with lobby next.

Out-of-scope systems include:

- Cards.
- Spell Card effects.
- Demon reactions.
- Lore delivery.
- Notebook systems.
- Campaign objectives.
- Interference systems beyond the current core loop.
- New networking architecture.
- Multiple gameplay books.
- Walking characters.
- Any system that changes the phrase outside the incantation phrase authority.
- Any system that validates ritual speech outside the selected ritual validation path.

These systems may return later, but they must not complicate the current milestone.

## Milestone Standard

The game must be fun without cards, demon reactions, or lore.

If the seated players, moving book, hourglass, shared phrase, retries, timeouts, and eliminations do not create tension and laughter on their own, secondary systems should wait.

The core ritual loop must work first.
