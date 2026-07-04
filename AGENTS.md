# AGENTS.md - Incantation

## Project Identity

You are working on **Incantation**, a Unity 6 multiplayer party game set in a dark fantasy horror atmosphere.

Tagline:

> Speak forbidden words. Betray your friends. Be the last mage standing.

Incantation is not primarily a horror game.

It is a competitive social party game set in a dark fantasy horror atmosphere.

The goal is to create memorable moments between players.

If a feature does not create tension, laughter, betrayal, stress, surprise, or a story players will remember, reconsider it.

## Core Vision

Incantation revolves around one cursed book placed at the center of the table.

Players are priests corrupted by a demon trapped inside the book.

The demon wants to be released.

The players believe they can inherit its power, but the demon is manipulating them.

In Last Priest Standing mode, the last surviving player wins, but the twist is that the winner is also absorbed by the book.

The book is the main character of the game.

The hourglass is the pressure system.

The demon is the master of ceremonies, but demon reactions are paused until the core table and voice loop is reliable.

## Current v0.1 Prototype State

The current playable prototype demonstrates the seated table ritual, not the full multiplayer product.

Implemented or present in the current prototype:

1. One real cursed book.
2. Physical seat traversal around the table.
3. Phrase growth by full active table rotation.
4. Local debug seat occupants for Play Mode testing.
5. Word-by-word realtime voice validation as the default prototype feel.
6. Optional full-phrase validation mode.
7. Realtime visual word absorption.
8. Wrong word rejection feedback.
9. Hourglass timer pressure.
10. Book movement after ritual acceptance.
11. Ambient audio.
12. Fire flicker.
13. Hourglass light possession effect.
14. Room veil and dark cabin ambience.

Not implemented yet:

1. Lobby.
2. Ready check.
3. Real multiplayer networking flow.
4. Networked player seating.
5. Production elimination flow.
6. Interference cards.
7. Demon reactions.
8. Campaign objectives.

Lobby is the next major milestone.

## Current Core Flow

1. Local debug occupants are assigned to seats for testing.
2. Players are treated as seated around the ritual table.
3. One real cursed book moves from active seat to active seat.
4. The ritual phrase starts with 1 word.
5. Every active player says the same visible phrase when the book reaches them.
6. In the default `WordByWordRealtime` mode, each expected word can be accepted immediately as it is spoken.
7. Incorrect words produce rejection feedback while the timer continues.
8. When the visible phrase is accepted, the book moves to the next active seat.
9. After a full active table rotation, 1 word is added to the shared phrase.
10. The phrase grows over rotations until players fail, time out, or future game-mode rules end the ritual.

Phrase growth is one word per full active table rotation, not one word per player.

## Golden Gameplay Loop

1. The cursed book moves to a player.
2. The hourglass flips or starts.
3. The current shared ritual phrase is shown.
4. The active player must speak the visible phrase.
5. Voice recognition provides spoken word or phrase candidates.
6. Ritual validation checks those candidates according to the selected validation mode.
7. Correct words are visually absorbed in realtime in `WordByWordRealtime` mode.
8. Wrong words are rejected with feedback.
9. If the player succeeds, the ritual continues and the book moves.
10. If the player fails before time runs out, they may retry while time remains.
11. If the hourglass runs out, the turn can fail and later elimination rules can apply.
12. After a full active table rotation, the phrase gains 1 new word.

## Current Priority

Priority 1 remains a clean core ritual loop and reliable voice interaction.

Focus on:

1. Lobby.
2. Ready check.
3. Automatic seating from lobby players.
4. Single book movement.
5. Hourglass pressure.
6. Shared phrase progression.
7. `WordByWordRealtime` validation as the default prototype mode.
8. `FullPhrase` validation as an optional strict mode.
9. Player failure, retry, timeout, and elimination.

Do not expand secondary systems until this loop works end to end.

## Voice Recognition Rules

- The current default prototype validation mode is `WordByWordRealtime`.
- `WordByWordRealtime` uses `WindowsKeywordVoiceRecognizer` for immediate keyword recognition and visual word validation.
- `FullPhrase` remains available as an optional strict mode that validates a full phrase transcript.
- Whisper is kept in the project and may be used for full-phrase or experimental recognition paths.
- Do not force Whisper as the only validation path.
- Windows keyword recognition is currently preferred for realtime prototype gameplay.
- Do not use Unity Dictation.
- Do not use Azure voice services.
- Ritual validation should prefer the current visible phrase and the selected validation mode.
- `SpellPhraseLibrary` is separate from ritual words.
- Do not merge spell/card phrases into the core ritual word vocabulary unless explicitly requested.

## Do Not Touch Unless Asked

- Notebook.
- `SpellPhraseLibrary`.
- `WhisperSandbox`.
- Assets.
- Visuals.
- Networking.
- Cards.
- Lore delivery.
- Demon reactions.

These systems are paused or out of scope until the core ritual loop and reliable voice interaction are working.

Documentation tasks may describe current visuals and ambience, but must not modify assets, scenes, prefabs, or scripts unless explicitly requested.

## Absolute Design Rules

