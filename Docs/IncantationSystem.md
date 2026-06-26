# Incantation System

## Purpose

The Incantation System is the single source of truth for every spoken word in the current ritual phrase.

It owns:

- The incantation vocabulary library.
- The generated incantation.
- The current word index.
- Which words have been completed.
- Validation of the currently spoken word.
- Events that other systems can react to.

It does not own UI, voice recognition, networking, timers, ritual progression, demon behavior or player elimination.

## Architecture

`IncantationWord` is a serializable data object containing the visible word text, optional speech aliases and whether that word has been completed in the current phrase.

`IncantationWordLibrary` is the vocabulary source for generated incantations and speech recognition aliases.

`IncantationManager` is a focused MonoBehaviour that reads from `IncantationWordLibrary`, generates a unique random incantation and validates spoken input against the current word. This keeps the rule for spoken-word progress in one place while allowing other systems to subscribe through UnityEvents.

`VoicePhraseNormalizer` builds its alias lookup from `IncantationWordLibrary`, so adding or changing a word affects both generated incantations and speech normalization from the same data.

This architecture was chosen because the incantation will be touched by many future systems, but none of those systems should duplicate the rules for word order, correctness or completion. The book can display the current state, voice recognition can submit phrases, the ritual can respond to completion, and demon reactions can listen for correct or incorrect attempts without owning the incantation rules.

## Future RitualController Integration

`RitualController` should later hold a serialized reference to `IncantationManager`.

When a player's turn begins, `RitualController` can call `GenerateIncantation()` or another future method that adds/progresses ritual words depending on the final turn design. During the turn, it can wait for `OnIncantationCompleted` before advancing the ritual or moving the book.

The important boundary is that `RitualController` decides when a ritual phase starts and ends, while `IncantationManager` decides whether the spoken words are correct.

## Future MockVoiceRecognizer Integration

`MockVoiceRecognizer` already raises `OnPhraseRecognized` with recognized text.

A later connector can subscribe to that event and call:

```csharp
incantationManager.TryCompleteCurrentWord(recognizedPhrase);
```

For the current prototype this means pressing Return can simulate a recognized word or phrase. Real voice recognition can use the same integration path later, keeping recognition separate from validation.
