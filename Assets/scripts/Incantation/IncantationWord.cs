using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class IncantationWord
{
    [SerializeField] private string word;
    [SerializeField] private List<string> speechAliases = new List<string>();
    [SerializeField] private bool isCompleted;

    public string Word => word;
    public string Text => word;
    public IReadOnlyList<string> SpeechAliases
    {
        get
        {
            if (speechAliases == null)
                speechAliases = new List<string>();

            return speechAliases;
        }
    }

    public bool IsCompleted => isCompleted;

    public IncantationWord()
    {
        word = string.Empty;
        speechAliases = new List<string>();
        isCompleted = false;
    }

    public IncantationWord(string word)
    {
        this.word = word;
        speechAliases = new List<string>();
        isCompleted = false;
    }

    public IncantationWord(string word, IEnumerable<string> speechAliases)
    {
        this.word = word;
        this.speechAliases = new List<string>();

        if (speechAliases != null)
            this.speechAliases.AddRange(speechAliases);

        isCompleted = false;
    }

    public void MarkCompleted()
    {
        isCompleted = true;
    }
}
