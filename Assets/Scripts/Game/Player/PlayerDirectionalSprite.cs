using UnityEngine;

/// <summary>
/// Plays the hiker's two-step walk cycle for whichever of the 8 compass directions he is
/// currently aiming in, and stands on the neutral pose when he isn't moving.
///
/// The cycle is neutral, step 1, neutral, step 2 - so the art only needs one pose per foot and
/// the hiker passes back through a legs-together frame between them, which is what makes two
/// drawings read as a walk. A vertical bob rides along on top, peaking on the neutral frames and
/// dipping on the step frames: legs together is the tallest part of a stride and legs apart the
/// shortest, so the bounce lands exactly where the body would really rise and fall.
///
/// PlayerScript rotates the root transform to face the mouse (that rotation drives the vision
/// cone and the enemy's evasion via the player's transform.up). The directional art is pre-drawn
/// per facing, so it must NOT also be spun by that transform or it would double-rotate. This
/// creates a child renderer held world-upright and expresses facing by swapping to the sprite for
/// the current aim octant, hiding the placeholder hexagon on the root. A second child draws a
/// darkened silhouette cast opposite the aim as a shadow, which the fog post-process dims into
/// the darkness on its far end.
///
/// Aim and travel are independent for the player in a way they were not for the monster, which
/// faced wherever it was going. Facing here comes from the mouse, playback rate from the
/// Rigidbody2D's actual velocity, and travelling against the aim runs the cycle backwards so
/// backing away with the flashlight held on something still steps the right way. Reading real
/// velocity rather than input also means every external speed modifier - the creek slowdown, the
/// backpedal penalty, crouch and sprint - slows or quickens the legs for free.
///
/// Any unassigned direction borrows the nearest assigned one by angle, so a partial set of art
/// still animates and new frames can be dropped into the Inspector without touching this file.
/// </summary>
[RequireComponent(typeof(PlayerScript))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDirectionalSprite : MonoBehaviour
{
    [System.Serializable]
    public class DirectionArt
    {
        [Tooltip("Legs-together pose. Shown standing still, and again between every step.")]
        public Sprite neutral;
        [Tooltip("One pose per foot. The cycle alternates neutral, step 0, neutral, step 1.")]
        public Sprite[] stepFrames;
    }

    // Order matches SlotAngles below. Compass names match the art files on disk.
    [Header("Directional Art (compass order)")]
    [Tooltip("Assign the ones you have; empty slots borrow the nearest assigned direction.")]
    [SerializeField] private DirectionArt north = new DirectionArt();
    [SerializeField] private DirectionArt northEast = new DirectionArt();
    [SerializeField] private DirectionArt east = new DirectionArt();
    [SerializeField] private DirectionArt southEast = new DirectionArt();
    [SerializeField] private DirectionArt south = new DirectionArt();
    [SerializeField] private DirectionArt southWest = new DirectionArt();
    [SerializeField] private DirectionArt west = new DirectionArt();
    [SerializeField] private DirectionArt northWest = new DirectionArt();

    [Header("Rendering")]
    [Tooltip("Uniform scale applied to the character art (1 = sprite's native size).")]
    [SerializeField] private float scaleMultiplier = 0.84375f;

    [Header("Animation")]
    [Tooltip("Poses per second at nominal speed. One full stride is four poses " +
             "(neutral, step, neutral, step), so 6 here is about 1.5 strides a second.")]
    [SerializeField] private float posesPerSecond = 6f;
    [Tooltip("Step faster as he moves faster, so the legs match the ground. This is what makes " +
             "crouch, walk, sprint and wading through a creek read differently from one cycle.")]
    [SerializeField] private bool scaleWithSpeed = true;
    [Tooltip("Speed at which the cycle plays at exactly Poses Per Second. Match to walk speed.")]
    [SerializeField] private float nominalSpeed = 3.5f;
    [Tooltip("Speed below which he counts as standing still and holds the neutral pose. Also " +
             "stops the legs churning when he's walking into a wall at zero actual velocity.")]
    [SerializeField] private float moveSpeedThreshold = 0.15f;
    [Tooltip("Run the cycle backwards when travelling against the aim, so backing away from " +
             "something while keeping the light on it steps backwards too.")]
    [SerializeField] private bool reverseOnBackpedal = true;

    [Header("Walk Bob")]
    [Tooltip("How far the body rises and falls over a stride, in world units. Small is better - " +
             "this should read as weight, not as hopping.")]
    [SerializeField] private float bobAmplitude = 0.035f;
    [Tooltip("Off snaps the bob between two heights on each pose change, which suits chunky pixel " +
             "art. On glides between them, peaking on the neutral pose and dipping on the steps.")]
    [SerializeField] private bool smoothBob = true;
    [Tooltip("How quickly the bob fades in when he starts walking and out when he stops, so " +
             "neither end snaps.")]
    [SerializeField] private float bobBlendSpeed = 8f;

    [Header("Shadow")]
    [Tooltip("Draw a darkened silhouette of the sprite cast opposite the aim (flashlight) " +
             "direction. It also fades into the fog on its own via the vision post-process.")]
    [SerializeField] private bool castShadow = true;
    [Tooltip("Shadow tint (rgb) and strength (alpha). Black = a plain dark shadow.")]
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.7f);
    [Tooltip("How far behind the player (opposite aim) the shadow is thrown, in world units.")]
    [SerializeField] private float shadowOffset = 0.35f;
    [Tooltip("Shadow size relative to the character sprite.")]
    [SerializeField] private float shadowScale = 1f;

    // Canonical direction (degrees, atan2 convention) for each slot below, matched by index.
    private static readonly float[] SlotAngles = { 90f, 45f, 0f, -45f, -90f, -135f, 180f, 135f };

    private PlayerScript player;
    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shadowRenderer;
    private DirectionArt[] slots;

    private int currentSlot = -1;
    private float posePhase;   // continuous position in the cycle, in poses
    private float bobBlend;    // 0 standing, 1 walking

    private void Awake()
    {
        player = GetComponent<PlayerScript>();
        body = GetComponent<Rigidbody2D>();
        slots = new[] { north, northEast, east, southEast, south, southWest, west, northWest };

        // Checked before the placeholder is switched off: with no art we would otherwise hide the
        // only renderer on the object and leave an invisible player walking around.
        int withArt = CountDirectionsWithArt();

        spriteRenderer = CreateUprightRenderer(hideRoot: withArt > 0);
        if (castShadow) shadowRenderer = CreateShadowRenderer(spriteRenderer);

        if (withArt == 0)
        {
            // The expected state the first time the scene is opened after this component changed
            // shape: Unity cannot migrate the old flat Sprite fields onto the new per-direction
            // sets, so the previous assignments are dropped on load.
            Debug.LogError(
                $"[PlayerDirectionalSprite] '{name}' has no directional art assigned, so the " +
                "placeholder sprite is being left visible. Right-click the component header and " +
                "pick 'Auto-Populate From Art Folder'.", this);
            return;
        }

        // Match the starting aim direction so we don't flash a wrong-facing sprite on spawn.
        currentSlot = NearestSlot(AimAngle());
        ApplySprite(ResolveArt(currentSlot)?.neutral);
    }

    private int CountDirectionsWithArt()
    {
        int withArt = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (HasArt(slots[i])) withArt++;
        }
        return withArt;
    }

    // Move the visible art onto a child we hold upright, and hide the placeholder hexagon on the
    // root (which spins with the aim direction) - but only once there is art to replace it with,
    // so a missing set degrades to the placeholder rather than to nothing at all.
    private SpriteRenderer CreateUprightRenderer(bool hideRoot)
    {
        var rootRenderer = GetComponent<SpriteRenderer>();

        var child = new GameObject("HikerSprite");
        child.transform.SetParent(transform, false);
        child.transform.localScale = Vector3.one * scaleMultiplier;

        var sr = child.AddComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            sr.sortingLayerID = rootRenderer.sortingLayerID;
            sr.sortingOrder = rootRenderer.sortingOrder;
            sr.sharedMaterial = rootRenderer.sharedMaterial;
            sr.maskInteraction = rootRenderer.maskInteraction;
            rootRenderer.enabled = !hideRoot;
        }
        return sr;
    }

    // A darkened silhouette drawn just under the character. It's a normal transparent, so the
    // vision desaturate post-process (which darkens everything outside the cone) fades its far
    // end into the surrounding fog for free - no manual gradient needed.
    private SpriteRenderer CreateShadowRenderer(SpriteRenderer reference)
    {
        var shadow = new GameObject("HikerShadow");
        shadow.transform.SetParent(transform, false);
        shadow.transform.localScale = Vector3.one * (scaleMultiplier * shadowScale);

        var sr = shadow.AddComponent<SpriteRenderer>();
        sr.sortingLayerID = reference.sortingLayerID;
        sr.sortingOrder = reference.sortingOrder - 1; // behind the character, still above the ground
        sr.maskInteraction = reference.maskInteraction;
        sr.sharedMaterial = reference.sharedMaterial;
        sr.color = shadowColor;
        return sr;
    }

    private void LateUpdate()
    {
        // Hold the art world-upright regardless of what the root is doing. Scale is re-applied
        // here rather than only in Awake so size can be tuned live in the Inspector during play.
        spriteRenderer.transform.rotation = Quaternion.identity;
        spriteRenderer.transform.localScale = Vector3.one * scaleMultiplier;

        Vector2 aim = player.FacingDirection;
        Vector2 velocity = body.linearVelocity;
        float speed = velocity.magnitude;

        // Facing follows the mouse, not travel - he keeps the flashlight where you point it even
        // while strafing or backing away.
        if (aim.sqrMagnitude > 0.0001f)
        {
            // The phase deliberately carries across a turn instead of resetting, so sweeping the
            // mouse around while walking doesn't restart the cycle and double-step him.
            currentSlot = NearestSlot(Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
        }

        if (currentSlot < 0) currentSlot = NearestSlot(-90f); // face south until art exists

        DirectionArt art = ResolveArt(currentSlot);
        if (art == null) return;

        int poseCount = CycleLength(art);
        bool walking = speed >= moveSpeedThreshold && poseCount > 0;

        if (walking)
        {
            AdvancePhase(poseCount, speed, reverseOnBackpedal && Vector2.Dot(velocity, aim) < 0f);
            ApplySprite(PoseAt(art, Mathf.FloorToInt(posePhase)));
        }
        else
        {
            posePhase = 0f;
            ApplySprite(art.neutral != null ? art.neutral : PoseAt(art, 0));
        }

        UpdateBob(walking);
        UpdateShadow(aim);
    }

    // Neutral on every even pose, the next foot on every odd one: neutral, step 0, neutral,
    // step 1, ... A direction with no neutral drawn still walks, just without the passing frame.
    private Sprite PoseAt(DirectionArt art, int pose)
    {
        int steps = art.stepFrames != null ? art.stepFrames.Length : 0;
        if (steps == 0) return art.neutral;
        if (art.neutral == null) return art.stepFrames[((pose % steps) + steps) % steps];

        if (pose % 2 == 0) return art.neutral;
        return art.stepFrames[(pose / 2) % steps];
    }

    // Two poses per step (the step itself plus the neutral before it), so a two-foot set is a
    // four-pose cycle.
    private static int CycleLength(DirectionArt art)
    {
        int steps = art.stepFrames != null ? art.stepFrames.Length : 0;
        if (steps == 0) return art.neutral != null ? 1 : 0;
        return art.neutral != null ? steps * 2 : steps;
    }

    private void AdvancePhase(int poseCount, float speed, bool reverse)
    {
        float rate = posesPerSecond;
        if (scaleWithSpeed && nominalSpeed > 0.01f) rate *= speed / nominalSpeed;
        if (rate <= 0.01f) return;

        posePhase += Time.deltaTime * rate * (reverse ? -1f : 1f);
        // Repeat rather than a raw modulo: this has to stay in range when running backwards too,
        // and C# would leave the sign on a negative remainder.
        posePhase = Mathf.Repeat(posePhase, poseCount);
    }

    // Rises on the legs-together poses and falls on the steps. The peak is centred in the middle
    // of each pose's time on screen (hence the half-pose shift) rather than landing on the instant
    // the sprite swaps, so the bounce reads as part of the stride instead of a twitch at the cut.
    private void UpdateBob(bool walking)
    {
        float catchUp = 1f - Mathf.Exp(-bobBlendSpeed * Time.deltaTime);
        bobBlend = Mathf.Lerp(bobBlend, walking ? 1f : 0f, catchUp);

        float height = smoothBob
            ? Mathf.Cos(Mathf.PI * (posePhase - 0.5f))
            : (Mathf.FloorToInt(posePhase) % 2 == 0 ? 1f : -1f);

        // Offset in world space, not local: the root spins to face the mouse, so a local offset
        // would swing the bob around with the aim instead of lifting him up the screen.
        spriteRenderer.transform.position =
            transform.position + Vector3.up * (height * bobAmplitude * bobBlend);
    }

    private void UpdateShadow(Vector2 aim)
    {
        if (shadowRenderer == null) return;

        // Thrown opposite the flashlight, held upright like the character art, and deliberately
        // left out of the bob - the shadow belongs to the ground, so keeping it still while he
        // rises is what sells the lift.
        Transform st = shadowRenderer.transform;
        st.position = transform.position - (Vector3)(aim * shadowOffset);
        st.rotation = Quaternion.identity;
        st.localScale = Vector3.one * (scaleMultiplier * shadowScale);
    }

    // The shadow is a silhouette of the current pose, so it tracks the animated frame rather
    // than just the direction.
    private void ApplySprite(Sprite sprite)
    {
        if (sprite == null) return;
        spriteRenderer.sprite = sprite;
        if (shadowRenderer != null) shadowRenderer.sprite = sprite;
    }

    private float AimAngle()
    {
        Vector2 aim = player.FacingDirection;
        if (aim.sqrMagnitude < 0.0001f) return -90f;
        return Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
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
        if (slots == null || slot < 0 || slot >= slots.Length) return null;
        return HasArt(slots[slot]) ? slots[slot] : null;
    }

    private static bool HasArt(DirectionArt art)
    {
        if (art == null) return false;
        if (art.neutral != null) return true;
        return art.stepFrames != null && art.stepFrames.Length > 0;
    }

#if UNITY_EDITOR
    private const string ArtRoot = "Assets/Sprites/Player";

    // File names on disk, in the same order as the slots above.
    private static readonly string[] DirectionNames =
        { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

    /// <summary>
    /// Fills all 8 directions from the art folder so nobody has to drag 24 sprites by hand.
    /// Right-click the component header in the Inspector to run it. Lives here behind
    /// UNITY_EDITOR rather than in an Assets/Editor script so it adds no new folder.
    ///
    /// Expects "north.png" for the neutral pose and "north1.png", "north2.png", ... for the
    /// steps, counting up until one is missing.
    /// </summary>
    [ContextMenu("Auto-Populate From Art Folder")]
    private void AutoPopulateFromArtFolder()
    {
        var targets = new[] { north, northEast, east, southEast, south, southWest, west, northWest };
        int neutralCount = 0, stepCount = 0;

        for (int i = 0; i < targets.Length; i++)
        {
            string dir = DirectionNames[i];

            targets[i].neutral = LoadSprite($"{ArtRoot}/{dir}.png");
            if (targets[i].neutral != null) neutralCount++;

            var steps = new System.Collections.Generic.List<Sprite>();
            for (int step = 1; ; step++)
            {
                var sprite = LoadSprite($"{ArtRoot}/{dir}{step}.png");
                if (sprite == null) break;
                steps.Add(sprite);
            }

            targets[i].stepFrames = steps.ToArray();
            stepCount += steps.Count;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[PlayerDirectionalSprite] Populated {neutralCount} neutral poses and " +
                  $"{stepCount} step frames across 8 directions.", this);
    }

    // The typed load covers Single-mode textures, where the Sprite is the main asset. The fallback
    // covers Multiple-mode ones, where the Texture2D is the main asset and the slices hang off it
    // as sub-assets - there the typed call returns null and would quietly assign nothing.
    private static Sprite LoadSprite(string path)
    {
        var direct = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (direct != null) return direct;

        foreach (var obj in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is Sprite sprite) return sprite;
        }
        return null;
    }
#endif
}
