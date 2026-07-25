using System;
using System.Collections.Generic;
using Incantation.Character;
using UnityEngine;

/// <summary>
/// Applies appearance data to the existing local character customization components.
/// This component contains no networking and never owns authoritative appearance state.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterAppearancePresentation : MonoBehaviour
{
    private readonly Dictionary<AppearanceSlot, CharacterSelectionGroup> selectionGroups = new();
    private CharacterSkinPalette skinPalette;
    private bool isApplyingSynchronizedValue;

    public event Action<AppearanceSlot, int> LocalSelectionChanged;

    public void Initialize()
    {
        Unsubscribe();
        selectionGroups.Clear();

        skinPalette = GetComponentInChildren<CharacterSkinPalette>(true);
        if (skinPalette != null)
        {
            skinPalette.SelectionChanged += HandleSkinSelectionChanged;
        }

        foreach (CharacterSelectionGroup group in GetComponentsInChildren<CharacterSelectionGroup>(true))
        {
            if (group == null)
            {
                continue;
            }

            selectionGroups[group.AppearanceSlot] = group;
            group.SelectionChanged += HandleGroupSelectionChanged;
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void Apply(AppearanceSlotValue value)
    {
        isApplyingSynchronizedValue = true;

        if (value.Slot == AppearanceSlot.Skin)
        {
            if (skinPalette != null)
            {
                skinPalette.Select(value.ValueId);
            }
        }
        else if (selectionGroups.TryGetValue(value.Slot, out CharacterSelectionGroup group))
        {
            group.Select(value.ValueId);
        }

        isApplyingSynchronizedValue = false;
    }

    public IEnumerable<AppearanceSlotValue> CaptureSupportedAppearance()
    {
        if (skinPalette != null && skinPalette.GetCurrentIndex() >= 0)
        {
            yield return new AppearanceSlotValue(AppearanceSlot.Skin, skinPalette.GetCurrentIndex());
        }

        foreach (KeyValuePair<AppearanceSlot, CharacterSelectionGroup> pair in selectionGroups)
        {
            int valueId = pair.Value.GetCurrentIndex();
            if (valueId >= 0)
            {
                yield return new AppearanceSlotValue(pair.Key, valueId);
            }
        }
    }

    private void HandleSkinSelectionChanged(int previousValue, int currentValue)
    {
        PublishLocalSelection(AppearanceSlot.Skin, currentValue);
    }

    private void HandleGroupSelectionChanged(
        AppearanceSlot slot,
        int previousValue,
        int currentValue)
    {
        PublishLocalSelection(slot, currentValue);
    }

    private void PublishLocalSelection(AppearanceSlot slot, int valueId)
    {
        if (!isApplyingSynchronizedValue)
        {
            LocalSelectionChanged?.Invoke(slot, valueId);
        }
    }

    private void Unsubscribe()
    {
        if (skinPalette != null)
        {
            skinPalette.SelectionChanged -= HandleSkinSelectionChanged;
        }

        foreach (CharacterSelectionGroup group in selectionGroups.Values)
        {
            if (group != null)
            {
                group.SelectionChanged -= HandleGroupSelectionChanged;
            }
        }

        skinPalette = null;
    }
}
