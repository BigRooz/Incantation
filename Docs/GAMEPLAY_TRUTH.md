# Gameplay Truth

This document defines the sacred gameplay rules of Incantation.

When implementation details conflict with this document, this document wins until it is deliberately updated.

## Core Gameplay Truth

Incantation is a competitive social party game set in a dark fantasy horror atmosphere.

The game is built around one cursed book, one visible ritual phrase, seated players, voice pressure, and the stories created when people succeed, panic, betray, laugh, or fail together.

The core game must be fun without cards, demon reactions, lore delivery, or campaign systems.

The ritual is the game.

## The Laws Of Incantation

1. Players stay seated for the ritual.
2. There is one real cursed book.
3. The book is the main driver of tension.
4. The hourglass is the pressure system.
5. Voice interaction is central to play.
6. All active players recite the same current visible phrase.
7. The ritual phrase starts with exactly 1 word.
8. Exactly 1 word is added after every full table rotation.
9. Phrase growth is rotation-based, not turn-based.
10. A player must recite the full visible phrase to progress.
11. Players may retry failed recitations while time remains.
12. Timeout can lead to elimination.
13. Fun comes before realism.
14. Every mechanic must support tension, laughter, betrayal, stress, surprise, or a story players will remember.

## Voice And Validation Truth

Whisper only transcribes.

Whisper never decides gameplay success, failure, elimination, phrase progression, or turn advancement.

Windows speech recognition is fallback only. It also does not decide gameplay authority.

`PhraseValidator` is the only validation authority for whether spoken text satisfies the current ritual phrase.

`GrowingIncantationManager` is the only source of truth for the current ritual phrase.

No UI, card, spell, voice chat, networking, book, timer, player, demon, or lore system may independently decide what the current ritual phrase is.

No system may bypass `PhraseValidator` to mark a ritual recitation as correct.

## Ritual Phrase Rules

The ritual phrase starts with 1 word.

All active players recite the same phrase.

The phrase remains unchanged during a table rotation.

Exactly 1 word is added after every full table rotation.

The phrase must not grow after every individual player turn.

The phrase must not fork per player.

The phrase must not be secretly changed by secondary systems.

Spell Cards never directly modify the ritual phrase.

`SpellPhraseLibrary` is separate from the ritual phrase vocabulary.

## Voice Chat Boundary

Voice Chat is independent from ritual recognition.

Voice Chat may let players talk, panic, negotiate, deceive, or distract each other.

Ritual recognition listens for ritual recitation and sends transcription candidates into validation.

Voice Chat must not validate ritual speech, mutate the phrase, advance turns, or eliminate players.

## Design Principles

Build moments, not features.

The book, hourglass, table, and spoken phrase are the center of the experience.

Prefer simple rules that create social chaos over complex systems that distract from the ritual.

Players should understand what they are supposed to say, who is under pressure, and why the moment is tense.

The best features make players look at each other, listen to each other, doubt each other, pressure each other, or remember what happened afterward.

Do not add mechanics that pull attention away from the seated table ritual unless the core loop already works and the new mechanic clearly strengthens that loop.

## Out Of Scope For The Current Milestone

The current milestone is the clean core ritual loop and reliable voice recognition.

Out-of-scope systems include:

- Cards.
- Spell Card effects.
- Demon reactions.
- Lore delivery.
- Notebook systems.
- Campaign objectives.
- Interference systems beyond the current core loop.
- Visual polish unrelated to core ritual clarity.
- New networking architecture.
- Any system that changes the phrase outside `GrowingIncantationManager`.
- Any system that validates ritual speech outside `PhraseValidator`.

These systems may return later, but they must not complicate the current milestone.

## Milestone Standard

The game must be fun without cards, demon reactions, or lore.

If the seated players, moving book, hourglass, shared phrase, retries, timeouts, and eliminations do not create tension and laughter on their own, secondary systems should wait.

The core ritual loop must work first.
