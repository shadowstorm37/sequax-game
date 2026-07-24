using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
        [SerializeField] private List<Text> slotCountTexts = new List<Text>(Inventory.SlotCount);
        [Tooltip("Assign your 4 UI GameObjects/Borders that highlight when selected.")]
        [SerializeField] private List<GameObject> slotHighlights = new List<GameObject>(Inventory.SlotCount);

        [Header("5th Slot: Currently Held/Selected Item")]
        [Tooltip("The UI Image for the separate, dedicated active/held item slot.")]
        [SerializeField] private Image heldItemIconImage;
        [Tooltip("Optional Text for the held item's stack size.")]
        [SerializeField] private Text heldItemCountText;
        [Tooltip("Optional Text to display the item's clean name (e.g., 'Rock').")]
        [SerializeField] private Text heldItemNameText;

        private readonly Dictionary<ItemId, Sprite> iconMap = new Dictionary<ItemId, Sprite>();
        private int lastHighlightedSlot = -1;

        private void Awake()
        {
            // Convert list mapping to a fast runtime dictionary lookup
            foreach (var mapping in itemIcons)
            {
                if (!iconMap.ContainsKey(mapping.itemId))
                {
                    iconMap.Add(mapping.itemId, mapping.itemSprite);
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
            if (slotIndex < 0 || slotIndex >= slotIconImages.Count || inventory == null) return;

            var slot = inventory.Slots[slotIndex];

            // If slot data is null or completely depleted, clear the visuals
            if (slot == null || slot.IsEmpty)
            {
                slotIconImages[slotIndex].sprite = null;
                slotIconImages[slotIndex].enabled = false;
                if (slotIndex < slotCountTexts.Count && slotCountTexts[slotIndex] != null)
                {
                    slotCountTexts[slotIndex].text = "";
                }

                // Force an update to the held panel if our currently selected slot just emptied out
                if (throwController != null && slotIndex == throwController.SelectedSlot)
                {
                    lastHighlightedSlot = -1; // Reset to force redraw
                }
                return;
            }

            // Otherwise, read out matching item definitions
            if (iconMap.TryGetValue(slot.itemId, out Sprite sprite))
            {
                slotIconImages[slotIndex].sprite = sprite;
                slotIconImages[slotIndex].enabled = sprite != null;
            }

            // Display item stack sizes if count > 1
            if (slotIndex < slotCountTexts.Count && slotCountTexts[slotIndex] != null)
            {
                slotCountTexts[slotIndex].text = slot.count > 1 ? slot.count.ToString() : "";
            }

            // Force an update to the held panel if our currently selected slot data changed
            if (throwController != null && slotIndex == throwController.SelectedSlot)
            {
                lastHighlightedSlot = -1; // Reset to force redraw
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