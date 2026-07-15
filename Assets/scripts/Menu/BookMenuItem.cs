using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Adds hover and click interaction to a manually placed TextMeshPro menu entry.
/// Depends on an Inspector-assigned TextMeshPro component and a collider for mouse events.
/// Menu actions remain owned by listeners configured through the On Click event.
/// </summary>
public sealed class BookMenuItem : MonoBehaviour
{
    [SerializeField] private TextMeshPro text;
    [SerializeField] private Color hoverColor = Color.white;
    [SerializeField, Min(1f)] private float hoverScale = 1.05f;
    [SerializeField] private UnityEvent onClick = new UnityEvent();

    private Color originalColor;
    private Vector3 originalScale;
    private bool hasOriginalState;

    private void Awake()
    {
        CaptureOriginalState();
    }

    private void OnDisable()
    {
        RestoreOriginalState();
    }

    private void OnMouseEnter()
    {
        if (!hasOriginalState)
        {
            CaptureOriginalState();
        }

        if (!hasOriginalState)
        {
            return;
        }

        text.color = hoverColor;
        text.transform.localScale = originalScale * hoverScale;
    }

    private void OnMouseExit()
    {
        RestoreOriginalState();
    }

    private void OnMouseDown()
    {
        onClick.Invoke();
    }

    private void CaptureOriginalState()
    {
        if (text == null)
        {
            return;
        }

        originalColor = text.color;
        originalScale = text.transform.localScale;
        hasOriginalState = true;
    }

    private void RestoreOriginalState()
    {
        if (!hasOriginalState || text == null)
        {
            return;
        }

        text.color = originalColor;
        text.transform.localScale = originalScale;
    }
}
