using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Items
{
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemId itemId;

        [FormerlySerializedAs("Inventory")]
        [SerializeField] private Inventory inventory;

        [Tooltip("The 'Press E to Pick Up' prompt graphic.")]
        [SerializeField] private GameObject interactionPrompt;

        private bool isPlayerInRange = false;

        /// <summary>Sets which item this pickup grants. Used when spawning a pickup at runtime (e.g. a thrown phone left on the ground).</summary>
        public void Initialize(ItemId id)
        {
            itemId = id;
        }

        private void Start()
        {
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
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
                gameObject.SetActive(false);
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
            if (interactionPrompt != null) interactionPrompt.SetActive(true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            isPlayerInRange = false;
            if (interactionPrompt != null) interactionPrompt.SetActive(false);
        }
    }
}
