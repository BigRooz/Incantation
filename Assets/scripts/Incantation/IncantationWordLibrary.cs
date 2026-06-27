using System.Collections.Generic;
using UnityEngine;

public class IncantationWordLibrary : MonoBehaviour
{
    [Header("Incantation Vocabulary")]
    [Tooltip("Single source of truth for generated incantation words and their speech recognition aliases.")]
    [SerializeField] private List<IncantationWord> words = new List<IncantationWord>
    {
        new IncantationWord("mor", new[] { "more" }),
        new IncantationWord("tor", new[] { "tore" }),
        new IncantationWord("lum", new[] { "loom" }),
        new IncantationWord("nok", new[] { "knock" }),
        new IncantationWord("vek", new[] { "veck" }),
        new IncantationWord("rak", new[] { "rack" }),
        new IncantationWord("dor", new[] { "door" }),
        new IncantationWord("zul", new[] { "zool" }),
        new IncantationWord("vak", new[] { "vac" }),
        new IncantationWord("kor", new[] { "core" })
    };

    public IReadOnlyList<IncantationWord> Words
    {
        get
        {
            if (words == null)
                words = new List<IncantationWord>();

            return words;
        }
    }

    public bool IsSpeechAliasForWord(string speechAlias, string expectedWord)
    {
        IncantationWord word = FindWord(expectedWord);
        return word != null && word.HasSpeechAlias(speechAlias);
    }

    public bool TryAddSpeechAlias(string expectedWord, string speechAlias)
    {
        IncantationWord word = FindWord(expectedWord);

        if (word == null)
            return false;

        return word.TryAddSpeechAlias(speechAlias);
    }

    [ContextMenu("Reset To Phonetic Vocabulary")]
    private void ResetToPhoneticVocabulary()
    {
        if (words == null)
            words = new List<IncantationWord>();

        words.Clear();
        AddPhoneticWord("mor", "more");
        AddPhoneticWord("tor", "tore");
        AddPhoneticWord("lum", "loom");
        AddPhoneticWord("nok", "knock");
        AddPhoneticWord("vek", "veck");
        AddPhoneticWord("rak", "rack");
        AddPhoneticWord("dor", "door");
        AddPhoneticWord("zul", "zool");
        AddPhoneticWord("vak", "vac");
        AddPhoneticWord("kor", "core");

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);

            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void AddPhoneticWord(string wordText, string speechAlias)
    {
        if (string.IsNullOrWhiteSpace(wordText) || FindWord(wordText) != null)
            return;

        words.Add(new IncantationWord(wordText, new[] { speechAlias }));
    }

    private IncantationWord FindWord(string expectedWord)
    {
        string normalizedExpectedWord = NormalizeSpeechText(expectedWord);

        if (string.IsNullOrEmpty(normalizedExpectedWord))
            return null;

        foreach (IncantationWord word in Words)
        {
            if (word == null)
                continue;

            if (NormalizeSpeechText(word.Word) == normalizedExpectedWord)
                return word;
        }

        return null;
    }

    private string NormalizeSpeechText(string speechText)
    {
        if (string.IsNullOrWhiteSpace(speechText))
            return string.Empty;

        return speechText.Trim().ToLowerInvariant();
    }
}
