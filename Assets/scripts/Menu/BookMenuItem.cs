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
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Color hoverColor = Color.white;
    [SerializeField, Min(1f)] private float hoverScale = 1.05f;
    [SerializeField] private bool enableHoverDebugLogs;
    [SerializeField] private UnityEvent onClick = new UnityEvent();

    private Color authoredColor;
    private Vector3 authoredScale;
    private bool hasAuthoredBaseline;
    private bool isHovered;
    private bool interactionEnabled;

    public bool InteractionEnabled => interactionEnabled;

    public void SetOnClickAction(UnityAction action)
    {
        onClick = new UnityEvent();

        if (action != null)
        {
            onClick.AddListener(action);
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (!enabled)
        {
            isHovered = false;
            RestoreAuthoredVisual();
        }
        else
        {
            RestoreAuthoredVisual();

            if (isHovered)
            {
                ApplyHoverVisual();
            }
        }

        if (interactionCollider != null)
        {
            interactionCollider.enabled = enabled;
        }

        LogHoverState($"{nameof(SetInteractionEnabled)}({enabled})");
    }

    public void RefreshVisualBaseline()
    {
        RestoreAuthoredVisual();

        if (isHovered && interactionEnabled)
        {
            ApplyHoverVisual();
        }

        LogHoverState(nameof(RefreshVisualBaseline));
    }

    public void CaptureAuthoredBaseline()
    {
        if (text == null)
        {
            return;
        }

        bool wasHovered = isHovered;

        if (wasHovered && hasAuthoredBaseline)
        {
            RestoreAuthoredVisual();
        }

        authoredColor = text.color;
        authoredScale = text.transform.localScale;
        hasAuthoredBaseline = true;

        if (wasHovered && interactionEnabled)
        {
            ApplyHoverVisual();
        }
    }

    private void Awake()
    {
        if (interactionCollider == null)
        {
            interactionCollider = GetComponent<Collider>();
        }

        interactionEnabled = interactionCollider == null || interactionCollider.enabled;
        CaptureAuthoredBaseline();
    }

    private void OnDisable()
    {
        isHovered = false;
        RestoreAuthoredVisual();
    }

    private void OnMouseEnter()
    {
        LogHoverState(nameof(OnMouseEnter));

        if (!interactionEnabled)
        {
            return;
        }

        if (!hasAuthoredBaseline)
        {
            return;
        }

        isHovered = true;
        ApplyHoverVisual();
    }

    private void OnMouseExit()
    {
        LogHoverState(nameof(OnMouseExit));
        isHovered = false;
        RestoreAuthoredVisual();
    }

    private void OnMouseDown()
    {
        LogHoverState(nameof(OnMouseDown));

        if (!interactionEnabled)
        {
            return;
        }

        onClick.Invoke();
    }

    private void ApplyHoverVisual()
    {
        if (!hasAuthoredBaseline || text == null)
        {
            return;
        }

        text.color = hoverColor;
        text.transform.localScale = authoredScale * hoverScale;
    }

    private void RestoreAuthoredVisual()
    {
        if (!hasAuthoredBaseline || text == null)
        {
            return;
        }

        text.color = authoredColor;
        text.transform.localScale = authoredScale;
    }

    private void LogHoverState(string source)
    {
        if (!enableHoverDebugLogs)
        {
            return;
        }

        bool colliderEnabled = interactionCollider != null && interactionCollider.enabled;
        string currentText = text != null ? text.text : "<unassigned>";
        Vector3 currentScale = text != null ? text.transform.localScale : Vector3.zero;
        Color currentColor = text != null ? text.color : Color.clear;

        Debug.Log(
            $"{nameof(BookMenuItem)}.{source} | GameObject={gameObject.name} | ColliderEnabled={colliderEnabled} | InteractionEnabled={interactionEnabled} | Text=\"{currentText}\" | Scale={currentScale} | Color={currentColor}",
            this);
    }
}
