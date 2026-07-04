# Documentation Governance

This document defines how Incantation documentation is owned, updated, and maintained.

Purpose: make the repository the permanent memory of the project.

Questions answered here:

- What kinds of documentation exist?
- Which document owns each kind of knowledge?
- How should documentation be updated when the project changes?
- How are conflicts between documents resolved?
- How does the documentation stay useful years from now?

This document does not contain gameplay rules, current project status, milestone history, implementation details, or backlog planning.

Read next: `Docs/START_HERE.md` for the official reading order, then the document that owns the question you are trying to answer.

## Philosophy

Documentation is part of production quality.

The repository is the permanent memory of Incantation. ChatGPT conversations are temporary. Human memory is imperfect. Future contributors must be able to understand the project using only the repository.

Documentation is not an archive of every thought. It is an active production system that teaches the current project, protects durable decisions, and preserves historical context in the right place.

The goal is clarity, not volume.

## Documentation Categories

Every document belongs to one of three categories.

### Immutable Documents

Immutable documents explain the identity of the project.

They rarely change. Update them only when the long-term vision, core design law, or production philosophy deliberately changes.

Examples:

- `Docs/GAMEPLAY_TRUTH.md`
- `Docs/GAME_DESIGN_PILLARS.md`
- `Docs/CORE_GAMEPLAY_RULES.md`
- `Docs/ProjectVision.md`
- `Docs/WHY.md`
- `DECISIONS.md`

Immutable documents may be refined for clarity, but they must not become status reports or backlog lists.

### Living Documents

Living documents describe today's project.

They are rewritten continuously. History must not accumulate inside them. When the project changes, update living documents so they describe the current reality.

Examples:

- `Docs/PROJECT_STATUS.md`
- `Docs/NEXT_TASK.md`
- `Docs/Roadmap.md`
- `Docs/FUTURE_DEVELOPMENT.md`
- `Docs/INSPECTOR_REFERENCE.md`
- `Docs/START_HERE.md`
- `Docs/WORKFLOW.md`
- Current architecture and system reference documents

Living documents answer "what is true now?" They should not preserve old states except when a current limitation needs context.

### Historical Documents

Historical documents preserve project memory.

They only grow. They do not describe today's state and should not be used as current instructions unless a living or immutable document points to a specific historical lesson.

Examples:

- `Docs/MILESTONES.md`
- `Docs/LESSONS_LEARNED.md`
- `CHANGELOG.md`

Historical documents answer "what happened and what did we learn?" They should not become the source of current priority, implementation authority, or active workflow.

## Document Ownership

Every important concept should have one authoritative document.

Use this ownership map when deciding where information belongs:

| Knowledge | Authoritative document |
| --- | --- |
| Project identity and sacred gameplay law | `Docs/GAMEPLAY_TRUTH.md` |
| Design pillars and emotional target | `Docs/GAME_DESIGN_PILLARS.md` |
| Decision rationale | `Docs/WHY.md` and `DECISIONS.md` |
| Current reality | `Docs/PROJECT_STATUS.md` |
| Immediate next objective | `Docs/NEXT_TASK.md` |
| Milestone order and production priorities | `Docs/Roadmap.md` and `Docs/FUTURE_DEVELOPMENT.md` |
| Completed milestone memory | `Docs/MILESTONES.md` |
| Practical lessons | `Docs/LESSONS_LEARNED.md` |
| Architecture boundaries | `Docs/TechnicalArchitecture.md` and focused system docs |
| Code ownership map and migration notes | `Docs/PROJECT_KNOWLEDGE.md` |
| Inspector tuning | `Docs/INSPECTOR_REFERENCE.md` |
| Contributor workflow | `Docs/WORKFLOW.md` and `AGENTS.md` |

If no document clearly owns a concept, improve the structure instead of duplicating the same explanation in multiple places.

## Update Rules

Before completing any production task, ask:

1. Did this change today's project?
2. Did this introduce or change an architectural decision?
3. Did this modify gameplay behavior?
4. Did this add Inspector tuning worth preserving?
5. Did this invalidate any documentation?
6. Did this create knowledge that only exists inside this conversation?

If the answer is yes, update the relevant documentation before the task is complete.

Rules:

- Rewrite living documents so they describe today.
- Append only to historical documents.
- Update immutable documents only when the core vision, rule, or rationale intentionally changes.
- Prefer references over copied blocks.
- Remove obsolete text instead of leaving contradictory notes.
- Keep each document focused on one responsibility.

## Conflict Resolution

If two documents disagree, the higher authority wins until the conflict is deliberately resolved.

Authority order:

1. `AGENTS.md` for contributor rules, task boundaries, and workflow constraints.
2. `Docs/GAMEPLAY_TRUTH.md` for gameplay law.
3. `DECISIONS.md` and `Docs/WHY.md` for durable decisions and rationale.
4. Focused system documents for implementation guidance inside their system.
5. `Docs/PROJECT_STATUS.md` for current reality.
6. `Docs/NEXT_TASK.md`, `Docs/Roadmap.md`, and `Docs/FUTURE_DEVELOPMENT.md` for production direction.
7. Historical documents for memory, not active authority.

When resolving a conflict, update all affected documents in the same task so the repository does not keep two sources of truth.

## Document Navigation Standard

Every major document should make its role obvious near the top.

Prefer this shape:

- Purpose.
- Questions answered here.
- What this document does not contain.
- What to read next.

Navigation should feel like guided onboarding. Contributors should not need to guess which document matters.

## Long-Term Maintenance

Treat documentation like production code.

As Incantation evolves:

- Merge documents when responsibilities overlap.
- Split documents only when a responsibility becomes too large.
- Remove obsolete documents when they no longer justify their existence.
- Eliminate duplicated explanations.
- Keep names direct and durable.
- Keep the reading order current.
- Keep living documents free of accumulated history.

Before creating a new document, ask:

1. Does another document already own this knowledge?
2. Would improving an existing document be clearer?
3. Will this document still matter one year from now?

If not, do not create it.

## Project Memory Test

Before considering major documentation work complete, imagine every previous conversation has disappeared.

A new developer receives only the repository.

They should be able to understand:

- The vision.
- Today's project state.
- The architecture.
- What not to change.
- What to build next.
- Why major decisions were made.
- How to continue development confidently.

If any answer is unclear, improve the documentation.
