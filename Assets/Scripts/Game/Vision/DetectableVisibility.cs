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

    // Each renderer's own alpha at registration. Cone visibility scales this rather than
    // replacing it, so a deliberately translucent renderer - a shadow silhouette, say - keeps
    // its tint instead of being flattened to fully opaque whenever the player looks at it.
    // Anything sitting at alpha 1 (everything before shadows existed) is unaffected.
    private float[] baseAlphas;

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

        baseAlphas = new float[renderersToToggle.Length];
        for (int i = 0; i < renderersToToggle.Length; i++)
        {
            baseAlphas[i] = renderersToToggle[i] != null ? renderersToToggle[i].color.a : 1f;
        }
    }

    /// <summary>
    /// Adds a renderer created after Awake (the directional sprite components build theirs at
    /// runtime) so it fades with the cone like the rest. Appends rather than rescanning, so a
    /// hand-picked list in the Inspector is not silently thrown away.
    /// </summary>
    public void RegisterRenderer(SpriteRenderer renderer)
    {
        if (renderer == null) return;

        if (renderersToToggle == null)
        {
            renderersToToggle = new[] { renderer };
            baseAlphas = new[] { renderer.color.a };
            return;
        }

        for (int i = 0; i < renderersToToggle.Length; i++)
        {
            if (renderersToToggle[i] == renderer) return;
        }

        var grown = new SpriteRenderer[renderersToToggle.Length + 1];
        renderersToToggle.CopyTo(grown, 0);
        grown[renderersToToggle.Length] = renderer;

        var grownAlphas = new float[grown.Length];
        if (baseAlphas != null) baseAlphas.CopyTo(grownAlphas, 0);
        grownAlphas[renderersToToggle.Length] = renderer.color.a;

        renderersToToggle = grown;
        baseAlphas = grownAlphas;
    }

    private void LateUpdate()
    {
        if (visionCone == null) return;

        float targetAlpha = visionCone.SampleVisibility(transform.position);
        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        for (int i = 0; i < renderersToToggle.Length; i++)
        {
            SpriteRenderer renderer = renderersToToggle[i];
            if (renderer == null) continue;

            Color color = renderer.color;
            color.a = currentAlpha * (baseAlphas != null && i < baseAlphas.Length ? baseAlphas[i] : 1f);
            renderer.color = color;
        }
    }
}
