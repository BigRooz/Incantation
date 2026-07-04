# Technical Architecture

## Current Priority

Priority 1 is the clean core ritual loop and reliable voice interaction.

The current prototype is a local playable ritual slice. Lobby is the next major milestone. Networking is not implemented yet.

## Architecture Boundaries

- Prefer extending existing systems over creating new ones.
- Keep gameplay logic out of visual-only objects.
- Keep the Seat system logical and the chair visual.
- `SeatManager` owns the configured physical seating order around the table.
- Keep `BookGhost` as a visual placement reference only.
- Keep one real gameplay book.
- Keep `SpellPhraseLibrary` separate from ritual words.
- Treat `WhisperSandbox` as sandbox/reference code unless explicitly asked to change it.
- Do not make characters walk around.

## Current Prototype Systems

The current v0.1 prototype includes:

- `RitualController` as the current prototype ritual orchestration surface.
- `SeatManager` for physical seat traversal and book routing.
- `BookMover` and book helpers for moving the one real book.
- `IncantationManager` and core ritual phrase systems for phrase state and word acceptance.
- `VoicePhraseNormalizer` for phrase and word normalization.
- `WindowsKeywordVoiceRecognizer` for realtime keyword recognition.
- `WhisperVoiceRecognizer` for Whisper-based recognition paths.
- `HourglassController` for timer pressure.
- Lighting and ambience components for fire flicker, hourglass light possession, room veil, and cabin atmosphere.

## Physical Seat Order

The cursed book advances according to the configured physical seating order, not according to GameObject names, seat numbers, hierarchy order, player join order, or network player index.

The Core Ritual Engine must never assume sequential numbering.

The current prototype clockwise order is:

1. Seat1
2. Seat5
3. Seat3
4. Seat6
5. Seat2
6. Seat7
7. Seat4
8. Seat8

Counter-clockwise traversal is the exact reverse.

Traversal rules may later be temporarily changed by spell-card effects such as reverse rotation, random next player, skip occupied seat, or double jump. Those effects modify traversal behavior; they do not move ownership of physical order out of the Seat system.

Debug occupants are for local testing only and should be replaced by lobby and networking flow later.

## Voice Architecture

The project currently supports two validation modes:

- `WordByWordRealtime`: default prototype mode using immediate keyword recognition for visible word-by-word acceptance.
- `FullPhrase`: optional strict mode using full phrase transcript validation.

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.

Whisper remains available for full-phrase or experimental recognition paths and should not be removed, but it should not be forced as the only validation path.

Unity Dictation and Azure are not part of the current project plan.

## Phrase Ownership

The ritual phrase:

- Starts with 1 word.
- Is shared by all active players.
- Grows by 1 word after a full active table rotation.
- Does not grow after each individual player.
- Does not fork per player.
- Must remain separate from `SpellPhraseLibrary`.

## Paused Systems

Notebook, cards, lore, demon reactions, campaign objectives, assets, scenes, prefabs, and networking should not be changed during core ritual tasks unless explicitly requested.
