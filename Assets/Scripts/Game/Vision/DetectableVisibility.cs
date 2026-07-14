using UnityEngine;

/// <summary>
/// Attach to enemies, items, or anything else that should be completely hidden
/// until the player can actually see it - as opposed to terrain, which stays
/// dimly visible outside the vision cone via DarknessOverlay.
/// </summary>
public class DetectableVisibility : MonoBehaviour
{
    [Tooltip("Left empty, this will find the first VisionConeMask in the scene at startup.")]
    [SerializeField] private VisionConeMask visionCone;
    [Tooltip("Left empty, this will use all SpriteRenderers found on this object and its children.")]
    [SerializeField] private SpriteRenderer[] renderersToToggle;

    private void Awake()
    {
        if (visionCone == null)
        {
            visionCone = FindFirstObjectByType<VisionConeMask>();
        }

        if (renderersToToggle == null || renderersToToggle.Length == 0)
        {
            renderersToToggle = GetComponentsInChildren<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        if (visionCone == null) return;

        bool visible = visionCone.IsPointVisible(transform.position);
        for (int i = 0; i < renderersToToggle.Length; i++)
        {
            renderersToToggle[i].enabled = visible;
        }
    }
}
