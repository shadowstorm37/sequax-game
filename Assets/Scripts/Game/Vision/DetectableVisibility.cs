using UnityEngine;

/// <summary>
/// Attach to enemies, items, or anything else that should fade in/out with the
/// player's vision cone - as opposed to terrain, which stays dimly visible outside
/// the vision cone via DarknessOverlay.
/// </summary>
public class DetectableVisibility : MonoBehaviour
{
    [Tooltip("Left empty, this will find the first VisionConeMask in the scene at startup.")]
    [SerializeField] private VisionConeMask visionCone;
    [Tooltip("Left empty, this will use all SpriteRenderers found on this object and its children.")]
    [SerializeField] private SpriteRenderer[] renderersToToggle;
    [Tooltip("How fast alpha catches up to the target visibility, in units per second (1 = fully fade in/out in 1 second).")]
    [SerializeField] private float fadeSpeed = 4f;

    private float currentAlpha;

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

        float targetAlpha = visionCone.SampleVisibility(transform.position);
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        for (int i = 0; i < renderersToToggle.Length; i++)
        {
            SpriteRenderer renderer = renderersToToggle[i];
            Color color = renderer.color;
            color.a = currentAlpha;
            renderer.color = color;
        }
    }
}
