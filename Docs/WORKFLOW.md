# Workflow

This document describes the current development workflow for Incantation.

For contributor rules, read `AGENTS.md`. For current project state, read `Docs/START_HERE.md` and `Docs/CURRENT_PROJECT_STATE.md`.

## Task Philosophy

Incantation is developed through small validated tasks.

One task should have a clear purpose and a narrow surface area. The goal is to preserve the playable ritual while improving one thing at a time.

Before starting, ask:

- What memorable moment does this task create or protect?
- Does it strengthen the book, table, hourglass, voice, phrase, or social pressure?
- Which system owns the state being changed?
- Can this be validated in Unity today?

If the answer is unclear, update documentation or ask for clarification before implementation.

## Codex Workflow

For each Codex task:

1. Read `AGENTS.md`.
2. Read `Docs/START_HERE.md`.
3. Read the relevant system documents inside `Docs`.
4. Run `git status` before editing.
5. Respect existing user changes.
6. Keep edits focused on the requested task.
7. Do not modify paused systems unless explicitly requested.
8. Validate the result.
9. Explain architectural decisions after the task.

Documentation-only tasks must not modify scripts, scenes, prefabs, or assets.

## Git Workflow

Run `git status` before editing.

Expected discipline:

- Keep one task to one completed change set.
- Do not commit `Library`, `Temp`, `Logs`, `Obj`, build folders, or generated Unity noise.
- Do not revert unrelated user changes.
- If unrelated scene or asset changes are already present, leave them alone.
- Stage only files that belong to the task.
- Use clear commit messages.

Example commit messages:

- `Add Game Design Pillars Documentation`
- `Document Inspector Reference`
- `Add Lobby Ready Flow`
- `Fix Book Movement Retry Timeout`

## Branch Strategy

Current branch work may happen on feature or rebuild branches.

Recommended branch behavior:

- Use small task branches when starting new work.
- Prefer the `codex/` prefix for Codex-created branches unless the user requests another name.
- Keep branch scope aligned with one milestone or one task.
- Do not start broad refactor branches while the core ritual loop is still stabilizing.

## Commit Philosophy

A commit should represent a reviewed, compiling, tested unit of work.

For code changes, do not commit until:

- The project compiles.
- The relevant scene opens.
- Play Mode validation passes for the changed loop.
- The change does not alter paused systems unintentionally.

For documentation-only changes, do not commit until:

- The docs are internally consistent.
- Cross-references point to the authoritative source.
- The diff contains docs only.

## Prototype Workflow

The current prototype workflow is local-first.

Use local debug occupants to validate the ritual until lobby seating is stable. Preserve the ability to test locally even after lobby work begins.

Prototype validation should confirm:

1. Occupied seats are found.
2. The single real book moves to the active occupied seat.
3. The book follows physical order.
4. The phrase starts with one word.
5. The phrase is shared by all active players.
6. `WordByWordRealtime` accepts expected words immediately.
7. Wrong words reject and allow retry while time remains.
8. `FullPhrase` remains available when selected.
9. The phrase grows only after a full active table rotation.
10. Timeout does not restart duplicate listeners, timers, or movement.

## Production Workflow

Production work should preserve the prototype's strongest emotional loop.

Recommended production order:

1. Stabilize the local ritual.
2. Add lobby.
3. Add ready check.
4. Add automatic seating from lobby state.
5. Formalize retry, timeout, failure, and elimination.
6. Add networked player flow after local lobby handoff is stable.
7. Re-enable interference cards.
8. Re-enable demon reactions.
9. Add campaign objectives.
10. Polish multiplayer.

Do not begin production networking before lobby and seating handoff are clear.

## Documentation Workflow

When a decision changes, update all affected sources of truth in the same task:

- `Docs/GAMEPLAY_TRUTH.md` for gameplay law.
- `DECISIONS.md` and `Docs/WHY.md` for rationale.
- Relevant system document for implementation guidance.
- `Docs/CURRENT_PROJECT_STATE.md`, `Docs/Roadmap.md`, or `Docs/MILESTONES.md` when milestone status changes.

Prefer one authoritative source and cross-reference it instead of duplicating large blocks everywhere.
