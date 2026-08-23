# Card Ideas

Purpose: preserve the approved card list and define the boundary between the active card presentation foundation and paused card gameplay.

Questions answered here:

- Which card work exists now?
- Which approved card names may receive definitions?
- Which card gameplay remains paused?

This document does not contain spell effect implementation, final incantation text, gameplay law, or current project status.

Read next: `Docs/NEXT_TASK.md` for active work or `Docs/FUTURE_DEVELOPMENT.md` for future milestone order.

## Current Status

Cards are now an explicitly requested data-and-presentation track.

The current foundation includes a three-card physical Spell Hand, `SpellDefinition` ScriptableObject
presentation data, server-authoritative private hand ownership on each persistent
`NetworkPlayer`, and complete-phrase Whisper casting that consumes an accepted card without
applying an effect. It does not include targeting, rarity probability, or effect resolution.

## Locked Hand Rules

- Each active, alive player begins a new match with exactly three authoritative card instances.
- A hand may never exceed three cards.
- At most one card is granted when that player's new authoritative ritual turn begins: zero cards
  when the hand already contains three, otherwise exactly one.
- The authoritative turn sequence prevents the same turn from refilling twice.
- `SpellUsedThisTurn` resets to false exactly once at the start of that player's new turn. An
  accepted authoritative complete-phrase cast consumes the exact instance and sets it true;
  rejection retains the card and leaves it false.
- Card assignment and randomness belong to the server. A card instance ID is distinct from its
  reusable `SpellDefinition.DefinitionId`.
- Detailed instance and definition IDs synchronize only to the owning player.
- The physical three-card `SpellHandController` remains local presentation. Empty authoritative
  slots are hidden. Runtime selection uses the owning gameplay camera's center gaze and permits
  exactly zero or one selected card.
- A successful cast consumes only its captured physical card. Surviving cards compact into their
  new slots while remaining raised; used-this-turn disables another selection/cast without closing
  the hand.
- A living player may prepare the hand until a correlated authoritative physical Book arrival is
  accepted for that player and turn. Arrival cancels any local voice attempt, locks E input, and
  automatically returns raised cards to their table poses.
- Eliminated players receive no refill and cannot interact with their hand. Ghost spell casting is
  not implemented.
- Match return clears the old hand; a later ritual sequence creates a fresh three-card hand with
  new monotonically allocated instance IDs.

Spell voice uses one complete production Whisper transcript and deterministic exact normalization
against `SpellDefinition.spokenIncantation` or explicitly authored accepted forms. Unknown words
remain unknown; there is no fuzzy matching. Spell effects, targeting, and interference
consequences remain paused. Spell incantations remain separate from the core ritual vocabulary.

## Initial Playable Definitions

The first playable three-spell pool is exactly:

- `Vade Retro` (`vade_retro`): `VADE RETRO`. Forces a cursed influence away from its current
  target. Accepted authored alternatives: `VADE RETRO SATANA`, `VEID RETRO`, `VEE DE RECRO`,
  `VAD ARITORU`, `VEIDERETRO`, `VEID ARRETRO`, `VAAD RETRU`, `VAAD AR RETRO`, `VAAD RETRO`, and
  `VAD RETRO`.
  Every malformed alternative is a deliberate exact alias from an observed Development Build
  Whisper transcript, not generic fuzzy matching.
- `Pactum Sanguis` (`pactum_sanguis`): `PACTUM SANGUIS`. Marks a blood-bound bargain for a future
  betrayal effect. Accepted authored alternative: `PACTUM SANGUINE`.
- `Lux in Umbra` (`lux_in_umbra`): `LUX IN UMBRA`. Reveals a truth hidden beneath the table's
  darkness. Accepted authored alternative: `LUX UMBRA`.

These descriptions and alternatives come from the existing `SpellPhraseLibrary` authoring. A new
match grants one instance of each definition for Development testing. Later one-card refills retain
the existing server-side random selection rule. No listed effect is implemented yet.

## Approved Card Names

Common:

- Petit Fantôme

Uncommon:

- Cri du Damné
- Souffle des Ténèbres

Rare:

- Renversement du Cercle
- Page Blanche

Epic:

- Le Livre n'est pas satisfait

Ultra Rare:

- Le Hurlement du Damné
- Le Livre t'Ignore

Several spoken incantations are not finalized. Do not invent production incantations or create speculative cards such as `Pluie de Pages` without explicit approval.
