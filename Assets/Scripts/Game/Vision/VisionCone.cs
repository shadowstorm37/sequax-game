using UnityEngine;

/// <summary>
/// Generates a Sprite (circle + forward-facing cone) at runtime and feeds it
/// into a SpriteMask. The shape is baked once as a texture; at runtime we
/// just rotate this GameObject's transform to point the cone toward the
/// player's facing direction, which is cheap compared to regenerating the
/// texture every frame.
///
/// SETUP:
/// 1. Create an empty child GameObject under Player, name it "VisionMask".
/// 2. Reset its local position to (0,0,0) - it must sit exactly on the player.
/// 3. Add this script to it (it auto-adds a SpriteMask component).
/// </summary>
[RequireComponent(typeof(SpriteMask))]
public class VisionConeMask : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Total width of the cone in degrees (e.g. 45 = 22.5 degrees either side of facing direction).")]
    [SerializeField] private float coneAngleDegrees = 45f;
    [Tooltip("How far the cone reaches, in world units.")]
    [SerializeField] private float coneRadius = 6f;
    [Tooltip("Radius of the small always-visible circle around the player, in world units.")]
    [SerializeField] private float circleRadius = 1.5f;

    [Header("Texture Quality")]
    [Tooltip("Resolution of the generated mask texture. Higher = smoother edges, more expensive to generate (only happens once).")]
    [SerializeField] private int textureResolution = 256;
    [Tooltip("Soft edge width in pixels, to avoid a hard jagged cutoff. 0 = fully hard edge.")]
    [SerializeField] private float featherPixels = 2f;

    private SpriteMask spriteMask;
    private PlayerScript player;

    private void Awake()
    {
        spriteMask = GetComponent<SpriteMask>();
        player = GetComponentInParent<PlayerScript>();

        if (player == null)
        {
            Debug.LogWarning("VisionConeMask: no PlayerScript found in parent. " +
                              "This should be a child of the Player GameObject.");
        }

        GenerateMaskSprite();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector2 facing = player.FacingDirection;
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f; // -90 because texture's cone points "up" by default
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void GenerateMaskSprite()
    {
        // World size the texture needs to cover: the larger of the two radii, plus a small margin.
        float worldRadius = Mathf.Max(coneRadius, circleRadius) + 0.25f;
        float worldDiameter = worldRadius * 2f;
        float pixelsPerUnit = textureResolution / worldDiameter;

        var texture = new Texture2D(textureResolution, textureResolution, TextureFormat.Alpha8, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(textureResolution / 2f, textureResolution / 2f);
        float circleRadiusPixels = circleRadius * pixelsPerUnit;
        float coneRadiusPixels = coneRadius * pixelsPerUnit;
        float halfConeAngleDeg = coneAngleDegrees * 0.5f;
        const float angleFeatherDeg = 3f; // small fixed angular softening, independent of pixel feather
        float safeFeather = Mathf.Max(featherPixels, 0.001f);

        for (int y = 0; y < textureResolution; y++)
        {
            for (int x = 0; x < textureResolution; x++)
            {
                Vector2 pixelOffset = new Vector2(x, y) - center;
                Vector2 worldOffset = pixelOffset / pixelsPerUnit;
                float distPixels = pixelOffset.magnitude;

                // Circle coverage: 1 inside, fades to 0 over `featherPixels` at the boundary.
                float circleAlpha = 1f - Mathf.Clamp01((distPixels - (circleRadiusPixels - safeFeather)) / safeFeather);

                // Cone coverage: combines a radius fade and an angle fade, multiplied together
                // so a pixel only counts as "in the cone" if it's within both bounds.
                float angleFromForwardDeg = Vector2.Angle(Vector2.up, worldOffset);
                float coneRadiusAlpha = 1f - Mathf.Clamp01((distPixels - (coneRadiusPixels - safeFeather)) / safeFeather);
                float coneAngleAlpha = 1f - Mathf.Clamp01((angleFromForwardDeg - (halfConeAngleDeg - angleFeatherDeg)) / angleFeatherDeg);
                float coneAlpha = coneRadiusAlpha * coneAngleAlpha;

                float alpha = Mathf.Clamp01(Mathf.Max(circleAlpha, coneAlpha));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        var sprite = Sprite.Create(
            texture,
            new Rect(0, 0, textureResolution, textureResolution),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit
        );

        spriteMask.sprite = sprite;
    }
}