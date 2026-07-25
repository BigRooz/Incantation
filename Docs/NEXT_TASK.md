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

Synchronize the remaining lobby presentation and character selection through `NetworkPlayer`.

## Why This Is Next

The FishNet foundation, permanent `NetworkPlayer` architecture, and Book-driven LAN Ritual Seal entry flow are implemented. The remaining lobby presentation and character choice still contain local or duplicated state that must be migrated onto this networking identity.

Using `NetworkPlayer` as the shared authority keeps each connected player's identity and lobby state in one place and prevents local lobby models from diverging across peers.

## Next Planned Milestones

1. Make remaining Book lobby roles and name presentation observe active `NetworkPlayer` state.
2. Remove duplicated local lobby state.
3. Replace the diagnostic LAN Seal directory only when the Steam/platform lobby task is approved.

## Out Of Scope

Do not include:

- A new networking framework or competing player-identity architecture.
- Ritual, voice, or elimination synchronization.
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
5. `Docs/Networking.md`
6. `Docs/TechnicalArchitecture.md`
7. `Docs/PROJECT_KNOWLEDGE.md`
8. The focused lobby, Seat, Character, UI, or Book documentation relevant to the selected milestone.

## Completion Criteria

Each migration milestone is not complete until:

1. The selected lobby state is synchronized through `NetworkPlayer` or an explicitly documented authority that consumes it.
2. Duplicated local ownership of that state is removed or converted into a presentation adapter.
3. Existing local debug support remains available where still required.
4. The change preserves physical Seat order and the single-book ritual rules.
5. Host and remote-client behavior are runtime validated.
6. Documentation is updated before commit.
