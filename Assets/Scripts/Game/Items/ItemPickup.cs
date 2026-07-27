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

        /// <summary>Sets which item this pickup grants. Used when spawning a pickup at runtime (e.g. a thrown phone left on the ground).</summary>
        public void Initialize(ItemId id)
        {
            itemId = id;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (inventory == null)
            {
                inventory = other.GetComponent<Inventory>();
            }

            if (inventory == null || !other.CompareTag("Player")) return;

            if (inventory.AddItem(itemId))
            {
                gameObject.SetActive(false);
            }
        }
    }
}
