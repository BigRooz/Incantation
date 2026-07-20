using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class CharacterBookPageController : MonoBehaviour
{
    [Header("Right Page")]
    [SerializeField] private BookRightPageController rightPageController;

    [Header("Character Action")]
    [SerializeField] private BookMenuController bookMenuController;

    [Header("Selection Groups")]
    [SerializeField] private CharacterSkinPalette skinPalette;
    [SerializeField] private CharacterSelectionGroup hairSelectionGroup;
    [SerializeField] private CharacterSelectionGroup beardSelectionGroup;

    [Header("Horn Placeholders")]
    [SerializeField] private string[] hornOptions = { "Horns I", "Horns II", "Horns III", "Horns IV" };

    [Header("Hat Placeholders")]
    [SerializeField] private string[] hatOptions = { "Hat I", "Hat II", "Hat III", "Hat IV" };

    [Header("Tattoo Placeholders")]
    [SerializeField] private string[] tattooOptions = { "Tattoo I", "Tattoo II", "Tattoo III", "Tattoo IV" };

    public void ShowSkin()
    {
        ShowSelectionList(
            "SKIN",
            skinPalette != null ? skinPalette.GetEntryCount : null,
            skinPalette != null ? skinPalette.GetDisplayName : null,
            skinPalette != null ? skinPalette.Select : null);
    }

    public void ShowHair()
    {
        ShowSelectionGroup("HAIR", hairSelectionGroup);
    }

    public void ShowBeards()
    {
        ShowSelectionGroup("BEARD", beardSelectionGroup);
    }

    public void ShowHorns()
    {
        ShowOptions("HORNS", hornOptions);
    }

    public void ShowHats()
    {
        ShowOptions("HATS", hatOptions);
    }

    public void ShowTattoos()
    {
        ShowOptions("TATTOOS", tattooOptions);
    }

    public void ShowCharacter()
    {
        if (bookMenuController != null)
        {
            bookMenuController.ShowCharacter();
        }
    }

    public void ResetCharacterPage()
    {
        if (rightPageController == null)
        {
            return;
        }

        rightPageController.ShowContextPage(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            ShowCharacter);
    }

    private void ShowOptions(string title, string[] options)
    {
        if (rightPageController == null)
        {
            return;
        }

        rightPageController.ShowContextPage(
            title,
            GetOption(options, 0),
            GetOption(options, 1),
            GetOption(options, 2),
            GetOption(options, 3),
            ShowCharacter);
    }

    private void ShowSelectionGroup(string title, CharacterSelectionGroup selectionGroup)
    {
        ShowSelectionList(
            title,
            selectionGroup != null ? selectionGroup.GetEntryCount : null,
            selectionGroup != null ? selectionGroup.GetDisplayName : null,
            selectionGroup != null ? selectionGroup.Select : null);
    }

    private void ShowSelectionList(
        string title,
        Func<int> getEntryCount,
        Func<int, string> getDisplayName,
        Action<int> select)
    {
        if (rightPageController == null)
        {
            return;
        }

        List<string> names = new List<string>();
        List<UnityAction> actions = new List<UnityAction>();
        int entryCount = getEntryCount != null ? getEntryCount() : 0;

        for (int i = 0; i < entryCount; i++)
        {
            int selectionIndex = i;
            names.Add(getDisplayName != null ? getDisplayName(selectionIndex) : string.Empty);
            actions.Add(select != null ? new UnityAction(() => select(selectionIndex)) : null);
        }

        rightPageController.ShowSelectionPage(title, names, actions);
    }

    private static string GetOption(string[] options, int index)
    {
        if (options == null || index < 0 || index >= options.Length)
        {
            return string.Empty;
        }

        return options[index] ?? string.Empty;
    }
}
