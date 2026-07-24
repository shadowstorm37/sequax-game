using UnityEngine;

/// <summary>
/// Computes the player's line of sight (circle + forward-facing cone, clipped by
/// obstacles) and bakes it into a world-aligned alpha texture: 1 = fully visible,
/// 0 = fully dark, with soft gradients in between. That texture is published as a
/// global shader texture, along with the world-space params needed to sample it,
/// for the VisionDesaturate full-screen shader (see Assets/Shaders/VisionDesaturate.shader)
/// to read back and desaturate whatever's on screen outside the player's sight.
///
/// This does NOT render anything itself - it's a data source for the Full Screen
/// Pass Renderer Feature. See VisionDesaturate.shader for the Editor-side setup.
///
/// SETUP:
/// 1. Create an empty child GameObject under Player, name it "VisionMask".
/// 2. Reset its local position to (0,0,0) - it must sit exactly on the player.
/// 3. Add this script to it.
/// 4. Put trees/rocks/walls that should block vision on a dedicated layer and
///    assign that layer to obstacleLayerMask below. They need a Collider2D.
/// 5. Set up the Full Screen Pass Renderer Feature per VisionDesaturate.shader's
///    header comment.
/// </summary>
public class VisionConeMask : MonoBehaviour
{
    [Header("Shape")]
    [Tooltip("Total width of the cone in degrees (e.g. 45 = 22.5 degrees either side of facing direction).")]
    [SerializeField] private float coneAngleDegrees = 45f;
    [Tooltip("How far the cone reaches, in world units.")]
    [SerializeField] private float coneRadius = 6f;
    [Tooltip("Radius of the small always-visible circle around the player, in world units.")]
    [SerializeField] private float circleRadius = 1.5f;

