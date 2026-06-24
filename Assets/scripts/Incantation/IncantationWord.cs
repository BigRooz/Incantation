using System;
using UnityEngine;

[Serializable]
public class IncantationWord
{
    [SerializeField] private string text;
    [SerializeField] private bool isCompleted;

    public string Text => text;
    public bool IsCompleted => isCompleted;

    public IncantationWord(string text)
    {
        this.text = text;
        isCompleted = false;
    }

    public void MarkCompleted()
    {
        isCompleted = true;
    }
}
