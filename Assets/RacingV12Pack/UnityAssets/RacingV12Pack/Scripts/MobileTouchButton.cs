using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles continuous press-and-hold touch input for on-screen mobile racing controls.
/// Supports pointer down, up, and exit events for rock-solid touch responsiveness on Android.
/// </summary>
public class MobileTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("Visual scale reduction when the button is held")]
    [SerializeField] private float pressedScale = 0.92f;

    public bool IsPressed { get; private set; }

    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    private void OnDisable()
    {
        IsPressed = false;
        transform.localScale = originalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        transform.localScale = originalScale * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsPressed = false;
        transform.localScale = originalScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsPressed = false;
        transform.localScale = originalScale;
    }
}
