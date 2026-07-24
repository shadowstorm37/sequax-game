using UnityEngine;

/// <summary>
/// Plays the monster's pre-drawn run cycle for whichever of the 8 compass directions it is
/// currently moving in, and falls back to a standing sprite when it stops.
///
/// The art is drawn per facing from a high top-down angle, so it must never be spun by the
/// transform - that would rotate an already-angled drawing. EnemyMovement therefore no longer
/// rotates the body at all (nothing read its rotation; the collider is a circle, so facing was
/// purely cosmetic). Facing is expressed here by swapping sprites instead. The renderer still
/// lives on a child held world-upright, matching PlayerDirectionalSprite, so any rotation that
/// creeps back onto the root - from physics or future code - can't tilt the art.
///
/// Direction comes from actual Rigidbody2D velocity rather than the AI's intended heading, so
/// what you see is where it is really going after obstacle avoidance has had its say.
///
/// Any unassigned direction borrows the nearest assigned one by angle, so a partial set of art
/// still runs and new frames can be dropped into the Inspector without touching this file.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyDirectionalSprite : MonoBehaviour
{
    [System.Serializable]
    public class DirectionArt
    {
        [Tooltip("Standing sprite, shown when not moving.")]
        public Sprite idle;
        [Tooltip("Run cycle frames, in order. Drag all frames of one direction in at once.")]
        public Sprite[] runFrames;
    }

    // Order matches SlotAngles below. Compass names match the art folders on disk.
    [Header("Directional Art (compass order)")]
    [SerializeField] private DirectionArt north = new DirectionArt();
    [SerializeField] private DirectionArt northEast = new DirectionArt();
    [SerializeField] private DirectionArt east = new DirectionArt();
    [SerializeField] private DirectionArt southEast = new DirectionArt();
    [SerializeField] private DirectionArt south = new DirectionArt();
    [SerializeField] private DirectionArt southWest = new DirectionArt();
    [SerializeField] private DirectionArt west = new DirectionArt();
    [SerializeField] private DirectionArt northWest = new DirectionArt();

    [Header("Rendering")]
    [Tooltip("Uniform scale applied to the art (1 = sprite's native size).")]
    [SerializeField] private float scaleMultiplier = 1f;
    [Tooltip("Used only if no existing SpriteRenderer is found to copy sorting from.")]
    [SerializeField] private string fallbackSortingLayer = "Default";
    [SerializeField] private int fallbackSortingOrder = 0;

    [Header("Animation")]
    [Tooltip("Run cycle playback rate at nominal speed.")]
    [SerializeField] private float framesPerSecond = 10f;
    [Tooltip("Speed the run cycle up as the monster moves faster, so the legs match the ground.")]
    [SerializeField] private bool scaleWithSpeed = true;
    [Tooltip("Speed at which the cycle plays at exactly Frames Per Second.")]
    [SerializeField] private float nominalSpeed = 3.5f;

    [Header("Shadow")]
    [Tooltip("Cast a long dark silhouette away from the player, who carries the only light.")]
    [SerializeField] private bool castShadow = true;
    [Tooltip("Shadow tint (rgb) and strength (alpha). It still fades with the vision cone on top of this.")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 231f / 255f);
    [Tooltip("Nudge along the cast direction, in world units. Negative pulls the shadow back " +
             "toward the player, positive pushes it further away.")]
    [SerializeField] private float shadowLength = 0.9f;
    [Tooltip("How much of the sprite frame he actually fills top to bottom. The art has a lot of " +
             "transparent padding, so the frame is taller than he is - lowering this pulls the " +
             "shadow's near end back onto his feet.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float shadowContentFill = 0.134f;
    [Tooltip("How many times his own length the shadow spreads out behind him. 1 = no stretch.")]
    [SerializeField] private float shadowStretch = 1.8f;
    [Tooltip("Width of the shadow relative to the body.")]
    [SerializeField] private float shadowWidth = 1.55f;

    [Header("Motion Smoothing")]
    [Tooltip("How fast the smoothed heading chases actual velocity. Lower = steadier facing, slower to turn.")]
    [SerializeField] private float headingSmoothing = 6f;
    [Tooltip("Smoothed speed below which he counts as going nowhere - shuffling in place rather than travelling.")]
    [SerializeField] private float hoverSpeedThreshold = 1.2f;

    [Header("Eerie Idle")]
    [Tooltip("While going nowhere, stand still and stare at the player instead of facing his own jitter.")]
    [SerializeField] private bool starePlayerWhenIdle = true;
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Once he settles, hold still at least this long (random between x and y) so a single " +
             "frame of boundary jitter can't snap him back into a run.")]
    [SerializeField] private Vector2 idleHoldSeconds = new Vector2(1f, 3f);
    [Tooltip("Smoothed speed that cancels an idle hold immediately, so a real lunge is never " +
             "masked as standing still. Keep this above Hover Speed Threshold.")]
    [SerializeField] private float lungeBreakoutSpeed = 3f;

    // Canonical angle (atan2 degrees) for each slot, matched by index.
    private static readonly float[] SlotAngles = { 90f, 45f, 0f, -45f, -90f, -135f, 180f, 135f };

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shadowRenderer;
    private DirectionArt[] slots;
    private Transform playerTransform;

    private int currentSlot = -1;
    private float frameTimer;
    private int frameIndex;

    private Vector2 smoothedVelocity;
    private float idleHoldRemaining;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        slots = new[] { north, northEast, east, southEast, south, southWest, west, northWest };
        spriteRenderer = CreateUprightRenderer();
        if (castShadow) shadowRenderer = CreateShadowRenderer(spriteRenderer);

        var playerObj = GameObject.FindWithTag(playerTag);
        if (playerObj != null) playerTransform = playerObj.transform;
    }

    // A darkened silhouette drawn beneath the body. Registered with DetectableVisibility like the
    // body is, so it fades out with him - an opaque shadow under an invisible monster would give
    // his position away through the darkness.
    private SpriteRenderer CreateShadowRenderer(SpriteRenderer reference)
    {
        var shadow = new GameObject("MonsterShadow");
        shadow.transform.SetParent(transform, false);

        var sr = shadow.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = reference.sortingLayerID;
        sr.sortingOrder = reference.sortingOrder - 1; // under the werewolf, still over the ground
        sr.sharedMaterial = reference.sharedMaterial;
        sr.maskInteraction = reference.maskInteraction;
        sr.color = shadowColor;
        return sr;
    }

    private void Start()
    {
        // DetectableVisibility caches its renderer list in Awake and drives alpha to fade the
        // monster in and out of the vision cone. Ours is built at runtime, and Awake order
        // between components on one object is undefined, so register from Start (always after
        // every Awake) instead of hoping we were constructed first. Without this the monster
        // stays at full alpha and is visible straight through the darkness.
        var visibility = GetComponent<DetectableVisibility>();
        if (visibility != null)
        {
            visibility.RegisterRenderer(spriteRenderer);
            // Registered after its tint is set, so the shadow's own alpha becomes the base the
            // cone visibility scales - it fades with him without losing its darkness.
            if (shadowRenderer != null) visibility.RegisterRenderer(shadowRenderer);
        }

        // Missing art is otherwise a silent failure - LateUpdate just never assigns a sprite and
        // the monster renders as nothing, with the old renderer already switched off. Say so.
        int withArt = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (HasArt(slots[i])) withArt++;
        }

        if (withArt == 0)
        {
            Debug.LogError(
                $"[EnemyDirectionalSprite] '{name}' has no directional art assigned, so it will draw " +
                "nothing. Right-click the component header and pick 'Auto-Populate From Art Folders'.", this);
            return;
        }

        Debug.Log(
            $"[EnemyDirectionalSprite] '{name}': {withArt}/8 directions have art. " +
            $"Layer '{SortingLayer.IDToName(spriteRenderer.sortingLayerID)}' order {spriteRenderer.sortingOrder}, " +
            $"fades with vision cone: {(visibility != null)}.", this);
    }

    // Adopt sorting from whatever renderer already draws the monster, then hide it so we don't
    // draw two overlapping creatures. Falls back to Inspector values if there is nothing to copy.
    private SpriteRenderer CreateUprightRenderer()
    {
        var existing = GetComponentInChildren<SpriteRenderer>();

        var child = new GameObject("MonsterSprite");
        child.transform.SetParent(transform, false);
        child.transform.localScale = Vector3.one * scaleMultiplier;

        var sr = child.AddComponent<SpriteRenderer>();
        if (existing != null)
        {
            sr.sortingLayerID = existing.sortingLayerID;
            sr.sortingOrder = existing.sortingOrder;
            sr.sharedMaterial = existing.sharedMaterial;
            sr.maskInteraction = existing.maskInteraction;
            existing.enabled = false;
        }
        else
        {
            sr.sortingLayerName = fallbackSortingLayer;
            sr.sortingOrder = fallbackSortingOrder;
        }
        return sr;
    }

    private void LateUpdate()
    {
        // Hold the art world-upright regardless of what the root is doing. Scale is re-applied
        // here rather than only in Awake so size can be tuned live in the Inspector during play.
        spriteRenderer.transform.rotation = Quaternion.identity;
        spriteRenderer.transform.localScale = Vector3.one * scaleMultiplier;

        // The AI holds a stalk radius by crossing its stopping distance over and over, so raw
        // velocity flips direction several times a second. Averaging it cancels that out: the
        // smoothed vector's *direction* is a steady heading, and its *magnitude* separates real
        // travel from shuffling on the spot (opposing jitter sums to nearly zero, a run does not).
        // Exponential form so the smoothing is the same at any frame rate.
        float catchUp = 1f - Mathf.Exp(-headingSmoothing * Time.deltaTime);
        smoothedVelocity = Vector2.Lerp(smoothedVelocity, body.linearVelocity, catchUp);
        float travelSpeed = smoothedVelocity.magnitude;

        bool wantsToTravel = travelSpeed >= hoverSpeedThreshold;

        // A decisive move always wins over a hold - being frozen mid-lunge would look broken and
        // would hide the one moment the player most needs to read.
        if (travelSpeed >= lungeBreakoutSpeed)
        {
            idleHoldRemaining = 0f;
        }
        else if (idleHoldRemaining > 0f)
        {
            idleHoldRemaining -= Time.deltaTime;
        }
        else if (!wantsToTravel)
        {
            // Just settled: commit to standing still for a beat, so he reads as deliberately
            // watching rather than stuttering between stances.
            idleHoldRemaining = Random.Range(idleHoldSeconds.x, idleHoldSeconds.y);
        }

        bool travelling = wantsToTravel && idleHoldRemaining <= 0f;

        Vector2 facing = travelling ? smoothedVelocity : StareDirection();
        if (facing.sqrMagnitude > 0.0001f)
        {
            int slot = NearestSlot(Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            if (slot != currentSlot)
            {
                currentSlot = slot;
                frameIndex = 0;
                frameTimer = 0f;
            }
        }

        if (currentSlot < 0) currentSlot = NearestSlot(-90f); // face south until it first moves

        DirectionArt art = ResolveArt(currentSlot);
        if (art == null) return;

        if (travelling && art.runFrames != null && art.runFrames.Length > 0)
        {
            // Drive the cycle from the smoothed speed too, or the legs would stutter in step
            // with the very jitter we just filtered out.
            AdvanceFrame(art.runFrames.Length, travelSpeed);
            spriteRenderer.sprite = art.runFrames[frameIndex];
        }
        else
        {
            frameIndex = 0;
            frameTimer = 0f;
            // A direction with no idle art still reads correctly from its first run frame.
            spriteRenderer.sprite = art.idle != null
                ? art.idle
                : (art.runFrames != null && art.runFrames.Length > 0 ? art.runFrames[0] : spriteRenderer.sprite);
        }

        UpdateShadow();
    }

    // Thrown directly away from the player, who carries the only light in the scene, and
    // stretched along that axis so it reads as a long cast shadow rather than a dark copy.
    private void UpdateShadow()
    {
        if (shadowRenderer == null) return;

        shadowRenderer.sprite = spriteRenderer.sprite;

        Vector2 awayFromLight = playerTransform != null
            ? (Vector2)transform.position - (Vector2)playerTransform.position
            : Vector2.down;

        if (awayFromLight.sqrMagnitude < 0.0001f) awayFromLight = Vector2.down;
        awayFromLight.Normalize();

        Transform st = shadowRenderer.transform;

        // Align the silhouette's local up with the cast direction, then stretch along it. The
        // body art stays upright; only this smeared copy turns, which is what sells the length.
        st.rotation = Quaternion.LookRotation(Vector3.forward, awayFromLight);
        st.localScale = new Vector3(scaleMultiplier * shadowWidth, scaleMultiplier * shadowStretch, 1f);

        // The sprites pivot at their centre, so stretching alone would grow the shadow equally in
        // both directions and bury half of it under him. Push it out by exactly the length the
        // stretch added, which pins the near end at his feet and sends all the growth backwards.
        //
        // Measure against how much of the frame he actually occupies, not the frame itself: the
        // art is a 228px square with generous transparent padding, so sprite.bounds is far taller
        // than the creature and compensating for the full height throws the shadow way past him.
        float halfHeight = spriteRenderer.sprite != null
            ? spriteRenderer.sprite.bounds.extents.y * shadowContentFill
                * scaleMultiplier * Mathf.Abs(transform.lossyScale.y)
            : 0f;
        float stretchOffset = halfHeight * (shadowStretch - 1f);

        st.position = (Vector2)transform.position + awayFromLight * (stretchOffset + shadowLength);
    }

    // Where he looks while standing still. Staring at the player reads as stalking; falling back
    // to the last heading just keeps him from snapping to a default when there is no player.
    private Vector2 StareDirection()
    {
        if (!starePlayerWhenIdle || playerTransform == null) return Vector2.zero;
        return (Vector2)playerTransform.position - (Vector2)transform.position;
    }

    private void AdvanceFrame(int frameCount, float speed)
    {
        float fps = framesPerSecond;
        if (scaleWithSpeed && nominalSpeed > 0.01f) fps *= speed / nominalSpeed;
        if (fps <= 0.01f) return;

        frameTimer += Time.deltaTime * fps;
        while (frameTimer >= 1f)
        {
            frameTimer -= 1f;
            frameIndex = (frameIndex + 1) % frameCount;
        }
        if (frameIndex >= frameCount) frameIndex = 0;
    }

    // Nearest slot by angle that actually has art, so missing directions borrow the closest
    // one that exists instead of rendering nothing.
    private int NearestSlot(float angle)
    {
        int best = -1;
        float bestDelta = float.MaxValue;
        for (int i = 0; i < slots.Length; i++)
        {
            if (!HasArt(slots[i])) continue;
            float delta = Mathf.Abs(Mathf.DeltaAngle(angle, SlotAngles[i]));
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }
        return best;
    }

    private DirectionArt ResolveArt(int slot)
    {
        if (slot < 0 || slot >= slots.Length) return null;
        return HasArt(slots[slot]) ? slots[slot] : null;
    }

    private static bool HasArt(DirectionArt art)
    {
        if (art == null) return false;
        if (art.idle != null) return true;
        return art.runFrames != null && art.runFrames.Length > 0;
    }

#if UNITY_EDITOR
    private const string ArtRoot = "Assets/Art/Environment/Sprites/Monster";

    // Folder names on disk, in the same order as the slots above.
    private static readonly string[] FolderNames =
        { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

    /// <summary>
    /// Fills all 8 directions from the art folders so nobody has to drag 40 sprites by hand.
    /// Right-click the component header in the Inspector to run it. Lives here behind
    /// UNITY_EDITOR rather than in an Assets/Editor script so it adds no new folder.
    /// </summary>
    [ContextMenu("Auto-Populate From Art Folders")]
    private void AutoPopulateFromArtFolders()
    {
        var targets = new[] { north, northEast, east, southEast, south, southWest, west, northWest };
        int idleCount = 0, frameCount = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            string dir = FolderNames[i];

            var idleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/Idle/{dir}.png");
            if (idleSprite != null)
            {
                targets[i].idle = idleSprite;
                idleCount++;
            }

            string runFolder = $"{ArtRoot}/Run/{dir}";
            if (!UnityEditor.AssetDatabase.IsValidFolder(runFolder)) continue;

            // Sort by path so frame_000..frame_003 stay in cycle order; FindAssets does not guarantee it.
            var paths = new System.Collections.Generic.List<string>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { runFolder }))
            {
                paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            }
            paths.Sort(System.StringComparer.Ordinal);

            var frames = new System.Collections.Generic.List<Sprite>();
            foreach (string path in paths)
            {
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) frames.Add(sprite);
            }

            targets[i].runFrames = frames.ToArray();
            frameCount += frames.Count;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[EnemyDirectionalSprite] Populated {idleCount} idle sprites and {frameCount} run frames across 8 directions.");
    }
#endif
}
