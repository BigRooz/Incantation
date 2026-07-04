# Start Here

This is the single official entry point for Incantation documentation.

Purpose: guide contributors through the documentation in the correct order.

Questions answered here:

- Which document should be read first?
- Why does each major document exist?
- When should a document be read?
- When should a document not be read?
- Why does the reading order matter?

This document does not contain the full project status, full gameplay law, full architecture, milestone archive, or implementation reference.

Read next: follow the official reading order below.

## Project In One Page

Incantation is a Unity 6 multiplayer party game set in a dark fantasy horror atmosphere.

The game is not primarily a horror game. Horror is the mood. The real product is social pressure around a table: tension, laughter, betrayal, stress, surprise, mistakes, and stories players remember after the session ends.

The core fantasy is:

> Speak forbidden words. Betray your friends. Be the last mage standing.

Players are corrupted priests seated around a ritual table. One cursed book moves from seat to seat and demands a spoken ritual phrase before the hourglass runs out. The demon trapped inside the book manipulates the ritual, but demon reactions are intentionally paused until the core table, book, hourglass, and voice loop is reliable.

## Why The Reading Order Matters

Contributors should not read documentation randomly.

Incantation has immutable identity documents, living current-state documents, and historical memory documents. Reading them out of order can make old context feel current, make implementation details seem more authoritative than design law, or encourage duplicated sources of truth.

The correct order protects the project:

1. First learn the rules of contribution.
2. Then learn how the documentation system works.
3. Then learn the gameplay truth.
4. Then learn today's project state.
5. Then learn the immediate objective.
6. Then read roadmap, architecture, and focused references only as needed.

## Official Reading Order

Read these first, in this order:

1. `AGENTS.md`
   - Why it exists: contributor rules, hard project boundaries, coding rules, and workflow discipline.
   - Read when: before every task.
   - Do not use it for: detailed architecture or milestone history.

2. `Docs/START_HERE.md`
   - Why it exists: official onboarding map and reading order.
   - Read when: starting work, returning after a break, or helping a new contributor.
   - Do not use it for: full status, full architecture, or history.

3. `Docs/DOCUMENTATION_GOVERNANCE.md`
   - Why it exists: documentation categories, ownership, lifecycle, conflict resolution, and maintenance rules.
   - Read when: creating, renaming, deleting, or materially updating documentation.
   - Do not use it for: gameplay rules or production status.

4. `Docs/GAMEPLAY_TRUTH.md`
   - Why it exists: sacred gameplay law.
   - Read when: changing gameplay, resolving gameplay conflicts, or checking whether a feature belongs.
   - Do not use it for: implementation details or current bug tracking.

5. `Docs/GAME_DESIGN_PILLARS.md`
   - Why it exists: emotional target, design pillars, and long-term identity.
   - Read when: evaluating whether a feature creates the right moments.
   - Do not use it for: current task selection or technical ownership.

6. `Docs/WHY.md` and `DECISIONS.md`
   - Why they exist: durable rationale and decision ledger.
   - Read when: changing a major rule, architecture direction, or paused-system boundary.
   - Do not use them for: today's status snapshot.

7. `Docs/PROJECT_STATUS.md`
   - Why it exists: current reality of the prototype.
   - Read when: asking what works, what is incomplete, what is experimental, and what is next.
   - Do not use it for: historical memory or design philosophy.

8. `Docs/NEXT_TASK.md`
   - Why it exists: immediate production objective.
   - Read when: starting the next implementation task.
   - Do not use it for: backlog planning or long-term dreams.

9. `Docs/Roadmap.md` and `Docs/FUTURE_DEVELOPMENT.md`
   - Why they exist: milestone order, known limitations, technical debt, and future production priorities.
   - Read when: planning beyond the immediate next task.
   - Do not use them for: current status if `PROJECT_STATUS.md` disagrees.

10. `Docs/CoreRitualLoopArchitecture.md`, `Docs/TechnicalArchitecture.md`, and `Docs/SYSTEM_DIAGRAM.md`
    - Why they exist: architecture ownership, runtime flow, migration direction, and extension points.
    - Read when: changing ritual orchestration, turn flow, phrase progression, validation, hourglass pressure, book movement, or system ownership.
    - Do not use them for: changing core gameplay law without updating `GAMEPLAY_TRUTH.md`.

11. Supporting references as needed:
    - `Docs/PROJECT_KNOWLEDGE.md`: code map, runtime migration notes, current traps, and validation expectations.
    - `Docs/INSPECTOR_REFERENCE.md`: current recommended Inspector values and prototype tuning.
    - `Docs/WORKFLOW.md`: Git, Codex, branch, task, commit, prototype, production, and documentation workflow.
    - `Docs/CORE_GAMEPLAY_RULES.md`: expanded core laws subordinate to `GAMEPLAY_TRUTH.md`.
    - `Docs/BookSystem.md` and `Docs/BOOK_STATE_MACHINE.md`: book ownership, movement, and state direction.
    - `Docs/IncantationSystem.md` and `Docs/VoiceMagicSystem.md`: ritual phrase and voice recognition boundaries.
    - `Docs/UNITY_SCENE_SETUP.md`: current scene expectations.
    - `Docs/CODING_STANDARDS.md`: implementation standards.
    - `Docs/Networking.md`: networking status and boundary.
    - `Docs/MILESTONES.md`, `Docs/LESSONS_LEARNED.md`, and `CHANGELOG.md`: historical memory.

## Single Source Of Truth

Every important concept has one authoritative document.

Prefer references over copy-paste.

If two documents disagree:

1. Follow the authority order in `Docs/DOCUMENTATION_GOVERNANCE.md`.
2. Update the conflicting documents in the same task.
3. Do not maintain two sources of truth.

## Documentation Categories

Incantation documentation uses three categories:

- Immutable documents: project identity, gameplay law, design pillars, and durable rationale.
- Living documents: today's state, next task, workflow, roadmap, architecture, and reference material.
- Historical documents: milestones, lessons, and changelog.

Read `Docs/DOCUMENTATION_GOVERNANCE.md` before changing documentation structure or responsibility.

## Quick Current-State Pointers

Do not treat this section as the authority. It only points to the owning documents.

- Current project status: `Docs/PROJECT_STATUS.md`.
- Immediate next objective: `Docs/NEXT_TASK.md`.
- Gameplay law: `Docs/GAMEPLAY_TRUTH.md`.
- Architecture ownership: `Docs/TechnicalArchitecture.md` and `Docs/PROJECT_KNOWLEDGE.md`.
- Milestone order: `Docs/Roadmap.md` and `Docs/FUTURE_DEVELOPMENT.md`.
- Completed milestone memory: `Docs/MILESTONES.md`.

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
