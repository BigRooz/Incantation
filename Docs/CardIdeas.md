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
presentation data, and server-authoritative private hand ownership on each persistent
`NetworkPlayer`. It does not include voice activation, spell execution, targeting, rarity
probability, or effect resolution.

## Locked Hand Rules

- Each active, alive player begins a new match with exactly three authoritative card instances.
- A hand may never exceed three cards.
- At most one card is granted when that player's new authoritative ritual turn begins: zero cards
  when the hand already contains three, otherwise exactly one.
- The authoritative turn sequence prevents the same turn from refilling twice.
- `SpellUsedThisTurn` resets to false exactly once at the start of that player's new turn. Future
  successful consumption may set it true; this milestone provides no cast or consumption request.
- Card assignment and randomness belong to the server. A card instance ID is distinct from its
  reusable `SpellDefinition.DefinitionId`.
- Detailed instance and definition IDs synchronize only to the owning player.
- The physical three-card `SpellHandController` remains local presentation. Empty authoritative
  slots are hidden.
- A living player may prepare the hand only while the authoritative Book is assigned to another
  player. Assignment to the local player locks E input and automatically returns raised cards to
  their table poses.
- Eliminated players receive no refill and cannot interact with their hand. Ghost spell casting is
  not implemented.
- Match return clears the old hand; a later ritual sequence creates a fresh three-card hand with
  new monotonically allocated instance IDs.

Spell effects, targeting, spell-phrase voice validation, and interference consequences remain
paused unless a focused task explicitly activates them. Spell incantations remain separate from
the core ritual word vocabulary.

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
