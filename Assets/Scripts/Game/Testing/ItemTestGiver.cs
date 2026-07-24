using Game.Items;
using UnityEngine;

/// <summary>
/// TEST HARNESS - not part of the real game. Delete or disable once item
/// pickups are actually placed in levels.
///
/// Press G to add a Rock to the target Inventory, for testing throw logic
/// without needing a real ItemPickup in the scene.
/// </summary>
public class ItemTestGiver : MonoBehaviour
{
    [SerializeField] private Inventory inventory;
    [SerializeField] private ItemId testItem = ItemId.Rock;
    [SerializeField] private KeyCode giveKey = KeyCode.G;

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<Inventory>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(giveKey))
        {
            if (inventory == null)
            {
                Debug.LogWarning("ItemTestGiver: no Inventory assigned/found.");
                return;
            }

            bool added = inventory.AddItem(testItem);
            Debug.Log(added
                ? $"[ItemTestGiver] Added {testItem} to inventory."
                : $"[ItemTestGiver] Could not add {testItem} (inventory full?).");
        }
    }
}
