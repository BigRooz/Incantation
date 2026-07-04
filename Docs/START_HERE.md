# Start Here

This is the onboarding map for Incantation.

If every prior conversation about the project disappeared, this document should tell a new developer where to look, what the project currently is, why it is shaped this way, and what to do next.

## Project In One Page

Incantation is a Unity 6 multiplayer party game set in a dark fantasy horror atmosphere.

The game is not primarily a horror game. Horror is the mood. The real product is social pressure around a table: tension, laughter, betrayal, stress, surprise, mistakes, and stories players remember after the session ends.

The core fantasy is:

> Speak forbidden words. Betray your friends. Be the last mage standing.

Players are corrupted priests seated around a ritual table. One cursed book moves from seat to seat and demands a spoken ritual phrase before the hourglass runs out. The demon trapped inside the book manipulates the ritual, but demon reactions are intentionally paused until the core table, book, hourglass, and voice loop is reliable.

## Authoritative Documentation

Read these first, in this order:

1. `AGENTS.md`
   - Task rules for AI and human contributors.
   - Project identity, hard boundaries, coding rules, and workflow discipline.

2. `Docs/GAMEPLAY_TRUTH.md`
   - The gameplay source of truth.
   - If gameplay behavior conflicts with this document, `GAMEPLAY_TRUTH.md` wins until it is deliberately updated.

3. `Docs/CURRENT_PROJECT_STATE.md`
   - What the prototype currently does and does not do.

4. `Docs/CoreRitualLoopArchitecture.md`
   - The intended architecture for the seated ritual loop.
   - Use before changing ritual orchestration, turn flow, phrase progression, voice validation, hourglass pressure, or book movement.

5. `Docs/TechnicalArchitecture.md`
   - System ownership boundaries and current prototype components.

6. `Docs/Roadmap.md` and `Docs/MILESTONES.md`
   - Current priority and upcoming milestone order.

7. `DECISIONS.md`
   - Durable design and technical decisions with rationale.

Supporting references:

- `Docs/PROJECT_KNOWLEDGE.md`: preserved project memory, runtime migration notes, code map, known tensions, and validation expectations.
- `Docs/CORE_GAMEPLAY_RULES.md`: expanded explanation of the core laws, subordinate to `GAMEPLAY_TRUTH.md`.
- `Docs/BookSystem.md` and `Docs/BOOK_STATE_MACHINE.md`: book ownership, movement, and state direction.
- `Docs/IncantationSystem.md` and `Docs/VoiceMagicSystem.md`: ritual phrase and voice recognition boundaries.
- `Docs/UNITY_SCENE_SETUP.md`: current scene expectations.
- `Docs/CODING_STANDARDS.md`: implementation standards.
- `Docs/Networking.md`: networking status and boundary.

## Current Prototype State

The current playable prototype is a local v0.1 ritual slice, not the full multiplayer product.

Implemented or present:

1. One real cursed book.
2. Physical seat traversal around the table.
3. Phrase growth by full active table rotation.
4. Local debug seat occupants for Play Mode testing.
5. `WordByWordRealtime` validation as the default prototype mode.
6. `FullPhrase` validation as an optional strict mode.
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
3. Production automatic seating from lobby players.
4. Real multiplayer networking flow.
5. Networked player seating.
6. Production elimination flow.
7. Interference cards.
8. Demon reactions.
9. Campaign objectives.

Lobby is the next major milestone.

## Core Gameplay Loop

The golden loop is:

1. The cursed book moves to a seated player.
2. The hourglass starts.
3. The shared visible ritual phrase is shown.
4. The active player speaks the visible phrase.
5. Voice recognition produces word or phrase candidates.
6. Ritual validation checks those candidates against the selected validation mode.
7. Correct words are visually absorbed in realtime in `WordByWordRealtime`.
8. Wrong words are rejected while time continues.
9. If the player succeeds, the book moves to the next active seat.
10. If the player fails but time remains, they may retry.
11. If the hourglass runs out, the turn can fail and later elimination rules can apply.
12. After a full active table rotation, the shared phrase gains exactly one word.

The phrase grows by one word per full active table rotation, not by one word per player.

## Why The Current Decisions Exist

Incantation is being built around moments, not feature count.

### One book

There is one real gameplay book because the book is the main character. A separate book per player would turn the ritual into isolated personal turns and weaken the feeling that the table is being manipulated by one cursed object.

### Seated players

Players stay seated so attention stays on faces, voices, the table, the book, the hourglass, and social pressure. Walking characters would pull development and player attention toward navigation and animation instead of the ritual.

### Physical seat order

`SeatManager` owns the physical seat order because the table is a space, not a list of connected players. The book moves through the room according to configured physical order, not seat numbers, hierarchy order, player join order, or network index.

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

