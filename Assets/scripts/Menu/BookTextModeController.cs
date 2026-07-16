using UnityEngine;

public sealed class BookTextModeController : MonoBehaviour
{
    [Header("Authored Text Roots")]
    [SerializeField] private GameObject menuTextRoot;
    [SerializeField] private GameObject ritualTextRoot;

    private void Awake()
    {
        ShowMenuTexts();
    }

    public void ShowMenuTexts()
    {
        if (menuTextRoot != null)
            menuTextRoot.SetActive(true);

        if (ritualTextRoot != null)
            ritualTextRoot.SetActive(false);
    }

    public void ShowRitualTexts()
    {
        if (menuTextRoot != null)
            menuTextRoot.SetActive(false);

        if (ritualTextRoot != null)
            ritualTextRoot.SetActive(true);
    }
}
