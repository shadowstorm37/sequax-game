using UnityEngine;

namespace Game.Items
{
    [RequireComponent(typeof(Collider2D))]
    public class ItemPickup : MonoBehaviour
    {
        [SerializeField] private string itemName;
        [SerializeField] private Inventory Inventory;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Inventory == null)
            {
                Inventory = other.GetComponent<Inventory>();
            }

            if (Inventory == null || !other.CompareTag("Player")) return;

            if (Inventory.AddItem(itemName))
            {
                gameObject.SetActive(false);
            }
        }
    }
}