### Rotation-based phrase growth

The phrase grows after a full active table rotation so all active players share the same escalating memory challenge. Growing after every player would make difficulty spike too quickly and would make players recite different effective burdens within the same rotation.

### Word-by-word realtime voice by default

`WordByWordRealtime` is the default prototype mode because immediate word acceptance creates a stronger table feel: players see the phrase being consumed as they speak, and wrong words can sting immediately while the hourglass keeps pressure on.

`FullPhrase` remains available as an optional strict mode because full recitation may still be useful for testing, alternate difficulty, or future variants.

### Windows keyword recognition first

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay because it supports immediate keyword-style feedback. Whisper remains in the project for full-phrase or experimental recognition paths, but it must not be forced as the only path.

Unity Dictation and Azure voice services are not part of the current plan.

### Paused secondary systems

Notebook, cards, lore delivery, demon reactions, campaign objectives, and networking polish are paused because the game must be fun with only the table, book, hourglass, shared phrase, voice validation, retry, timeout, and elimination. Secondary systems should only return once they clearly strengthen that loop.

## Architecture Ownership

Keep these boundaries intact:

- `SeatManager` owns configured physical seat order and traversal.
- The book system owns movement of the single real book.
- The phrase/incantation system owns the current shared ritual phrase and accepted word state.
- Voice recognizers produce candidates only.
- Ritual validation decides whether candidates satisfy the selected mode.
- The hourglass owns timer pressure and timeout signaling.
- Ritual orchestration coordinates the systems and prevents duplicate movement, listening, timer starts, and turn resolution.
- Game mode rules eventually own ending conditions.

Systems that must not own gameplay authority:

- `BookGhost`.
- Visual-only scene objects.
- UI displays.
- Voice chat.
- Spell/card phrases.
- Networking transport.
- Demon reactions.
- Lore delivery.

## Current Priority

Priority 1 remains a clean core ritual loop and reliable voice interaction.

Work in this order:

1. Preserve and stabilize the core ritual loop.
2. Preserve one-book movement through physical seats.
3. Preserve hourglass pressure.
4. Preserve `WordByWordRealtime` as the default prototype validation mode.
5. Keep `FullPhrase` as an optional strict mode.
6. Add lobby.
7. Add ready check.
8. Add automatic seating from lobby state.
9. Add clear retry, timeout, failure, and elimination flow.
10. Add networked player flow only after the local lobby handoff is stable.
11. Re-enable interference cards only after the core loop is dependable.
12. Re-enable demon reactions after the ritual can stand on its own.

## Development Workflow

Use small validated tasks.

1. Read `AGENTS.md` and the relevant docs before changing code.
2. Run `git status` before editing.
3. Keep each task focused.
4. Prefer extending existing systems over creating new architecture.
5. Do not modify scenes, prefabs, assets, networking, cards, notebook, lore, demon reactions, `SpellPhraseLibrary`, or `WhisperSandbox` unless explicitly requested.
6. Validate compile and Play Mode behavior for code tasks.
7. Commit one completed task at a time.
8. Push after the task is reviewed, compiling, tested, and committed.

Documentation-only tasks must not change gameplay code, scenes, prefabs, or assets.

## Project History

The project has moved from broad game concept toward a narrow playable vertical slice.

Key historical decisions now captured in the repository:

1. The identity changed from "horror game" toward "competitive social party game in dark fantasy horror atmosphere."
2. The table ritual became the center of development.
3. The cursed book became the main character and turn driver.
4. Characters were locked to seated play to keep focus on voice and social pressure.
5. The prototype used local debug occupants to prove the ritual loop before lobby and networking.
6. Phrase growth was corrected to one word per full active table rotation.
7. Realtime word-by-word validation became the default prototype feel.
8. Whisper was retained but moved out of the critical realtime path.
9. Unity Dictation and Azure voice services were rejected for the current plan.
10. Cards, notebook, lore delivery, demon reactions, campaign objectives, and networking polish were paused until the core loop is reliable.

If future work changes any of these decisions, update `DECISIONS.md`, `GAMEPLAY_TRUTH.md`, and the relevant system document in the same task.

For deeper preserved context about current runtime migration, code ownership, and known traps, read `Docs/PROJECT_KNOWLEDGE.md`.

## New Developer Self-Check

Before starting a task, a developer should be able to answer:

1. What memorable moment does this task create or protect?
2. Does it keep the book, table, hourglass, shared phrase, and voice interaction central?
3. Which system owns the state being changed?
4. Does the change preserve one real gameplay book?
5. Does the change preserve seated play?
6. Does phrase growth still happen after a full active table rotation?
7. Does it avoid paused systems unless explicitly requested?
8. How will it be validated in Unity?

If the answer is unclear, pause and update documentation or ask for clarification before implementing.
