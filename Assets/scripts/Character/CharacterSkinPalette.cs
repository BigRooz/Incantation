using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterSkinPalette : MonoBehaviour
{
    [Serializable]
    private sealed class SkinEntry
    {
        [SerializeField] private string displayName;
        [SerializeField] private Material material;

        public string DisplayName => displayName ?? string.Empty;
        public Material Material => material;
    }

    [Header("Skin Renderers")]
    [SerializeField] private List<Renderer> skinRenderers = new List<Renderer>();

    [Header("Skin Entries")]
    [SerializeField] private List<SkinEntry> skinEntries = new List<SkinEntry>();

    [SerializeField] private int currentIndex;

    public event Action<int, int> SelectionChanged;

    private void Awake()
    {
        ApplySelection();
    }

    public void Select(int index)
    {
        if (index < 0 || index >= skinEntries.Count)
        {
            return;
        }

        int previousIndex = currentIndex;
        currentIndex = index;
        ApplySelection();

        if (previousIndex != currentIndex)
        {
            SelectionChanged?.Invoke(previousIndex, currentIndex);
        }
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    public int GetEntryCount()
    {
        return skinEntries.Count;
    }

    public string GetDisplayName(int index)
    {
        return index >= 0 && index < skinEntries.Count && skinEntries[index] != null
            ? skinEntries[index].DisplayName
            : string.Empty;
    }

    private void ApplySelection()
    {
        if (skinEntries.Count == 0)
        {
            currentIndex = -1;
            return;
        }

        if (currentIndex < 0 || currentIndex >= skinEntries.Count)
        {
            currentIndex = 0;
        }

        if (skinEntries[currentIndex] == null)
        {
            return;
        }

        Material selectedMaterial = skinEntries[currentIndex].Material;

        for (int i = 0; i < skinRenderers.Count; i++)
        {
            Renderer skinRenderer = skinRenderers[i];
            if (skinRenderer == null)
            {
                continue;
            }

            skinRenderer.sharedMaterial = selectedMaterial;
        }
    }
}
