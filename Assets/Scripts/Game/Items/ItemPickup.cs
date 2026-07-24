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
