# Roadmap

## Current Priority

Priority 1 is the clean core ritual loop and reliable voice interaction.

Do not expand secondary systems until the table ritual works end to end.

## v0.1 Prototype State

The current prototype already demonstrates:

1. One cursed book.
2. Physical seat traversal.
3. Growing incantation by full active table rotation.
4. Local debug seat occupants.
5. `WordByWordRealtime` validation as the default mode.
6. Optional `FullPhrase` strict validation.
7. Realtime visual word absorption.
8. Wrong word rejection feedback.
9. Hourglass timer pressure.
10. Book movement after ritual acceptance.
11. Ambient audio.
12. Fire flicker.
13. Hourglass light possession effect.
14. Room veil and dark cabin ambience.

## Next Major Milestone

Lobby is next.

The next milestone should replace local debug occupants with a real pre-ritual player flow:

1. Lobby entry.
2. Ready check.
3. Automatic seat assignment.
4. Ritual start handoff into the current table loop.

Networking is not implemented yet and should not be assumed by documentation or tasks.

## Vertical Slice Order

1. Preserve the current one-book ritual.
2. Preserve physical seat traversal through `SeatManager`.
3. Preserve phrase growth after a full active table rotation.
4. Keep `WordByWordRealtime` as the default prototype feel.
5. Keep `FullPhrase` available as optional strict validation.
6. Add lobby.
7. Add ready check.
8. Add automatic seating from lobby state.
9. Add clear failure, retry, timeout, and elimination flow.
10. Add networked player flow only after local lobby and ritual handoff are stable.
11. Re-enable interference cards only after the core loop is dependable.
12. Re-enable demon reactions after the ritual can stand on its own.

## Paused

- Notebook.
- Cards.
- Lore delivery.
- Demon reactions.
- Campaign objectives.
- Networking.
- `SpellPhraseLibrary` changes beyond preserving separation from ritual words.
- `WhisperSandbox` changes.
- Multiple gameplay books.
- Walking characters.

## Not In Current Voice Plan

- Unity Dictation.
- Azure voice services.
- Forcing Whisper as the only validation path.