- Players stay seated for the entire ritual.
- Characters do not walk around.
- Characters only move their head, mouth, body subtly, eyes, and hands.
- The game happens around the table.
- There is one main cursed book.
- Do not create multiple gameplay books.
- The book creates tension by choosing who plays next.
- The hourglass creates urgency.
- Other players can interfere once interference systems are re-enabled.
- Fun comes before realism.
- Voice interaction is central to the game.
- Do not add mechanics that distract from the table, the book, the voice, or the social chaos.

## Unity Architecture

Current project structure:

Assets/
- Models/
- Prefabs/
- Scenes/
- Scripts/
  - Book/
  - CoreRitualLoop/
  - Hourglass/
  - Incantation/
  - Lighting/
  - Player/
  - Ritual/
  - Seats/
  - Voice/

Important scene systems:

- Room: visual environment only.
- Players: player character instances or local debug occupants.
- Book: book-related objects.
- SeatSystem: logical seat system.
- Managers: global managers.
- Ritual controller objects: prototype ritual orchestration and core-loop bridge components.
- Lighting and ambience: room veil, fire flicker, hourglass possession light, and ambient sound.

Do not move or rename major systems without a clear reason.

## Seat System

Each Seat represents a logical player position.

A Seat may contain:

- PlayerSpawn.
- BookTarget.
- BookGhost.
- LookTarget.
- LeftHand.
- RightHand.
- ChairClickZone.

The chair is visual.

The Seat is logic.

`SeatManager` owns the configured physical seat order.

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

Debug occupants are for local testing only and must not be treated as the final lobby or networking model.

BookGhost is only a visual placement reference.

BookGhost must never contain gameplay scripts.

BookModel is the real book.

The real book moves toward BookGhost or BookTarget depending on the current BookMover setup.

## Book System

The cursed book is unique.

Do not create one book per player unless explicitly requested.

BookModel is the real moving book.

BookTarget defines where the book should go.

BookGhost is a visual preview used in the editor.

The book should move around the table in a dramatic way, not simply teleport.

The book may hesitate, slow down, or fake choosing a player to create tension.

## Coding Rules

- Always provide complete C# scripts when modifying code.
- Do not give partial snippets unless explicitly asked.
- Use PascalCase for classes.
- Use camelCase for fields and variables.
- Keep scripts focused on one responsibility.
- Avoid giant scripts that control everything.
- Prefer serialized references in the Inspector over GameObject.Find.
- Avoid hardcoded object names when a serialized reference is better.
- Do not add temporary test scripts without marking them clearly.
- Remove obsolete scripts when they are replaced.
- Do not silently change architecture.

## Game Modes

### Last Priest Standing

Competitive mode.

The demon has corrupted the priests.

Players betray one another to be the last survivor.

The winner is ultimately absorbed by the book.

### Campaign

Cooperative mode.

Players work together to stop the demon and seal it back inside the book.

Campaign uses the same core systems but different objectives.

## Development Philosophy

Build moments, not features.

Before adding a feature, ask:

> What memorable moment does this create?

If the answer is weak, do not implement it yet.

Work in this order:

1. Core loop.
2. Book system.
3. Hourglass system.
4. Voice incantation system.
5. Player elimination.
6. Interference cards.
7. Demon reactions.
8. Multiplayer polish.

## Development Workflow

This project is developed using small validated tasks.

Workflow:

1. One Codex conversation = one task.
2. One Git commit = one completed task.
3. Prefer small isolated tasks.
4. Do not modify more than necessary.
5. Never start a new task until the previous one is:
   - Reviewed.
   - Compiling.
   - Tested inside Unity.
   - Committed.
   - Pushed.
6. Bug fixes remain inside the same task conversation until the task is complete.
7. Read AGENTS.md before making any code changes.
8. Read `Docs/START_HERE.md` and follow the official documentation reading order before implementing new systems.
9. Prefer extending existing systems over creating new ones.
10. Keep systems modular and event-driven.
11. Avoid putting gameplay logic inside visual components.
12. Explain architectural decisions after every completed task.

Documentation is production memory.

- Treat documentation as production code.
- Use `Docs/DOCUMENTATION_GOVERNANCE.md` for document ownership, lifecycle, update rules, and conflict resolution.
- Use `Docs/PROJECT_STATUS.md` for today's project state.
- Use `Docs/NEXT_TASK.md` for the immediate production objective.
- Do not leave important gameplay, architecture, workflow, Inspector, or production knowledge only inside a conversation.
- When documentation becomes outdated, update it before considering the task complete.

## Git Rules

- Run git status before editing.
- Keep the project working before committing.
- Commit after every meaningful feature.
- Use clear commit messages.
- Do not commit Library, Temp, Logs, Obj, or build folders.
- Respect the Unity .gitignore.

Example commit messages:

- Add Seat System
- Add Book Target Movement
- Add Voice Incantation Prototype
- Fix BookGhost Script Conflict
- Update Prototype Documentation

## When Unsure

Do not guess randomly.

Prefer asking for clarification when:

- A feature affects core gameplay.
- A script might break the scene.
- A system conflicts with the vision.
- There are multiple possible architectures.

The goal is to finish a playable vertical slice quickly while preserving the identity of Incantation.
