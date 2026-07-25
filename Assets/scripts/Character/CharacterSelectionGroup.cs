using System;
using System.Collections.Generic;
using Incantation.Character;
using UnityEngine;

public sealed class CharacterSelectionGroup : MonoBehaviour
{
    [Serializable]
    private sealed class Entry
    {
        [SerializeField] private string displayName;
        [SerializeField] private GameObject target;

        public string DisplayName => displayName ?? string.Empty;
        public GameObject Target => target;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    [SerializeField] private AppearanceSlot appearanceSlot = AppearanceSlot.Hair;
    [SerializeField] private int currentIndex;

    public AppearanceSlot AppearanceSlot => appearanceSlot;
    public event Action<AppearanceSlot, int, int> SelectionChanged;

    private void Awake()
    {
        ApplySelection();
    }

    public void Select(int index)
    {
        if (index < 0 || index >= entries.Count)
        {
            Debug.LogWarning($"{nameof(CharacterSelectionGroup)} cannot select entry {index}.", this);
            return;
        }

        int previousIndex = currentIndex;
        currentIndex = index;
        ApplySelection();

        if (previousIndex != currentIndex)
        {
            SelectionChanged?.Invoke(appearanceSlot, previousIndex, currentIndex);
        }
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
    }

    public string GetDisplayName(int index)
    {
        return index >= 0 && index < entries.Count && entries[index] != null
            ? entries[index].DisplayName
            : string.Empty;
    }

    public int GetEntryCount()
    {
        return entries.Count;
    }

    private void ApplySelection()
    {
        if (entries.Count == 0)
        {
            currentIndex = -1;
            return;
        }

        if (currentIndex < 0 || currentIndex >= entries.Count)
        {
            currentIndex = 0;
        }

        GameObject selectedTarget = entries[currentIndex] != null
            ? entries[currentIndex].Target
            : null;

        for (int i = 0; i < entries.Count; i++)
        {
            GameObject target = entries[i] != null ? entries[i].Target : null;

            if (target != null)
            {
                target.SetActive(target == selectedTarget);
            }
        }
    }
}
