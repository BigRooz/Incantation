# Current Project State

## Summary

Incantation is currently a v0.1 playable local prototype of the core seated ritual loop.

The core vision has not changed: one cursed book, one table, seated players, an hourglass, voice pressure, betrayal, tension, laughter, and memorable social moments.

The book is the main character.

## What Works Now

The current prototype includes:

1. One cursed book.
2. Physical seat traversal.
3. Growing incantation by full active table rotation.
4. Local debug seat occupants.
5. `WordByWordRealtime` validation as the default prototype mode.
6. `FullPhrase` validation as an optional strict mode.
7. `WindowsKeywordVoiceRecognizer` for immediate realtime keyword validation.
8. Whisper retained for full-phrase or experimental recognition paths.
9. Realtime visual word absorption.
10. Wrong word rejection feedback.
11. Hourglass timer pressure.
12. Book movement after ritual acceptance.
13. Ambient audio.
14. Fire flicker.
15. Hourglass light possession effect.
16. Room veil and dark cabin ambience.

## What Is Not Implemented Yet

- Lobby.
- Ready check.
- Production automatic seating from lobby players.
- Networking.
- Production elimination flow.
- Interference cards.
- Demon reactions.
- Campaign objectives.

Lobby is the next major milestone.

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
