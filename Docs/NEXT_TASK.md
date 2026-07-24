# Next Task

This document defines the immediate production objective for Incantation.

Purpose: keep the next task focused on one milestone, not the whole backlog.

Questions answered here:

- What should be built next?
- Why is it next?
- What is in scope?
- What is out of scope?
- Which documents should be read before starting?

This document does not contain long-term roadmap planning, historical milestone memory, detailed architecture, or general backlog ideas.

Read next: `Docs/PROJECT_STATUS.md` for current reality, then `Docs/Roadmap.md` and the relevant system documents before implementation.

## Immediate Objective

Add the first lobby flow that can hand ready local players into the existing seated ritual loop.

The FishNet connection foundation now exists, but this objective remains local-first. Do not synchronize lobby or gameplay state as part of this task.

## Why This Is Next

The current prototype uses local debug occupants for Play Mode testing. That proves the ritual loop, but it is not the production player entry flow.

Lobby is the next major milestone because it connects players to seats before networking, cards, demon reactions, or campaign systems return.

## Scope

The first lobby task should focus on:

1. Lobby entry.
2. Ready state.
3. Minimum and maximum player rules for the prototype.
4. Automatic assignment of ready players to physical Seats through the Seat system.
5. Handoff from lobby state into the current table ritual loop.
6. Preservation of local debug occupant testing until lobby seating is stable.

## Out Of Scope

Do not include:

- Production networking flow beyond the installed FishNet localhost foundation.
- Networked player seating.
- Interference cards.
- Demon reactions.
- Campaign objectives.
- New art pass.
- Multiple gameplay books.
- Walking characters.
- Replacing the core ritual loop with a broad rewrite.

## Required Reading

Before implementing, read:

1. `AGENTS.md`
2. `Docs/START_HERE.md`
3. `Docs/PROJECT_STATUS.md`
4. `Docs/GAMEPLAY_TRUTH.md`
5. `Docs/CoreRitualLoopArchitecture.md`
6. `Docs/TechnicalArchitecture.md`
7. `Docs/PROJECT_KNOWLEDGE.md`
8. `Docs/INSPECTOR_REFERENCE.md`

## Completion Criteria

The task is not complete until:

1. Ready local players can be represented before the ritual starts.
2. Ready players can be assigned to physical Seats without using hierarchy order, seat number order, player join order, or network index as the authority.
3. The ritual can start from lobby state using the existing one-book table loop.
4. Local debug occupant testing remains available.
5. Documentation that changed from the task is updated before commit.
