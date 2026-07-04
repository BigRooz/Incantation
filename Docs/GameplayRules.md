# Gameplay Rules

## Current Ritual Rules

- Players stay seated.
- Characters do not walk around.
- There is one real cursed book.
- The book moves from active seat to active seat.
- `SeatManager` owns physical seat order.
- The current clockwise order is Seat1 -> Seat5 -> Seat3 -> Seat6 -> Seat2 -> Seat7 -> Seat4 -> Seat8.
- The phrase starts with 1 word.
- Every active player says the same current phrase.
- After a full active table rotation, add 1 word.
- The phrase does not grow after every player.
- The active player must satisfy the selected validation mode before the hourglass expires.
- `WordByWordRealtime` is the default prototype validation mode.
- `FullPhrase` is an optional strict validation mode.
- `WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.
- Whisper remains available but must not be forced as the only validation path.

## Current Prototype Notes

- Local debug occupants are for Play Mode testing only.
- Lobby is the next major milestone.
- Networking is not implemented yet.

## Out Of Current Scope

- Notebook.
- Cards.
- Lore delivery.
- Demon reactions.
- Unity Dictation.
- Azure voice services.
- Multiple gameplay books.
- Walking characters.
