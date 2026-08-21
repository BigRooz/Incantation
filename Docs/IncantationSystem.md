# Incantation System

Purpose: define the spoken ritual phrase system and its boundaries.

Questions answered here:

- Who owns ritual word vocabulary and phrase state?
- How do validation modes affect phrase acceptance?
- What should the incantation system not own?

This document does not contain current task planning, full voice implementation details, book movement ownership, or elimination rules.

Read next: `Docs/VoiceMagicSystem.md` for voice-recognition boundaries or `Docs/CoreRitualLoopArchitecture.md` for ritual coordination.

## Purpose

The Incantation System is the source of truth for the spoken ritual phrase.

It owns:

- The ritual word vocabulary.
- The current shared ritual phrase.
- Which words are currently visible.
- Word-by-word acceptance state for the current phrase.
- Validation of recognized candidates against the current phrase.
- Events that other systems can react to.

It does not own UI, microphone capture, networking, timers, book movement, demon behavior, player elimination, cards, notebook logic, or lore delivery.

## Current Ritual Rule

- The phrase starts with 1 word.
- All active players say the same visible phrase.
- The book moves from active seat to active seat.
- After the book completes a full active table rotation, 1 word is added to the shared phrase.
- The active player must satisfy the selected validation mode before the hourglass runs out.

At new-match initialization, the authoritative `GrowingIncantationManager` creates one runtime
Fisher-Yates shuffle of its canonical ritual vocabulary. Phrase growth consumes that order
sequentially without replacement. When the complete vocabulary has been consumed, it appends a
new shuffled cycle and prevents the last word of the previous cycle from immediately repeating
as the first word of the next cycle. In a network ritual only the Host/server performs this
shuffle; clients display the synchronized authoritative phrase and never choose phrase words.

The canonical 15-word ritual vocabulary is: `UMBRA`, `VAKOR`, `MORTIS`, `NOCTIS`, `KORVA`,
`VELUM`, `ZULAK`, `DORIM`, `RAKAN`, `NETHOR`, `INIS`, `VEXOR`, `MALUM`, `LUXOR`, and
`TENEBRIS`. `INIS` replaces the former canonical `IGNIS`. Useful recognizer outputs `ignis`,
`igneous`, `ignice`, `inice`, `inis`, and `enis` are aliases that normalize to `INIS`; they are
not additional canonical words. `SpellPhraseLibrary` remains a separate spell/card vocabulary.

This replaces older notes where every individual turn added a new word. Word growth is rotation-based, not player-turn-based.

## Voice Recognition Direction

The current prototype supports two validation modes:

- `WordByWordRealtime`: default prototype mode. Recognized keywords are validated immediately against the next expected word, allowing realtime visual word absorption and wrong-word rejection feedback.
- `FullPhrase`: optional strict mode. A full transcript is validated against the full visible phrase.

`WindowsKeywordVoiceRecognizer` is currently preferred for realtime prototype gameplay.

Whisper is kept for full-phrase or experimental recognition paths, but it must not be forced as the only validation path.

MainGame preloads its single `WhisperManager` during scene startup. A local Book turn cannot begin a recording until that manager reports ready; preload failure remains visible in the Development overlay. Once ready, one recording is tagged with the authoritative ritual sequence, turn sequence, and local player identity. `MicrophoneRecord.StartRecord()` begins when the local Book enters LISTENING. Silence before speech does nothing. VAD only observes whether speech occurred and when the configured `0.8` seconds of trailing silence elapsed; it never gates or trims capture. That exact timer also drives the existing Book menu-hover highlight through its independent voice request: full while voice is detected, linearly decaying through silence, restored without restarting capture when voice resumes, and zero before transcription starts. `OnRecordStop` passes that exact complete `AudioChunk` once to `WhisperManager.GetTextAsync`, matching the proven Sandbox lifecycle. Timeout, turn loss, reset, failure, game over, lobby return, or teardown invalidates the minimal session identity and discards any late result.

