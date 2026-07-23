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

    [Header("Aim Indicator")]
    [Tooltip("LineRenderer shown from the player to the landing point while aiming (right-click held).")]
    [SerializeField] private LineRenderer aimLine;

    private static readonly KeyCode[] SlotKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4
    };

    private Inventory inventory;
    private Camera mainCamera;
    private bool isAiming;
    private int selectedSlot;

    public bool IsAiming => isAiming;
    public int SelectedSlot => selectedSlot;

    private void Awake()
    {
        inventory = GetComponent<Inventory>();
        if (playerScript == null) playerScript = GetComponent<PlayerScript>();
        mainCamera = Camera.main;
    }

    private void Update()
    {
        UpdateSlotSelection();

        if (Input.GetMouseButtonDown(1))
        {
            isAiming = true;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            isAiming = false;
        }

        UpdateAimLine();

        if (isAiming && Input.GetMouseButtonDown(0))
        {
            Throw();
        }
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

    private void UpdateAimLine()
    {
        if (aimLine == null) return;

        aimLine.enabled = isAiming;
        if (!isAiming) return;

        Vector2 origin = transform.position;
        Vector2 target = GetThrowTarget(origin);
        aimLine.SetPosition(0, origin);
        aimLine.SetPosition(1, target);
    }

    private Vector2 GetThrowTarget(Vector2 origin)
    {
        // Clamp to the mouse distance so a close click lands short, not always at max range.
        float distance = Mathf.Min(GetMouseDistance(origin), maxThrowRange);
        return origin + playerScript.FacingDirection * distance;
    }

    private void Throw()
    {
        Inventory.Slot slot = inventory.Slots[selectedSlot];
        if (slot == null || slot.IsEmpty) return;

        ItemId itemId = slot.itemId;
        Vector2 origin = transform.position;
        Vector2 target = GetThrowTarget(origin);

        if (!inventory.RemoveFromSlot(selectedSlot)) return;

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

        GetThrowAudio(itemId)?.Play();
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

    private float GetMouseDistance(Vector2 origin)
    {
        if (mainCamera == null) return maxThrowRange;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(mouseScreenPos);

        return Vector2.Distance(origin, mouseWorldPos);
    }
}
