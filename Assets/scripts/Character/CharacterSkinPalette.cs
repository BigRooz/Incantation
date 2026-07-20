using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterSkinPalette : MonoBehaviour
{
    [Serializable]
    private sealed class SkinEntry
    {
        [SerializeField] private string displayName;
        [SerializeField] private Texture2D texture;

        public string DisplayName => displayName ?? string.Empty;
        public Texture2D Texture => texture;
    }

    [Header("Skin Renderers")]
    [SerializeField] private List<Renderer> skinRenderers = new List<Renderer>();

    [Header("Skin Entries")]
    [SerializeField] private List<SkinEntry> skinEntries = new List<SkinEntry>();

    [Header("Shader")]
    [SerializeField] private string texturePropertyName = "_BaseMap";

    [SerializeField] private int currentIndex;

    private MaterialPropertyBlock propertyBlock;
    private int texturePropertyId;

    private void Awake()
    {
        RefreshTexturePropertyId();
        ApplySelection();
    }

    public void Select(int index)
    {
        if (index < 0 || index >= skinEntries.Count)
        {
            return;
        }

        currentIndex = index;
        ApplySelection();
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

        if (skinEntries[currentIndex] == null || string.IsNullOrWhiteSpace(texturePropertyName))
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Texture2D selectedTexture = skinEntries[currentIndex].Texture;

        for (int i = 0; i < skinRenderers.Count; i++)
        {
            Renderer skinRenderer = skinRenderers[i];
            if (skinRenderer == null)
            {
                continue;
            }

            skinRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(texturePropertyId, selectedTexture);
            skinRenderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }
    }

    private void RefreshTexturePropertyId()
    {
        texturePropertyId = Shader.PropertyToID(texturePropertyName ?? string.Empty);
    }

    private void OnValidate()
    {
        RefreshTexturePropertyId();
    }
}
