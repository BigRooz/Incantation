# Coding Standards

This document defines how code must be written for Incantation.

It complements `AGENTS.md`, `GAMEPLAY_TRUTH.md`, and `CoreRitualLoopArchitecture.md`.

These standards exist to keep the project simple, readable, testable, and focused on the seated ritual around the book, the hourglass, and the spoken phrase.

## 1. Core Philosophy

- Prefer simplicity over cleverness.
- Prefer readability over shortcuts.
- Prefer explicit ownership over hidden side effects.
- Prefer small focused systems over giant controllers.
- Prefer composition over inheritance-heavy designs.
- Gameplay clarity comes before technical novelty.
- Every implementation should support memorable player moments: tension, laughter, betrayal, stress, surprise, or a story players will remember.

Code should make the ritual easier to understand and safer to change.

If a solution makes the core loop harder to reason about, simplify it.

## 2. Class Rules

Each class must have one clear responsibility.

A class should be easy to describe in one sentence. If that sentence needs "and then also", the class may be doing too much.

Public classes must include XML summaries that explain:

- Responsibility.
- Key dependencies.
- Important ownership boundaries.
- Future TODOs when relevant.

Classes should be Inspector-friendly where appropriate:

- Prefer `[SerializeField]` private fields over public mutable fields.
- Prefer serialized references over `GameObject.Find`.
- Avoid hardcoded scene object names when an Inspector reference is clearer.
- Keep gameplay logic out of visual-only components.

Do not create giant classes that own unrelated systems. Compose focused classes instead.

Examples:

- `PhraseValidator` validates recognized speech against the expected ritual phrase.
- `GrowingIncantationManager` owns the current shared ritual phrase.
- `TurnManager` owns seated turn order and rotation completion.
- `WhisperController` owns ritual listening session flow.

These responsibilities should not be merged into one large script.

## 3. Method Rules

Methods should be small, named clearly, and written for the next person reading them.

Prefer:

- Clear method names that describe intent.
- Early return for invalid or completed conditions.
- Flat logic over deeply nested branches.
- One meaningful operation per method.
- Private helper methods when a public method becomes difficult to scan.

Avoid:

- Long methods that mix validation, state mutation, UI updates, and event dispatch.
- Boolean parameters that make call sites unclear.
- Hidden state changes inside methods that sound like queries.
- Duplicating authority that belongs to another system.

If a method becomes difficult to name, it is probably doing too many things.

## 4. Task Rules

Every development task must:

- Solve one problem.
- Have one objective.
- Modify as few files as possible.
- Explain the root cause.
- Explain the solution.
- Include validation.
- Include a manual test checklist.

Tasks should stay small enough to review, compile, test in Unity, commit, and push before starting the next task.

Do not mix unrelated fixes into a task. If a bug is discovered while working, only fix it in the same task when it directly blocks the task objective.

## 5. Git Rules

One feature

v

One commit

v

One push

Never mix these inside one commit:

- Bug fixes.
- Refactoring.
- New features.

Each commit should describe one completed, validated change.

Before committing:

- Confirm the project still compiles.
- Confirm the relevant Unity scene or Play Mode path has been tested when applicable.
- Confirm no unrelated files are staged.
- Confirm `Library`, `Temp`, `Logs`, `Obj`, and build folders are not committed.

## 6. Migration Rules

When replacing an existing system, never replace everything at once.

Use this migration rhythm:

1. Identify one responsibility to extract.
2. Create or extend the focused system for that responsibility.
3. Wire it through serialized references or a small adapter.
4. Validate the behavior in isolation.
5. Validate the behavior in the current ritual flow.
6. Commit the working step.
7. Repeat.

Do not rewrite working prototype behavior until the replacement path has equivalent or better behavior.

When migrating away from a combined system, preserve working gameplay while responsibilities are moved one at a time.

## 7. Dependencies

Prefer interfaces or focused adapters when a system only needs a narrow capability.

Avoid circular dependencies.

Keep systems independent and authoritative within their own boundaries.

Examples:

- `PhraseValidator` may receive an expected phrase and recognized phrase, but it must not ask `TurnManager` who is active.
- `GrowingIncantationManager` may expose the current phrase, but it must not listen to microphones or move the book.
- `TurnManager` may raise rotation completion, but it must not decide which word is added.
- `WhisperController` may raise recognized phrase candidates, but it must not decide whether the phrase is correct.

Systems should communicate through explicit method calls and events.

No system should bypass the authority defined in `GAMEPLAY_TRUTH.md` or `CoreRitualLoopArchitecture.md`.

## 8. Performance

Avoid unnecessary `Update()` methods.

Prefer events over polling when events already exist or can be added cleanly.

Use `Update()` only when frame-by-frame behavior is truly required, such as smooth movement, animation coordination, or active timer display.

When using `Update()`:

- Keep the work small.
- Exit early when inactive.
- Avoid repeated allocation.
- Avoid repeated scene searches.
- Avoid polling another system when an event would communicate the change directly.

The core ritual should feel responsive without making every system constantly check every other system.

## 9. Documentation

Every public class must have an XML summary.

The summary should include:

- Responsibility.
- Dependencies.
- Future TODOs where relevant.

Example structure:

```csharp
/// <summary>
/// Owns seated ritual turn order and detects full table rotations.
/// Depends on Seat data supplied by the ritual setup flow.
/// TODO: Skip eliminated players after elimination rules are implemented.
/// </summary>
```

Documentation should clarify ownership and intent. It should not restate obvious code line by line.

When a system has authority over gameplay state, document that authority in the class summary.

## 10. Code Review Checklist

Before every commit, ask:

- Does this class have one responsibility?
- Is another system already doing this?
- Can this be tested independently?
- Does this respect `GAMEPLAY_TRUTH.md`?
- Does this respect `CoreRitualLoopArchitecture.md`?
- Does this preserve the seated table ritual?
- Does this keep the book, hourglass, voice, and shared phrase central?
- Does this avoid touching out-of-scope systems?
- Does this avoid unnecessary scene, asset, or architecture changes?
- Is validation included?
- Is there a manual test checklist?

If the answer to any question is unclear, pause and clarify before committing.
