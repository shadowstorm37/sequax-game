using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

namespace Game.Items
{
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemId itemId;

        [FormerlySerializedAs("Inventory")]
        [SerializeField] private Inventory inventory;

        [Tooltip("Drag your TextMeshPro component from your world Canvas here.")]
        [SerializeField] private TextMeshProUGUI interactionPromptText;

        private bool isPlayerInRange = false;

        /// <summary>
        /// Wires up an ItemPickup added at runtime (e.g. by ThrowController once a thrown item
        /// lands), which has no Inspector-assigned prompt text of its own. promptText is
        /// optional - pass null to behave like before (pickup still works, just silently).
        /// </summary>
        public void Initialize(ItemId id, TextMeshProUGUI promptText = null)
        {
            itemId = id;
            if (promptText != null) interactionPromptText = promptText;
        }

        private void Start()
        {
            if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (isPlayerInRange && Input.GetKeyDown(KeyCode.E) && inventory != null)
            {
                TryPickUp();
            }
        }

        private void TryPickUp()
        {
            if (inventory.AddItem(itemId))
            {
                if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
                gameObject.SetActive(false);
            }
            else
            {
                // If the inventory rejected the item, immediately change the text to reflect it
                UpdatePromptText(true);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (inventory == null)
            {
                inventory = other.GetComponent<Inventory>();
            }

            if (inventory == null || !other.CompareTag("Player")) return;

            isPlayerInRange = true;
            UpdatePromptText(false);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            isPlayerInRange = false;
            if (interactionPromptText != null) interactionPromptText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Evaluates the current inventory state and item category to display the correct prompt.
        /// </summary>
        private void UpdatePromptText(bool forcedFullLayout)
        {
            if (interactionPromptText == null) return;

            string displayName = ItemDatabase.GetDisplayName(itemId);
            ItemCategory category = ItemDatabase.GetCategory(itemId);

            // KeyItems bypass slot limits, so they are never blocked by a full inventory
            if (category == ItemCategory.KeyItem)
            {
                interactionPromptText.text = $"Press 'E' to pick up {displayName}";
                interactionPromptText.gameObject.SetActive(true);
                return;
            }

            // For throwables, check if we have room (either stacking or finding an empty slot)
            if (forcedFullLayout || !HasRoomForThrowable(itemId))
            {
                interactionPromptText.text = $"Inventory Full! Cannot pick up {displayName}";
            }
            else
            {
                interactionPromptText.text = $"Press 'E' to pick up {displayName}";
            }

            interactionPromptText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Simulates the slot checking logic inside Inventory.cs to see if a dynamic addition will succeed.
        /// </summary>
        private bool HasRoomForThrowable(ItemId id)
        {
            if (inventory == null) return false;

            // check if it can stack into an existing slot
            foreach (var slot in inventory.Slots)
            {
                if (slot != null && slot.itemId == id)
                {
                    return true;
                }
            }

            // Check if there is a completely vacant slot left
            foreach (var slot in inventory.Slots)
            {
                if (slot == null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
