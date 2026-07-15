using UnityEngine;

/// <summary>
/// Swaps the player's visible sprite to match the 8-way aim direction (mouse).
///
/// PlayerScript rotates the root transform to face the mouse (that rotation drives the
/// vision cone and the enemy's evasion via the player's transform.up). The directional art
/// is pre-drawn per facing, so it must NOT also be spun by that transform or it would
/// double-rotate. This creates a child renderer held world-upright and expresses facing by
/// swapping to the sprite for the current aim octant, hiding the placeholder hexagon on the
/// root.
///
/// Only the "down" facing sprites exist so far; any unassigned direction falls back to the
/// nearest assigned sprite by angle, so it degrades gracefully and the remaining art can be
/// dropped into the Inspector later without touching this script.
/// </summary>
[RequireComponent(typeof(PlayerScript))]
public class PlayerDirectionalSprite : MonoBehaviour
{
    [Header("Directional Sprites")]
    [Tooltip("Assign the ones you have; empty slots borrow the nearest assigned sprite.")]
    [SerializeField] private Sprite up;
    [SerializeField] private Sprite upRight;
    [SerializeField] private Sprite right;
    [SerializeField] private Sprite downRight;
    [SerializeField] private Sprite down;
    [SerializeField] private Sprite downLeft;
    [SerializeField] private Sprite left;
    [SerializeField] private Sprite upLeft;

    // Canonical direction (degrees, atan2 convention) for each slot below, matched by index.
    private static readonly float[] SlotAngles = { 90f, 45f, 0f, -45f, -90f, -135f, 180f, 135f };

    private PlayerScript player;
    private SpriteRenderer spriteRenderer;
    private Sprite[] slotSprites;
    private Sprite currentSprite;

    private void Awake()
    {
        player = GetComponent<PlayerScript>();
        slotSprites = new[] { up, upRight, right, downRight, down, downLeft, left, upLeft };

        spriteRenderer = CreateUprightRenderer();

        // Match the starting aim direction so we don't flash a wrong-facing sprite on spawn.
        currentSprite = PickSprite(player.FacingDirection);
        spriteRenderer.sprite = currentSprite;
    }

    // Move the visible art onto a child we hold upright, and hide the placeholder hexagon
    // on the root (which spins with the aim direction).
    private SpriteRenderer CreateUprightRenderer()
    {
        var rootRenderer = GetComponent<SpriteRenderer>();

        var child = new GameObject("HikerSprite");
        child.transform.SetParent(transform, false);

        var sr = child.AddComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            sr.sortingLayerID = rootRenderer.sortingLayerID;
            sr.sortingOrder = rootRenderer.sortingOrder;
            sr.sharedMaterial = rootRenderer.sharedMaterial;
            sr.maskInteraction = rootRenderer.maskInteraction;
            rootRenderer.enabled = false;
        }
        return sr;
    }

    private void LateUpdate()
    {
        // Keep the art world-upright; the parent transform spins toward the mouse (that drives
        // the vision cone and enemy evasion), but facing is expressed by swapping to the
        // matching directional sprite, not by tilting this one.
        spriteRenderer.transform.rotation = Quaternion.identity;

        Vector2 aim = player.FacingDirection;
        if (aim.sqrMagnitude < 0.0001f) return;

        Sprite next = PickSprite(aim);
        if (next != currentSprite)
        {
            currentSprite = next;
            spriteRenderer.sprite = next;
        }
    }

    // Nearest assigned sprite to the aim angle, so missing directions borrow the
    // closest one that exists. Returns null only if nothing is assigned at all.
    private Sprite PickSprite(Vector2 aimDir)
    {
        float aimAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

        Sprite best = null;
        float bestDelta = float.MaxValue;
        for (int i = 0; i < slotSprites.Length; i++)
        {
            if (slotSprites[i] == null) continue;
            float delta = Mathf.Abs(Mathf.DeltaAngle(aimAngle, SlotAngles[i]));
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = slotSprites[i];
            }
        }
        return best;
    }
}
