using System.Collections.Generic;
using UnityEngine;

namespace Game.Items
{
    public class Inventory : MonoBehaviour
    {
        [SerializeField] private int slotCount = 6;

        private readonly List<string> items = new List<string>();

        public IReadOnlyList<string> Items => items;
        public bool IsFull => items.Count >= slotCount;

        public bool AddItem(string itemName)
        {
            if (IsFull) return false;

            items.Add(itemName);
            return true;
        }

        public bool RemoveItem(string itemName)
        {
            return items.Remove(itemName);
        }
    }
}