There is no production `WhisperStream`, fragment resolver, per-word identity, local expected-word advancement, or per-word gameplay validation. While speech is active the Book only strengthens its listening response. After recording stops the Book enters TRANSCRIBING/JUDGING. `VoicePhraseNormalizer` resolves canonical ritual words and explicitly configured aliases across the complete transcript; unresolved, missing, extra, or incorrect words remain available for authoritative full-phrase validation without fuzzy matching.

The Living Book has two deliberately separate meanings. Its local renderer glow communicates only that the current microphone recording hears speech or is counting down the same trailing-silence window. It remains off throughout transcription and authority wait. Word colors are reserved for the authoritative verdict: muted magical green means correct and deep crimson means wrong. Only an authoritative full-phrase result starts the sequential word replay.

Before authority answers, every phrase word remains neutral. Accepted words receive a short interruptible brighten/scale acknowledgement before settling into the completed color. Rejection holds the whole failed word red without shake, scaling, or other movement before revealing reset progress. Remote observers see the same replicated acceptance/rejection; the Book listening glow remains local.

Network phrase-state synchronization updates authoritative words and progress without clearing transient verdict presentation. Repeated or changed snapshots cannot cancel an acknowledgement already in flight. A turn-sequence change explicitly clears all transient color, scale, and replay state, while rejected retry cleanup occurs only after the matching replay reaches `CompleteReplay()`.

The complete transcript is split into textual tokens in spoken order. `VoicePhraseNormalizer.TryBankCompleteAttempt` performs only lowercase/punctuation cleanup and exact lookup against the serialized canonical word plus accepted forms in `IncantationWordLibrary`. Unknown tokens remain uppercase in their original position; missing tokens are never invented and extra tokens are never removed. The resulting BANKED phrase produces exactly one `RequestCompleteWhisperRitualAttempt` call. `NetworkRitualAuthority` validates it against its own complete phrase and publishes the accepted-word count plus first failed index. The complete authoritative word-result timeline is queued atomically before `IncantationTextDisplay` starts playback. Correct words then accumulate in muted magical green at a serialized `0.11`-second interval. Success leaves the complete green verdict visible for the existing single Book pulse, then server-owned turn advancement clears it. Rejection preserves only green confirmations before the failed index, holds that expected word in deep crimson for `0.5` seconds without movement, and reaches `CompleteReplay()` before the controller resets all phrase visuals and records a fresh complete attempt while the same Timer continues. Tokenless input retains its independent retry path because it has no authoritative verdict replay.

Every authoritative local turn and Match 2 start clears the previous recording, transcript, verdict, replay, and attempt lock. Session/ritual/turn/player correlation plus server phase checks prevent late Whisper results from affecting another turn. The Windows keyword path remains separate and retains its existing realtime contracts.

Unity Dictation and Azure are not part of the current implementation plan and must not be reintroduced.

## Architecture

`IncantationWord` is a serializable data object containing visible ritual word text and optional speech aliases.

`IncantationWordLibrary` is the vocabulary source for generated ritual phrases and ritual speech aliases.

`IncantationManager` should remain focused on ritual phrase state, word acceptance, and validation. Other systems may subscribe to its events, but they should not duplicate the rules for phrase order, phrase growth, correctness, or completion.

`VoicePhraseNormalizer` may build alias lookup from the ritual word library so adding or changing a ritual word affects recognition normalization in one place.

Offline, `IncantationManager` evaluates and applies candidates through `PhraseValidator`. During
a network ritual, `NetworkRitualAuthority` is the only phrase judge: it reads the server
`IncantationManager` phrase, uses the same pure evaluation methods, and publishes an immutable
verdict. Each peer's `IncantationManager` applies that verdict for progress and presentation
without independently comparing the submitted speech.

## Spell Phrase Boundary

`SpellPhraseLibrary` is separate from ritual words.

Do not merge spell/card phrases into the ritual phrase vocabulary unless explicitly requested. Spell phrases belong to future spell, card, or interference systems. Ritual words belong to the core book-and-hourglass loop.

## Paused Integrations

Notebook, card, lore, and demon reaction integrations are paused until the core ritual loop and voice interaction work reliably.

`WhisperSandbox` is a sandbox/reference area and should not be modified during core ritual work unless explicitly requested.
