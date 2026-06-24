using System;
using UnityEngine;

public class MockVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
{
    [Header("Testing")]
    [SerializeField] private string testPhrase = "ordo tenebris";

    public bool IsListening { get; private set; }

    public event Action<string> OnPhraseRecognized;

    private void Update()
    {
        if (!IsListening)
            return;

        if (Input.GetKeyDown(KeyCode.Return))
            OnPhraseRecognized?.Invoke(testPhrase);
    }

    public void StartListening()
    {
        IsListening = true;
    }

    public void StopListening()
    {
        IsListening = false;
    }
}
