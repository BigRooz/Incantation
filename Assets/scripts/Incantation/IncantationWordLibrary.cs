using System.Collections.Generic;
using UnityEngine;

public class IncantationWordLibrary : MonoBehaviour
{
    private static readonly List<IncantationWordLibrary> activeLibraries = new List<IncantationWordLibrary>();

    private static readonly DefaultIncantationWord[] DefaultVocabulary =
    {
        new DefaultIncantationWord("mor", new[] { "more" }),
        new DefaultIncantationWord("tor", new[] { "tore" }),
        new DefaultIncantationWord("lum", new[] { "loom" }),
        new DefaultIncantationWord("nok", new[] { "knock" }),
        new DefaultIncantationWord("vek", new[] { "veck" }),
        new DefaultIncantationWord("rak", new[] { "rack" }),
        new DefaultIncantationWord("dor", new[] { "door" }),
        new DefaultIncantationWord("zul", new[] { "zool" }),
        new DefaultIncantationWord("vak", new[] { "vac" }),
        new DefaultIncantationWord("kor", new[] { "core" })
    };

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

    public IReadOnlyList<IncantationWord> GetWords()
    {
        return Words;
    }

    public IEnumerable<SpeechAliasMapping> GetSpeechAliasMappings()
    {
        Dictionary<string, HashSet<string>> aliasesByWord = new Dictionary<string, HashSet<string>>();

        AddSerializedAliasMappings(this, aliasesByWord);

        foreach (IncantationWordLibrary activeLibrary in activeLibraries)
        {
            if (activeLibrary == null || activeLibrary == this)
                continue;

            AddSerializedAliasMappings(activeLibrary, aliasesByWord);
        }

        foreach (KeyValuePair<string, HashSet<string>> wordAliases in aliasesByWord)
        {
            Debug.Log($"Library word aliases exposed: {wordAliases.Key} -> {wordAliases.Value.Count}");

            foreach (string normalizedAlias in wordAliases.Value)
                yield return new SpeechAliasMapping(normalizedAlias, wordAliases.Key);
        }
    }

    private void OnEnable()
    {
        if (!activeLibraries.Contains(this))
            activeLibraries.Add(this);
    }

    private void OnDisable()
    {
        activeLibraries.Remove(this);
    }

    private void AddSerializedAliasMappings(IncantationWordLibrary library, Dictionary<string, HashSet<string>> aliasesByWord)
    {
        if (library == null)
            return;

        foreach (IncantationWord word in library.Words)
        {
            AddSerializedAliasMappings(word, aliasesByWord);
        }
    }