    [Header("Occlusion")]
    [Tooltip("Layers that block vision (trees, rocks, walls). These need a Collider2D.")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [Tooltip("Number of rays cast around the player to find occluders. Higher = more precise obstacle edges, more expensive.")]
    [SerializeField] private int occlusionRayCount = 180;

    [Header("Edge Shading")]
    [Tooltip("How far (world units) before the edge of the cone/circle/obstacle the darkness starts creeping in.")]
    [SerializeField] private float fadeWidth = 1.5f;
    [Tooltip("Angular softening (degrees) applied to the cone's side edges.")]
    [SerializeField] private float fadeAngleDegrees = 6f;

    [Header("Texture Quality")]
    [Tooltip("Resolution of the generated vision texture. Higher = smoother edges, more expensive to regenerate.")]
    [SerializeField] private int textureResolution = 256;

    [Header("Performance")]
    [Tooltip("Seconds between vision texture regenerations. Lower = more responsive to moving obstacles/facing, more expensive.")]
    [SerializeField] private float updateInterval = 0.1f;
    [Tooltip("How far the player must move (world units) before the texture is worth rebuilding.")]
    [SerializeField] private float rebuildMoveThreshold = 0.02f;
    [Tooltip("How far the player must turn (degrees) before the texture is worth rebuilding.")]
    [SerializeField] private float rebuildTurnThreshold = 0.5f;
    [Tooltip("Rebuild at least this often even when the player is perfectly still, so an occluder " +
             "that moves without telling us can't leave the texture stale forever.")]
    [SerializeField] private float maxSecondsBetweenRebuilds = 0.5f;

    private static readonly int VisionTexId = Shader.PropertyToID("_VisionTex");
    private static readonly int VisionOriginId = Shader.PropertyToID("_VisionOrigin");
    private static readonly int VisionWorldDiameterId = Shader.PropertyToID("_VisionWorldDiameter");
    private static readonly int VisionCamWorldPosId = Shader.PropertyToID("_VisionCamWorldPos");
    private static readonly int VisionOrthoSizeId = Shader.PropertyToID("_VisionOrthoSize");

    private PlayerScript player;
    private Camera mainCamera;

    private Texture2D visionTexture;
    private Color32[] visionPixels;
    private float[] visibleDistances;

    // A pixel's angle and distance from the texture centre are fixed by its coordinate - they are
    // identical on every rebuild. Caching them turns the old per-pixel Atan2 and sqrt (65k of each,
    // ten times a second) into two array reads. Angle is scale-invariant, and distance is stored in
    // pixels and divided by pixelsPerUnit at use, so tweaking cone radius never invalidates these;
    // only a resolution change does.
    private float[] pixelAngles;
    private float[] pixelDistances;
    private int cachedTableResolution = -1;

    private float pixelsPerUnit;
    private float worldDiameter;
    private float updateTimer;

    private Vector2 lastBakeOrigin;
    private Vector2 lastBakeFacing;
    private float secondsSinceRebuild;
    private bool hasBaked;

    private void Awake()
    {
        player = GetComponentInParent<PlayerScript>();
        mainCamera = Camera.main;

        if (player == null)
        {
            Debug.LogWarning("VisionConeMask: no PlayerScript found in parent. " +
                              "This should be a child of the Player GameObject.");
        }

        occlusionRayCount = Mathf.Max(occlusionRayCount, 8);
        visibleDistances = new float[occlusionRayCount];

        InitializeTexture();
        RegenerateVisionTexture();
    }

    private void LateUpdate()
    {
        PublishCameraGlobals();

        if (player == null) return;

        updateTimer += Time.deltaTime;
        secondsSinceRebuild += Time.deltaTime;
        if (updateTimer < updateInterval) return;

        // Standing still and facing the same way produces a bit-identical texture, so skip the
        // whole rebuild. Deliberately don't reset updateTimer here: it stays expired, so this
        // cheap check runs every frame and the rebuild fires the instant the player does move,
        // rather than waiting out another interval.
        if (!ShouldRebuild()) return;

        updateTimer = 0f;
        RegenerateVisionTexture();
    }

    private bool ShouldRebuild()
    {
        if (!hasBaked) return true;
        if (secondsSinceRebuild >= maxSecondsBetweenRebuilds) return true;

        Vector2 origin = transform.position;
        if ((origin - lastBakeOrigin).sqrMagnitude > rebuildMoveThreshold * rebuildMoveThreshold) return true;

        Vector2 facing = player != null ? player.FacingDirection : Vector2.up;
        return Vector2.Angle(facing, lastBakeFacing) > rebuildTurnThreshold;
    }

    /// <summary>
    /// True if worldPoint is within the always-visible circle or the facing cone, with a
    /// clear line of sight (no obstacle between the player and it). Intended for enemies,
    /// items, etc. that should be fully hidden until actually spotted.
    /// </summary>
    public bool IsPointVisible(Vector2 worldPoint)
    {
        if (player == null) return false;

        Vector2 origin = transform.position;
        Vector2 toPoint = worldPoint - origin;
        float distWorld = toPoint.magnitude;

        float maxRadius = Mathf.Max(coneRadius, circleRadius);
        if (distWorld > maxRadius) return false;

        if (distWorld > 0.0001f)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, toPoint.normalized, distWorld, obstacleLayerMask);
            if (hit.collider != null) return false;
        }

        if (distWorld <= circleRadius) return true;
        if (distWorld > coneRadius) return false;

