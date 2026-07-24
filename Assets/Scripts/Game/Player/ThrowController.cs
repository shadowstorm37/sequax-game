using System;
using System.Collections;
using Game.Items;
using UnityEngine;

[RequireComponent(typeof(Inventory))]
public class ThrowController : MonoBehaviour
{
    [Serializable]
    private struct ThrowVisual
    {
        public ItemId itemId;
        public GameObject prefab; // world-space object with a SpriteRenderer, e.g. a rock sprite
    }

    [SerializeField] private PlayerScript playerScript;
    [SerializeField] private float maxThrowRange = 8f;
    [SerializeField] private float throwLoudness = 8f;
    [SerializeField] private float throwSpeed = 12f; // world units/sec the visual travels at
    [SerializeField] private float lingerAfterLanding = 0.2f; // time the sprite stays visible once it lands, before despawning
    [SerializeField] private ThrowVisual[] throwVisuals;

    [Header("Throw Audio")]
    [Tooltip("One ThrowableItemAudio per item, each with its own AudioSource/clip assigned in the Inspector.")]
    [SerializeField] private ThrowableItemAudio[] throwAudioSources;

    [Header("Obstacle Blocking")]
    [Tooltip("Same Obstacles layer the vision cone occludes against - throws are blocked by it too. " +
             "Layers not in this mask (e.g. Default) never block a throw, so items can sail over them.")]
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("Clamped-Target Marker")]
    [Tooltip("Only shown when the target had to be pulled in short of the cursor (max range or an obstacle). " +
             "The cursor itself is the reticle otherwise - this just marks where the throw actually lands when it can't reach the cursor.")]
    [SerializeField] private Transform clampedTargetMarker;
    [SerializeField] private SpriteRenderer clampedTargetMarkerRenderer; // optional, for the clamp-reason color below
    [SerializeField] private Color rangeClampedColor = Color.white;
    [SerializeField] private Color obstacleClampedColor = Color.red;

    private static readonly KeyCode[] SlotKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4
    };

    private Inventory inventory;
    private Camera mainCamera;
    private bool isAiming;
    private int selectedSlot;

    /// <summary>True while the aim/throw button is held. Art/animation can poll this or subscribe to OnAimingChanged.</summary>
    public bool IsAiming => isAiming;
    public int SelectedSlot => selectedSlot;

    /// <summary>Fired once when aiming starts and once when it ends (release, throw, or cancel). Hook animations/sprites here.</summary>
    public event Action<bool> OnAimingChanged;

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        if (playerScript == null) playerScript = GetComponent<PlayerScript>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        UpdateSlotSelection();
        UpdateAimingState();
        UpdateClampedTargetMarker();

        if (isAiming && Input.GetMouseButtonDown(0))
        {
            Throw();
        }
    }

    private void UpdateAimingState()
    {
        if (Input.GetMouseButtonDown(1))
        {
            SetAiming(true);
        }
        else if (Input.GetMouseButtonUp(1))
        {
            SetAiming(false);
        }
    }

    private void SetAiming(bool value)
    {
        if (isAiming == value) return;
        isAiming = value;
        OnAimingChanged?.Invoke(isAiming);
    }

    private void UpdateSlotSelection()
    {
        for (int i = 0; i < SlotKeys.Length; i++)
        {
            if (Input.GetKeyDown(SlotKeys[i]))
            {
                selectedSlot = i;
                break;
            }
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f)
        {
            selectedSlot = (selectedSlot - 1 + Inventory.SlotCount) % Inventory.SlotCount;
        }
        else if (scroll < 0f)
        {
            selectedSlot = (selectedSlot + 1) % Inventory.SlotCount;
        }
    }

    private void UpdateClampedTargetMarker()
    {
        if (clampedTargetMarker == null) return;

        if (!isAiming)
        {
            clampedTargetMarker.gameObject.SetActive(false);
            return;
        }

        Vector2 origin = transform.position;
        Vector2 target = GetThrowTarget(origin, out ClampReason reason);

        clampedTargetMarker.gameObject.SetActive(reason != ClampReason.None);
        if (reason == ClampReason.None) return;

        clampedTargetMarker.position = target;
        if (clampedTargetMarkerRenderer != null)
        {
            clampedTargetMarkerRenderer.color = reason == ClampReason.Obstacle ? obstacleClampedColor : rangeClampedColor;
        }
    }

    private enum ClampReason { None, Range, Obstacle }

    /// <summary>
    /// The cursor world position, range-clamped, then further clamped to the nearest
    /// Obstacles-layer hit along that path. reason reports which clamp (if any) applied,
    /// so callers only need to show a marker when the target actually differs from the cursor.
    /// </summary>
    private Vector2 GetThrowTarget(Vector2 origin, out ClampReason reason)
    {
        reason = ClampReason.None;

        Vector2 mouseWorldPos = GetMouseWorldPosition();
        Vector2 toMouse = mouseWorldPos - origin;
        float mouseDistance = toMouse.magnitude;

        if (mouseDistance < 0.0001f) return origin;

        Vector2 direction = toMouse / mouseDistance;
        float rangeClampedDistance = Mathf.Min(mouseDistance, maxThrowRange);
        if (rangeClampedDistance < mouseDistance) reason = ClampReason.Range;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, rangeClampedDistance, obstacleLayerMask);
        if (hit.collider != null)
        {
            reason = ClampReason.Obstacle;
            return hit.point;
        }

        return origin + direction * rangeClampedDistance;
    }

    private void Throw()
    {
        Inventory.Slot slot = inventory.Slots[selectedSlot];
        if (slot == null || slot.IsEmpty) return;

        ItemId itemId = slot.itemId;
        Vector2 origin = transform.position;
        Vector2 target = GetThrowTarget(origin, out _);

        if (!inventory.RemoveFromSlot(selectedSlot)) return;

        SetAiming(false);
        StartCoroutine(AnimateThrow(itemId, origin, target));
    }

    private IEnumerator AnimateThrow(ItemId itemId, Vector2 origin, Vector2 target)
    {
        GameObject prefab = GetVisualPrefab(itemId);
        GameObject visual = prefab != null ? Instantiate(prefab, origin, Quaternion.identity) : null;

        float duration = Vector2.Distance(origin, target) / throwSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (visual != null)
            {
                visual.transform.position = Vector2.Lerp(origin, target, elapsed / duration);
            }
            yield return null;
        }

        if (visual != null)
        {
            visual.transform.position = target;
        }

        ThrowableItemAudio throwAudio = GetThrowAudio(itemId);
        if (throwAudio != null)
        {
            throwAudio.transform.SetParent(null, true);
            throwAudio.transform.position = target;
            throwAudio.Play();
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.EmitSound(target, throwLoudness, ItemDatabase.GetThrowSound(itemId), gameObject);
        }

        if (visual != null)
        {
            Destroy(visual, lingerAfterLanding);
        }
    }

    private GameObject GetVisualPrefab(ItemId itemId)
    {
        foreach (var visual in throwVisuals)
        {
            if (visual.itemId == itemId) return visual.prefab;
        }
        return null;
    }

    private ThrowableItemAudio GetThrowAudio(ItemId itemId)
    {
        foreach (var audio in throwAudioSources)
        {
            if (audio != null && audio.ItemId == itemId) return audio;
        }
        return null;
    }

    private Vector2 GetMouseWorldPosition()
    {
        if (mainCamera == null) return transform.position;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        return mainCamera.ScreenToWorldPoint(mouseScreenPos);
    }
}
