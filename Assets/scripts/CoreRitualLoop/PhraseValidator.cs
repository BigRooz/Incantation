using UnityEngine;

/// <summary>
/// Future owner of comparing the expected ritual phrase against recognized speech text.
/// </summary>
public class PhraseValidator : MonoBehaviour
{
    /*
     * Responsibility:
     * - Receive an expected phrase.
     * - Receive a recognized phrase.
     * - Return whether the recognized phrase satisfies the expected ritual phrase.
     *
     * TODO:
     * - Define normalization rules for full visible phrase matching.
     * - Support ritual word aliases without touching SpellPhraseLibrary.
     * - Return validation details once retry and failure feedback are migrated.
     */

    public bool ValidatePhrase(string expectedPhrase, string recognizedPhrase)
    {
        // TODO: Future migration will implement full visible phrase matching.
        return false;
    }
}
