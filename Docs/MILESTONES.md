# Milestones

## Completed Prototype Milestone: v0.1 Local Ritual

The v0.1 prototype proves the core table ritual can function locally.

Completed prototype beats:

1. One cursed book exists as the ritual focus.
2. The book moves through physical seats.
3. `SeatManager` owns physical seat order.
4. Active local debug occupants can participate.
5. The incantation starts with one word.
6. The phrase grows by one word after a full active table rotation.
7. `WordByWordRealtime` validation gives immediate word acceptance.
8. `FullPhrase` validation remains available as a strict option.
9. Wrong words can be rejected.
10. Accepted words can be visually absorbed.
11. Hourglass pressure exists.
12. The book moves after ritual acceptance.
13. Ambience and lighting support the ritual mood.

## Current Milestone: Lobby

Lobby is the next major milestone.

Goal:

Move from local debug occupants to a real pre-ritual player flow.

Expected scope:

1. Player enters lobby.
2. Player readies up.
3. Ready rules decide when the ritual can begin.
4. Active players are assigned to seats.
5. The existing ritual loop starts from those occupied seats.

Out of scope:

- Networking implementation unless explicitly scoped.
- Cards.
- Demon reactions.
- Campaign objectives.
- New art pass.
- Multiple gameplay books.
- Walking characters.

## Next Milestone: Failure And Elimination

After lobby handoff is stable, formalize:

1. Retry rules.
2. Timeout behavior.
3. Elimination rules.
4. Skipping eliminated seats.
5. End condition for Last Priest Standing.

## Later Milestones

Future work should happen only after the core loop remains stable:

1. Networking.
2. Interference cards.
3. Demon reactions.
4. Multiplayer polish.
5. Campaign objectives.
6. Lore delivery.

## Milestone Standard

Every milestone must strengthen the table, book, hourglass, voice, or social chaos.

If a milestone distracts from those pillars, it should wait.

For known technical debt, current limitations, and production priority detail, read `Docs/FUTURE_DEVELOPMENT.md`.