        Vector2 facing = player.FacingDirection;
        float facingAngleDeg = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        float pointAngleDeg = Mathf.Atan2(toPoint.y, toPoint.x) * Mathf.Rad2Deg;
        float angleDiff = Mathf.Abs(Mathf.DeltaAngle(pointAngleDeg, facingAngleDeg));
        return angleDiff <= coneAngleDegrees * 0.5f;
    }

    private void InitializeTexture()
    {
        UpdateWorldSizing();

        visionPixels = new Color32[textureResolution * textureResolution];
        visionTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.Alpha8, false)
        {
            wrapMode = TextureWrapMode.Clamp
        };

        BuildPixelTables();
    }

    // Built once per resolution. Costs one Atan2 and one sqrt per pixel here so the rebuild loop
    // needs neither.
    private void BuildPixelTables()
    {
        int res = textureResolution;
        if (cachedTableResolution == res && pixelAngles != null) return;

        pixelAngles = new float[res * res];
        pixelDistances = new float[res * res];

        float centerPixel = res / 2f;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x - centerPixel;
                float dy = y - centerPixel;
                int idx = y * res + x;

                pixelDistances[idx] = Mathf.Sqrt(dx * dx + dy * dy);

                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                pixelAngles[idx] = angle;
            }
        }

        cachedTableResolution = res;
    }

    /// <summary>
    /// Forces the next allowed rebuild to happen even if the player has not moved. Call this if
    /// something other than the player changes what blocks vision - a door opening, an occluder
    /// being enabled - since the movement check alone cannot see that.
    /// </summary>
    public void MarkDirty()
    {
        hasBaked = false;
    }

    // Recomputed every regeneration (not just once in Awake) so that tweaking Cone Radius /
    // Circle Radius in the Inspector - including during Play Mode - doesn't leave the texture's
    // world-space sampling bounds stale. A stale (too-small) worldDiameter hard-clips the vision
    // to a square well inside the actual cone radius, since the shader treats "outside the
    // texture bounds" as fully hidden.
    private void UpdateWorldSizing()
    {
        float worldRadius = Mathf.Max(coneRadius, circleRadius) + 0.25f;
        worldDiameter = worldRadius * 2f;
        pixelsPerUnit = textureResolution / worldDiameter;
    }

    private void PublishCameraGlobals()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        Shader.SetGlobalVector(VisionCamWorldPosId, mainCamera.transform.position);
        Shader.SetGlobalFloat(VisionOrthoSizeId, mainCamera.orthographicSize);
    }

    private void RegenerateVisionTexture()
    {
        // Resolution is only read when the buffers are built, so changing it in the Inspector at
        // runtime would otherwise leave the loop indexing past the end of the old arrays.
        if (visionPixels == null || visionPixels.Length != textureResolution * textureResolution)
        {
            if (visionTexture != null) Destroy(visionTexture);
            InitializeTexture();
        }

        UpdateWorldSizing();

        Vector2 origin = transform.position;
        Vector2 facing = player != null ? player.FacingDirection : Vector2.up;
        float facingAngleDeg = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;

        // Ray count sets how finely obstacle edges are resolved, and is worth tuning against the
        // texture resolution. Rebuilt here rather than only in Awake so it can be dialled in
        // during Play Mode without the sampler indexing past the end of the old array.
        if (visibleDistances == null || visibleDistances.Length != occlusionRayCount)
        {
            occlusionRayCount = Mathf.Max(occlusionRayCount, 8);
            visibleDistances = new float[occlusionRayCount];
        }

        float maxCastRadius = Mathf.Max(coneRadius, circleRadius);
        float rayStepDeg = 360f / occlusionRayCount;

        for (int i = 0; i < occlusionRayCount; i++)
        {
            float angleDeg = i * rayStepDeg;
            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
            RaycastHit2D hit = Physics2D.Raycast(origin, dir, maxCastRadius, obstacleLayerMask);

            if (hit.collider == null)
            {
                visibleDistances[i] = maxCastRadius;
                continue;
            }

            // Stop at the obstacle's near surface, same as any other occluder - obstacles
            // outside the cone/circle should gray out like everything else, not stay lit
            // just because part of them pokes into view.
            visibleDistances[i] = Mathf.Min(hit.distance, maxCastRadius);
        }

        float halfConeAngleDeg = coneAngleDegrees * 0.5f;

        // Flat single loop over the cached tables. Multiply by the reciprocal rather than dividing
        // per pixel - at 65k pixels a rebuild the difference is worth having.
        int pixelCount = textureResolution * textureResolution;
        float invPixelsPerUnit = 1f / pixelsPerUnit;

        for (int idx = 0; idx < pixelCount; idx++)
        {
            float pixelAngleDeg = pixelAngles[idx];
            float distWorld = pixelDistances[idx] * invPixelsPerUnit;

            float obstacleDist = SampleObstacleDistance(pixelAngleDeg, rayStepDeg);
            float circleEffectiveRadius = Mathf.Min(circleRadius, obstacleDist);
            float coneEffectiveRadius = Mathf.Min(coneRadius, obstacleDist);
            float angleFromForwardDeg = Mathf.Abs(Mathf.DeltaAngle(pixelAngleDeg, facingAngleDeg));

            float visibility = ComputeVisibility(
                distWorld, angleFromForwardDeg, circleEffectiveRadius, coneEffectiveRadius,
                halfConeAngleDeg, fadeWidth, fadeAngleDegrees);

            visionPixels[idx] = new Color32(255, 255, 255, (byte)(visibility * 255f));
        }

        visionTexture.SetPixels32(visionPixels);
        visionTexture.Apply(false);

        Shader.SetGlobalTexture(VisionTexId, visionTexture);
        Shader.SetGlobalVector(VisionOriginId, origin);
        Shader.SetGlobalFloat(VisionWorldDiameterId, worldDiameter);

        lastBakeOrigin = origin;
        lastBakeFacing = facing;
        secondsSinceRebuild = 0f;
        hasBaked = true;
    }

    // Combines a radius fade and an angle fade, multiplied together so a pixel only
    // counts as visible if it's within both the (obstacle-clipped) radius and the angle.
    private static float ComputeVisibility(
        float distWorld, float angleFromForwardDeg,
        float circleEffectiveRadius, float coneEffectiveRadius,
        float halfConeAngleDeg, float radialFeather, float angleFeatherDeg)
    {
        float safeRadial = Mathf.Max(radialFeather, 0.001f);
        float safeAngle = Mathf.Max(angleFeatherDeg, 0.001f);

        float circleAlpha = 1f - Mathf.Clamp01((distWorld - (circleEffectiveRadius - safeRadial)) / safeRadial);

        float coneRadiusAlpha = 1f - Mathf.Clamp01((distWorld - (coneEffectiveRadius - safeRadial)) / safeRadial);
        float coneAngleAlpha = 1f - Mathf.Clamp01((angleFromForwardDeg - (halfConeAngleDeg - safeAngle)) / safeAngle);
        float coneAlpha = coneRadiusAlpha * coneAngleAlpha;

        return Mathf.Clamp01(Mathf.Max(circleAlpha, coneAlpha));
    }

    /// <summary>
    /// Continuous 0-1 visibility for a single world point - same radius/angle fade
    /// used for the desaturation texture, but sampled directly instead of baked into
    /// a texture. Use this (instead of IsPointVisible) when you want a smooth fade
    /// in/out rather than a hard cut, e.g. for DetectableVisibility.
    /// </summary>
    public float SampleVisibility(Vector2 worldPoint)
    {
        if (player == null) return 0f;

        Vector2 origin = transform.position;
        Vector2 toPoint = worldPoint - origin;
        float distWorld = toPoint.magnitude;

        float maxRadius = Mathf.Max(coneRadius, circleRadius);
        if (distWorld > maxRadius + fadeWidth) return 0f;

        float obstacleDist = maxRadius;
        if (distWorld > 0.0001f)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, toPoint.normalized, distWorld, obstacleLayerMask);
            if (hit.collider != null)
            {
                obstacleDist = hit.distance;
                if (distWorld > obstacleDist) return 0f; // point itself is behind the obstacle
            }
        }

        Vector2 facing = player.FacingDirection;
        float facingAngleDeg = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        float pointAngleDeg = Mathf.Atan2(toPoint.y, toPoint.x) * Mathf.Rad2Deg;
        float angleFromForwardDeg = Mathf.Abs(Mathf.DeltaAngle(pointAngleDeg, facingAngleDeg));

        float circleEffectiveRadius = Mathf.Min(circleRadius, obstacleDist);
        float coneEffectiveRadius = Mathf.Min(coneRadius, obstacleDist);
        float halfConeAngleDeg = coneAngleDegrees * 0.5f;

        return ComputeVisibility(
            distWorld, angleFromForwardDeg, circleEffectiveRadius, coneEffectiveRadius,
            halfConeAngleDeg, fadeWidth, fadeAngleDegrees);
    }

    private float SampleObstacleDistance(float angleDeg, float rayStepDeg)
    {
        float idxF = angleDeg / rayStepDeg;
        int i0 = (int)idxF % occlusionRayCount;
        int i1 = (i0 + 1) % occlusionRayCount;
        float t = idxF - (int)idxF;
        return Mathf.Lerp(visibleDistances[i0], visibleDistances[i1], t);
    }
}
