using UnityEngine;
using Game.Items;

/// <summary>
/// Shows a screen-space arrow pointing at the car as soon as the player can escape
/// (car keys, or all three hotwire parts — see Inventory.CanEscape).
/// </summary>
public class CarDirectionArrow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Inventory playerInventory;
    [SerializeField] private Transform carTarget;
    [SerializeField] private Camera playerCamera;
    [Tooltip("The RectTransform of the arrow icon itself (child of the canvas). Sprite should point UP by default.")]
    [SerializeField] private RectTransform arrowRectTransform;

    [Header("Screen Edge Behavior")]
    [Tooltip("Pixels kept clear between the arrow and the edge of the screen.")]
    [SerializeField] private float screenEdgeMargin = 80f;

    private bool isUnlocked = false;

    private void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnKeyItemAdded += HandleKeyItemAdded;
        }
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnKeyItemAdded -= HandleKeyItemAdded;
        }
    }

    private void Start()
    {
        if (arrowRectTransform != null)
        {
            arrowRectTransform.gameObject.SetActive(false);
        }

        // Covers the case where the player already has everything needed when this loads.
        if (playerInventory != null && playerInventory.CanEscape)
        {
            ShowArrow();
        }
    }

    private void Update()
    {
        if (!isUnlocked || arrowRectTransform == null || carTarget == null || playerCamera == null) return;

        UpdateArrowTransform();
    }

    private void HandleKeyItemAdded(ItemId _)
    {
        if (isUnlocked || playerInventory == null) return;

        if (playerInventory.CanEscape)
        {
            ShowArrow();
        }
    }

    private void ShowArrow()
    {
        isUnlocked = true;

        if (arrowRectTransform != null)
        {
            arrowRectTransform.gameObject.SetActive(true);
        }
    }

    private void UpdateArrowTransform()
    {
        Vector3 screenPos = playerCamera.WorldToScreenPoint(carTarget.position);
        bool isBehindCamera = screenPos.z < 0f;

        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        Vector3 fromCenter = screenPos - screenCenter;

        // If the target is behind us, the projected point flips to the wrong side, so mirror it back.
        if (isBehindCamera)
        {
            fromCenter = -fromCenter;
        }

        float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
        arrowRectTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        // Clamp the arrow's position to stay just inside the screen bounds.
        float halfWidth = screenCenter.x - screenEdgeMargin;
        float halfHeight = screenCenter.y - screenEdgeMargin;

        float slope = fromCenter.y / fromCenter.x;
        Vector2 clampedOffset = fromCenter;

        if (Mathf.Abs(fromCenter.x) > 0.001f)
        {
            if (Mathf.Abs(halfHeight / slope) < halfWidth)
            {
                clampedOffset.x = halfHeight / Mathf.Abs(slope) * Mathf.Sign(fromCenter.x);
                clampedOffset.y = halfHeight * Mathf.Sign(fromCenter.y);
            }
            else
            {
                clampedOffset.x = halfWidth * Mathf.Sign(fromCenter.x);
                clampedOffset.y = halfWidth * Mathf.Abs(slope) * Mathf.Sign(fromCenter.y);
            }
        }

        arrowRectTransform.position = screenCenter + (Vector3)clampedOffset;
    }
}
