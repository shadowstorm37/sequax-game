using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace Game.Items
{
    public class InventoryUI : MonoBehaviour
    {
        [System.Serializable]
        private struct ItemIconMapping
        {
            public ItemId itemId;
            public Sprite itemSprite;
        }

        [Header("References")]
        [SerializeField] private Inventory inventory;
        [SerializeField] private ThrowController throwController;

        [Header("Visual Configurations")]
        [Tooltip("Map each ItemId to its UI Sprite art here.")]
        [SerializeField] private List<ItemIconMapping> itemIcons = new List<ItemIconMapping>();

        [Header("Standard 4 Inventory Slots")]
        [Tooltip("Assign your 4 UI Images representing inventory hotbar slots.")]
        [SerializeField] private List<Image> slotIconImages = new List<Image>(Inventory.SlotCount);
        [Tooltip("Assign your 4 UI Text objects for stack counts.")]
        [SerializeField] private List<TMP_Text> slotCountTexts = new List<TMP_Text>(Inventory.SlotCount);
        [Tooltip("Assign your 4 UI GameObjects/Borders that highlight when selected.")]
        [SerializeField] private List<GameObject> slotHighlights = new List<GameObject>(Inventory.SlotCount);

        [Header("5th Slot: Currently Held/Selected Item")]
        [Tooltip("The UI Image for the separate, dedicated active/held item slot.")]
        [SerializeField] private Image heldItemIconImage;
        [Tooltip("Optional Text for the held item's stack size.")]
        [SerializeField] private TMP_Text heldItemCountText;
        [Tooltip("Optional Text to display the item's clean name (e.g., 'Rock').")]
        [SerializeField] private TMP_Text heldItemNameText;

        private readonly Dictionary<ItemId, Sprite> iconMap = new Dictionary<ItemId, Sprite>();
        private int lastHighlightedSlot = -1;

        private void Awake()
        {
            // Explicitly reset and rebuild the lookup map fresh on game startup
            iconMap.Clear();

            if (itemIcons != null)
            {
                for (int i = 0; i < itemIcons.Count; i++)
                {
                    var mapping = itemIcons[i];

                    // Skip any unassigned entries or empty slots in the inspector list
                    if (mapping.itemSprite == null)
                    {
                        Debug.LogWarning($"[Inventory UI Layout] Item ID '{mapping.itemId}' at index {i} has no Sprite assigned in the Inspector!");
                        continue;
                    }

                    if (!iconMap.ContainsKey(mapping.itemId))
                    {
                        iconMap.Add(mapping.itemId, mapping.itemSprite);
                    }
                    else
                    {
                        Debug.LogWarning($"[Inventory UI Layout] Duplicate mapping found for Item ID '{mapping.itemId}'. Skipping duplicate entry.");
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (inventory != null)
            {
                inventory.OnSlotChanged += UpdateSlotVisuals;
            }
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.OnSlotChanged -= UpdateSlotVisuals;
            }
        }

        private void Start()
        {
            // Rebuild our runtime map one extra time just in case script initialization order shifts
            Awake();

            // Fully render hotbar layouts on startup
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                UpdateSlotVisuals(i);
            }
            UpdateSelectionAndHeldVisuals(true);
        }

        private void Update()
        {
            // Continuously sync selection state changes and held items
            UpdateSelectionAndHeldVisuals(false);
        }

        /// <summary>Updates individual slot icons/counts automatically whenever structural inventory changes.</summary>
        private void UpdateSlotVisuals(int slotIndex)
        {
            if (inventory == null || slotIndex < 0 || slotIndex >= Inventory.SlotCount) return;

            // Ensure our UI lists match up structurally
            bool hasValidIconImage = slotIndex < slotIconImages.Count && slotIconImages[slotIndex] != null;
            bool hasValidCountText = slotIndex < slotCountTexts.Count && slotCountTexts[slotIndex] != null;

            var slot = inventory.Slots[slotIndex];

            // Case A: The data slot is empty or invalid
            if (slot == null || slot.IsEmpty)
            {
                if (hasValidIconImage)
                {
                    slotIconImages[slotIndex].sprite = null;
                    // Instead of disabling the component, let's keep it enabled but fully transparent 
                    // This prevents canvas redraw bugs and layout dropouts
                    var clr = slotIconImages[slotIndex].color;
                    clr.a = 0f;
                    slotIconImages[slotIndex].color = clr;
                }

                if (hasValidCountText)
                {
                    slotCountTexts[slotIndex].text = "";
                }

                if (throwController != null && slotIndex == throwController.SelectedSlot)
                {
                    lastHighlightedSlot = -1;
                }
                return;
            }

            // Case B: An item is sitting in this slot!
            if (hasValidIconImage)
            {
                if (iconMap.TryGetValue(slot.itemId, out Sprite sprite))
                {
                    slotIconImages[slotIndex].sprite = sprite;

                    // Bring the visibility back to 100% alpha opacity
                    var clr = slotIconImages[slotIndex].color;
                    clr.a = sprite != null ? 1f : 0f;
                    slotIconImages[slotIndex].color = clr;
                }
                else
                {
                    // LOUD DIAGNOSTIC CRASH WARNING
                    Debug.LogError($"[Inventory UI Error] You picked up {slot.itemId}, but this item is completely missing from the 'Item Icons' configuration list on your InventoryUI component script in the Inspector!");

                    slotIconImages[slotIndex].sprite = null;
                    var clr = slotIconImages[slotIndex].color;
                    clr.a = 0f;
                    slotIconImages[slotIndex].color = clr;
                }
            }

            if (hasValidCountText)
            {
                slotCountTexts[slotIndex].text = slot.count > 1 ? slot.count.ToString() : "";
            }

            if (throwController != null && slotIndex == throwController.SelectedSlot)
            {
                lastHighlightedSlot = -1;
            }
        }

        /// <summary>Syncs the highlights and mirrors active data to the 5th 'Held Item' display panel.</summary>
        private void UpdateSelectionAndHeldVisuals(bool forceRefresh)
        {
            if (throwController == null || inventory == null) return;

            int currentSelection = throwController.SelectedSlot;

            // Only recalculate UI elements if the selection index shifted or data was flagged as dirty
            if (currentSelection == lastHighlightedSlot && !forceRefresh) return;
            lastHighlightedSlot = currentSelection;

            // 1. Handle standard hotbar highlight borders
            for (int i = 0; i < slotHighlights.Count; i++)
            {
                if (slotHighlights[i] != null)
                {
                    slotHighlights[i].SetActive(i == currentSelection);
                }
            }

            // 2. Populate the 5th "Held/Selected Item" slot panel
            var selectedSlotData = inventory.Slots[currentSelection];

            if (selectedSlotData == null || selectedSlotData.IsEmpty)
            {
                // Clear the 5th slot if nothing is held in the active slot
                if (heldItemIconImage != null)
                {
                    heldItemIconImage.sprite = null;
                    heldItemIconImage.enabled = false;
                }
                if (heldItemCountText != null) heldItemCountText.text = "";
                if (heldItemNameText != null) heldItemNameText.text = "Empty Hands";
            }
            else
            {
                // Draw the mirrored active item configuration
                if (heldItemIconImage != null)
                {
                    if (iconMap.TryGetValue(selectedSlotData.itemId, out Sprite sprite))
                    {
                        heldItemIconImage.sprite = sprite;
                        heldItemIconImage.enabled = sprite != null;
                    }
                }

                if (heldItemCountText != null)
                {
                    heldItemCountText.text = selectedSlotData.count > 1 ? selectedSlotData.count.ToString() : "";
                }

                if (heldItemNameText != null)
                {
                    // Using ToString() temporarily until an ItemDatabase lookup is implemented
                    heldItemNameText.text = selectedSlotData.itemId.ToString();
                }
            }
        }
    }
}