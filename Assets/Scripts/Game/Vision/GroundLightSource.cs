using UnityEngine;

/// <summary>
/// A small always-visible glow that reveals darkness around itself, independent of the
/// player's facing/vision cone. Used by the phone once thrown and landed - lets the player
/// see (and see enemies within) a small area around it without holding it.
///
/// Deliberately simple: no obstacle occlusion, no cone, no baked texture - just a radial
/// falloff evaluated directly on the CPU (VisionConeMask.SampleVisibility, for enemy fade)
/// and in the desaturate shader (screen darkening). Only one instance is tracked; if more
/// ever exist simultaneously, the most recently enabled one wins - acceptable since only the
/// phone uses this today.
/// </summary>
public class GroundLightSource : MonoBehaviour
{
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float fadeWidth = 1f;

    private static readonly int ActiveId = Shader.PropertyToID("_ExtraVisionActive");
    private static readonly int OriginId = Shader.PropertyToID("_ExtraVisionOrigin");
    private static readonly int RadiusId = Shader.PropertyToID("_ExtraVisionRadius");
    private static readonly int FadeId = Shader.PropertyToID("_ExtraVisionFade");

    private static GroundLightSource activeInstance;

    public void Initialize(float newRadius, float newFadeWidth)
    {
        radius = newRadius;
        fadeWidth = newFadeWidth;
    }

    private void OnEnable()
    {
        activeInstance = this;
    }

    private void OnDisable()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
            Shader.SetGlobalFloat(ActiveId, 0f);
        }
    }

    private void LateUpdate()
    {
        Shader.SetGlobalFloat(ActiveId, 1f);
        Shader.SetGlobalVector(OriginId, transform.position);
        Shader.SetGlobalFloat(RadiusId, radius);
        Shader.SetGlobalFloat(FadeId, Mathf.Max(fadeWidth, 0.001f));
    }

    /// <summary>
    /// 0-1 visibility contribution from the active ground light at worldPoint, for CPU-side
    /// fade (see VisionConeMask.SampleVisibility). 0 if no light is currently active.
    /// </summary>
    public static float SampleVisibility(Vector2 worldPoint)
    {
        if (activeInstance == null) return 0f;

        float dist = Vector2.Distance(activeInstance.transform.position, worldPoint);
        float safeFade = Mathf.Max(activeInstance.fadeWidth, 0.001f);
        return 1f - Mathf.Clamp01((dist - (activeInstance.radius - safeFade)) / safeFade);
    }
}
