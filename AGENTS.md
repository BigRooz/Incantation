# AGENTS.md - Incantation

## Project Identity

You are working on **Incantation**, a Unity 6 multiplayer party horror game.

Tagline:

> Speak forbidden words. Betray your friends. Be the last mage standing.

Incantation is not primarily a horror game.

It is a competitive social party game set in a dark fantasy horror atmosphere.

The goal is to create memorable moments between players.

If a feature does not create tension, laughter, betrayal, stress, surprise, or a story players will remember, reconsider it.

---

## Core Vision

Incantation revolves around one cursed book placed at the center of the table.

Players are priests corrupted by a demon trapped inside the book.

The demon wants to be released.

The players believe they can inherit its power, but the demon is manipulating them.

In Last Priest Standing mode, the last surviving player wins, but the twist is that the winner is also absorbed by the book.

Current core flow:

1. Players enter through a lobby.
2. Players ready up.
3. When the ritual begins, players sit automatically around the table.
4. One real cursed book moves from player to player.
5. The ritual phrase starts with 1 word.
6. Every player says the same visible phrase when the book reaches them.
7. After the book completes a full table rotation, 1 word is added to the shared phrase.
8. The phrase grows over rotations until players fail, time out, or are eliminated.

---

## Golden Gameplay Loop

1. The cursed book moves to a player.
2. The hourglass flips.
3. The current shared ritual phrase is shown.
4. The active player must read the full visible phrase aloud.
5. Whisper checks the spoken phrase.
6. Other players may interfere at any time once interference systems are re-enabled.
7. If the player succeeds, the ritual continues.
8. If the player fails before time runs out, they may retry.
9. If the hourglass runs out, the player can be eliminated.
10. The book moves to another player.
11. After a full table rotation, the phrase gains 1 new word.

The book is the main character of the game.

The hourglass is the pressure system.

The demon is the master of ceremonies, but demon reactions are paused until the core voice loop works.

---

## Current Priority

Priority 1 is a clean core ritual loop and reliable voice recognition.

Focus on:

1. Lobby.
2. Ready check.
3. Automatic seating.
4. Single book movement.
5. Hourglass pressure.
6. Shared phrase progression.
7. Whisper-based phrase recognition.
8. Windows speech recognition fallback.
9. Player failure, retry, timeout, and elimination.

Do not expand secondary systems until this loop works end to end.

---

## Voice Recognition Rules

- Whisper is the primary voice system.
- Windows speech recognition is fallback only.
- Do not use Unity Dictation.
- Do not use Azure voice services.
- Ritual validation should prefer full visible phrase matching.
- `SpellPhraseLibrary` is separate from ritual words.
- Do not merge spell/card phrases into the core ritual word vocabulary unless explicitly requested.

---

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

These systems are paused or out of scope until the core ritual loop and reliable voice recognition are working.

---

## Absolute Design Rules

- Players stay seated for the entire ritual.
- Characters do not walk around.
- Characters only move their head, mouth, body subtly, eyes, and hands.
- The game happens around the table.
- There is one main cursed book.
- The book creates tension by choosing who plays next.
- The hourglass creates urgency.
- Other players can interfere once interference systems are re-enabled.
- Fun comes before realism.
- Voice interaction is central to the game.
- Do not add mechanics that distract from the table, the book, the voice, or the social chaos.

---

## Unity Architecture

Current project structure:

Assets/
- Models/
- Prefabs/
- Scenes/
- Scripts/
  - Book/
  - Player/
  - Seats/

Important scene systems:

- Room: visual environment only.
- Players: player character instances.
- Book: book-related objects.
- SeatSystem: logical seat system.
- Managers: global managers.

Do not move or rename major systems without a clear reason.

---

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

BookGhost is only a visual placement reference.

BookGhost must never contain gameplay scripts.

BookModel is the real book.

The real book moves toward BookGhost or BookTarget depending on the current BookMover setup.

---

## Book System

The cursed book is unique.

Do not create one book per player unless explicitly requested.

BookModel is the real moving book.

BookTarget defines where the book should go.

BookGhost is a visual preview used in the editor.

The book should eventually move around the table in a dramatic way, not simply teleport.

The book may hesitate, slow down, or fake choosing a player to create tension.

---

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

---

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

---

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

---

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
8. Read the relevant documentation inside Docs before implementing new systems.
9. Prefer extending existing systems over creating new ones.
10. Keep systems modular and event-driven.
11. Avoid putting gameplay logic inside visual components.
12. Explain architectural decisions after every completed task.

---

## Git Rules

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

---

## When Unsure

Do not guess randomly.

Prefer asking for clarification when:

- A feature affects core gameplay.
- A script might break the scene.
- A system conflicts with the vision.
- There are multiple possible architectures.

The goal is to finish a playable vertical slice quickly while preserving the identity of Incantation.
