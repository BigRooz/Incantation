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
    [SerializeField] private CharacterSelectionGroup hairSelectionGroup;

    [Header("Color Placeholders")]
    [SerializeField] private string[] colorOptions = { "Color I", "Color II", "Color III", "Color IV" };

    [Header("Horn Placeholders")]
    [SerializeField] private string[] hornOptions = { "Horns I", "Horns II", "Horns III", "Horns IV" };

    [Header("Hat Placeholders")]
    [SerializeField] private string[] hatOptions = { "Hat I", "Hat II", "Hat III", "Hat IV" };

    [Header("Tattoo Placeholders")]
    [SerializeField] private string[] tattooOptions = { "Tattoo I", "Tattoo II", "Tattoo III", "Tattoo IV" };

    public void ShowColors()
    {
        ShowOptions("COLORS", colorOptions);
    }

    public void ShowHair()
    {
        if (rightPageController == null)
        {
            return;
        }

        List<string> names = new List<string>();
        List<UnityAction> actions = new List<UnityAction>();
        int entryCount = hairSelectionGroup != null ? hairSelectionGroup.GetEntryCount() : 0;

        for (int i = 0; i < entryCount; i++)
        {
            int selectionIndex = i;
            names.Add(hairSelectionGroup.GetDisplayName(selectionIndex));
            actions.Add(() => hairSelectionGroup.Select(selectionIndex));
        }

        rightPageController.ShowSelectionPage("HAIR", names, actions);
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
            "CHARACTER",
            "Select Color",
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

    private static string GetOption(string[] options, int index)
    {
        if (options == null || index < 0 || index >= options.Length)
        {
            return string.Empty;
        }

        return options[index] ?? string.Empty;
    }
}
