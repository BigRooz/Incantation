# Project Vision

Purpose: describe the long-term creative vision for Incantation.

Questions answered here:

- What is the game trying to become?
- What player experience should all systems serve?
- What pillars define the long-term project?

This document does not contain current status, task planning, implementation architecture, or milestone history.

Read next: `Docs/GAMEPLAY_TRUTH.md` for authoritative gameplay law and `Docs/GAME_DESIGN_PILLARS.md` for design philosophy.

## Incantation

Incantation is a competitive multiplayer party game set in a dark fantasy horror atmosphere.

Players are corrupted priests seated around an ancient table. One cursed book moves between them, demanding spoken forbidden words while the group tries to survive, sabotage, and remember what just happened.

The objective is not only to win.

The objective is to create unforgettable moments between friends.

Every match should generate laughter, tension, betrayal, stress, surprise, and stories players remember after the session ends.

Dark fantasy and horror amplify those moments. They do not replace the social party-game core.

## Current Core Vision

- Players start in a lobby.
- Players ready up.
- Players sit automatically when the ritual begins.
- There is one real cursed book.
- The book moves player to player around the table.
- The ritual phrase starts with 1 word.
- All active players say the same visible phrase.
- After a full active table rotation, the phrase grows by 1 word.
- Phrase growth is rotation-based, not player-turn-based.
- `WordByWordRealtime` is the default prototype validation mode.
- `FullPhrase` is an optional strict validation mode.
- `WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.
- Whisper remains available but should not be forced as the only validation path.
- Unity Dictation and Azure are not part of the current voice plan.

## Current Prototype State

The v0.1 prototype is a local playable ritual slice.

It includes one cursed book, physical seat traversal, local debug occupants, realtime visual word absorption, wrong-word rejection feedback, hourglass timer pressure, book movement after ritual acceptance, ambient audio, fire flicker, hourglass light possession, room veil, and dark cabin ambience.

Lobby is the next major milestone.

Networking is not implemented yet.

## Mission

Create the most memorable voice-controlled multiplayer party game possible.

Every mechanic should encourage interaction between players rather than isolated gameplay.

## Pillars

### 1. Voice Is Magic

Players advance the ritual by speaking forbidden words aloud.

The player's voice is the primary interface.

### 2. Every Match Creates a Story

No two games should feel the same.

Players should leave every session with new stories to tell.

### 3. Betrayal Creates Memories

Unexpected betrayals, deception, pressure, and mistakes create the strongest moments.

### 4. Players Create the Experience

The game provides the ritual structure.

The players create the chaos.

### 5. Fun Before Realism

Gameplay always takes priority over realism.

The goal is memorable experiences.

## Current Priority

Priority 1 is the clean core ritual loop and reliable voice interaction.

Notebook, card, lore, networking, and demon systems are paused until the core voice loop works.

## Game Modes

### Last Priest Standing

Competitive multiplayer.

The last surviving priest wins, but the winner is ultimately absorbed by the book.

### Campaign

Cooperative multiplayer.

Players work together to seal the demon back inside the book using the same seated ritual foundation with different objectives.

## Design Rule

Every new feature must answer one question:

> Will players remember this moment tomorrow?

If the answer is no, the feature should be reconsidered.