    private void AddSerializedAliasMappings(IncantationWord word, Dictionary<string, HashSet<string>> aliasesByWord)
    {
        if (word == null)
            return;

        string normalizedWord = NormalizeSpeechText(word.Word);

        if (string.IsNullOrEmpty(normalizedWord))
            return;

        if (!aliasesByWord.TryGetValue(normalizedWord, out HashSet<string> aliases))
        {
            aliases = new HashSet<string>();
            aliasesByWord[normalizedWord] = aliases;
        }

        Debug.Log($"Serialized aliases visible: {normalizedWord} -> {word.SpeechAliasCount}");

        foreach (string speechAlias in word.SpeechAliases)
        {
            string normalizedAlias = NormalizeSpeechText(speechAlias);

            if (string.IsNullOrEmpty(normalizedAlias))
                continue;

            aliases.Add(normalizedAlias);
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

    [ContextMenu("Reset To Default Vocabulary")]
    private void ResetToDefaultVocabulary()
    {
        words = BuildDefaultVocabulary();
        MarkDirty();
    }

    [ContextMenu("Add Missing Default Words")]
    private void AddMissingDefaultWords()
    {
        EnsureWordsList();
        bool changed = false;

        foreach (DefaultIncantationWord defaultWord in DefaultVocabulary)
        {
            if (FindWord(defaultWord.Word) != null)
                continue;

            words.Add(defaultWord.ToIncantationWord());
            changed = true;
        }

        if (changed)
            MarkDirty();
    }

    [ContextMenu("Remove Duplicate Words")]
    private void RemoveDuplicateWords()
    {
        EnsureWordsList();

        List<IncantationWord> uniqueWords = new List<IncantationWord>();
        HashSet<string> seenWords = new HashSet<string>();
        bool changed = false;

        foreach (IncantationWord word in words)
        {
            if (word == null)
            {
                changed = true;
                continue;
            }

            string normalizedWord = NormalizeSpeechText(word.Word);

            if (string.IsNullOrEmpty(normalizedWord))
            {
                changed = true;
                continue;
            }

            if (!seenWords.Add(normalizedWord))
            {
                MergeAliasesIntoExistingWord(uniqueWords, normalizedWord, word.SpeechAliases);
                changed = true;
                continue;
            }

            List<string> uniqueAliases = GetUniqueNormalizedAliases(word.SpeechAliases);

            if (uniqueAliases.Count != word.SpeechAliases.Count || normalizedWord != word.Word)
                changed = true;

            uniqueWords.Add(new IncantationWord(normalizedWord, uniqueAliases));
        }

        if (!changed && uniqueWords.Count == words.Count)
            return;

        words = uniqueWords;
        MarkDirty();
    }

    [ContextMenu("Normalize All Words And Aliases")]
    private void NormalizeAllWordsAndAliases()
    {
        EnsureWordsList();

        List<IncantationWord> normalizedWords = new List<IncantationWord>();
        bool changed = false;

        foreach (IncantationWord word in words)
        {
            if (word == null)
            {
                changed = true;
                continue;
            }

            string normalizedWord = NormalizeSpeechText(word.Word);
            List<string> normalizedAliases = GetNormalizedAliases(word.SpeechAliases);

            if (normalizedWord != word.Word || !AliasesMatch(word.SpeechAliases, normalizedAliases))
                changed = true;

            normalizedWords.Add(new IncantationWord(normalizedWord, normalizedAliases));
        }

        if (!changed)
            return;

        words = normalizedWords;
        MarkDirty();
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

    private List<IncantationWord> BuildDefaultVocabulary()
    {
        List<IncantationWord> defaultWords = new List<IncantationWord>();

        foreach (DefaultIncantationWord defaultWord in DefaultVocabulary)
            defaultWords.Add(defaultWord.ToIncantationWord());

        return defaultWords;
    }

    private void EnsureWordsList()
    {
        if (words == null)
            words = new List<IncantationWord>();
    }

    private void MergeAliasesIntoExistingWord(List<IncantationWord> uniqueWords, string normalizedWord, IReadOnlyList<string> aliasesToMerge)
    {
        for (int i = 0; i < uniqueWords.Count; i++)
        {
            IncantationWord existingWord = uniqueWords[i];

            if (existingWord == null || NormalizeSpeechText(existingWord.Word) != normalizedWord)
                continue;

            List<string> aliases = GetUniqueNormalizedAliases(existingWord.SpeechAliases);

            foreach (string alias in GetUniqueNormalizedAliases(aliasesToMerge))
            {
                if (!ContainsNormalizedText(aliases, alias))
                    aliases.Add(alias);
            }

            uniqueWords[i] = new IncantationWord(normalizedWord, aliases);
            return;
        }
    }

    private List<string> GetNormalizedAliases(IReadOnlyList<string> aliases)
    {
        List<string> normalizedAliases = new List<string>();

        if (aliases == null)
            return normalizedAliases;

        foreach (string alias in aliases)
        {
            string normalizedAlias = NormalizeSpeechText(alias);

            if (!string.IsNullOrEmpty(normalizedAlias))
                normalizedAliases.Add(normalizedAlias);
        }

        return normalizedAliases;
    }

    private List<string> GetUniqueNormalizedAliases(IReadOnlyList<string> aliases)
    {
        List<string> uniqueAliases = new List<string>();

        foreach (string normalizedAlias in GetNormalizedAliases(aliases))
        {
            if (!ContainsNormalizedText(uniqueAliases, normalizedAlias))
                uniqueAliases.Add(normalizedAlias);
        }

        return uniqueAliases;
    }

    private bool AliasesMatch(IReadOnlyList<string> existingAliases, List<string> normalizedAliases)
    {
        if (existingAliases == null)
            return normalizedAliases.Count == 0;

        if (existingAliases.Count != normalizedAliases.Count)
            return false;

        for (int i = 0; i < existingAliases.Count; i++)
        {
            if (existingAliases[i] != normalizedAliases[i])
                return false;
        }

        return true;
    }

    private bool ContainsNormalizedText(List<string> values, string normalizedText)
    {
        foreach (string value in values)
        {
            if (NormalizeSpeechText(value) == normalizedText)
                return true;
        }

        return false;
    }

    private void MarkDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);

            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    public class SpeechAliasMapping
    {
        public string Alias { get; }
        public string Word { get; }

        public SpeechAliasMapping(string alias, string word)
        {
            Alias = alias;
            Word = word;
        }
    }

    private class DefaultIncantationWord
    {
        public string Word { get; }
        public IReadOnlyList<string> SpeechAliases { get; }

        public DefaultIncantationWord(string word, IReadOnlyList<string> speechAliases)
        {
            Word = word;
            SpeechAliases = speechAliases;
        }

        public IncantationWord ToIncantationWord()
        {
            return new IncantationWord(Word, SpeechAliases);
        }
    }
}
