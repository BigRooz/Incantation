# Decisions

This file records current project guidance that should survive individual task conversations.

## Current Direction

- Incantation is a competitive social party game in a dark fantasy horror atmosphere.
- Do not change the core vision.
- The cursed book is unique. Do not build one gameplay book per player.
- The book is the main character.
- Players stay seated. Characters do not walk around.
- The ritual loop is centered on seated players, the moving book, the hourglass, the shared spoken phrase, and voice validation.
- The phrase starts with 1 word.
- Every active player says the same current phrase when the book reaches them.
- After a full active table rotation, the phrase grows by 1 word.
- Phrase growth is rotation-based, not player-turn-based.
- Lobby is the next major milestone.
- Networking is not implemented yet.

## Current Voice Decision

- The default prototype validation mode is `WordByWordRealtime`.
- `WordByWordRealtime` uses `WindowsKeywordVoiceRecognizer` for immediate visual word validation.
- `FullPhrase` remains available as an optional strict mode using full phrase transcript validation.
- Whisper remains in the project, but it must not be forced as the only validation path.
- Windows keyword recognition is currently preferred for realtime prototype gameplay.
- Unity Dictation and Azure are not current project dependencies and must not be reintroduced.

## Current Table Decision

- `SeatManager` owns physical seat order.
- The current clockwise order is Seat1 -> Seat5 -> Seat3 -> Seat6 -> Seat2 -> Seat7 -> Seat4 -> Seat8.
- Counter-clockwise order is the exact reverse.
- Debug occupants are for local testing only.

## System Boundaries

- `SpellPhraseLibrary` is separate from ritual words. Do not merge spell/card phrases with the core ritual phrase vocabulary unless explicitly requested.
- Notebook, card, lore, and demon systems are paused until the clean core ritual loop and voice interaction are reliable.
- `WhisperSandbox` is a sandbox/reference area. Do not rework it during core ritual tasks unless explicitly requested.
- Assets, visuals, scenes, prefabs, and networking are not priority areas unless explicitly requested.

## Task Discipline

- Prefer small isolated tasks.
- Do not modify more than necessary.
- One task equals one commit.
- Documentation-only tasks must not change gameplay code, scenes, prefabs, or assets.
