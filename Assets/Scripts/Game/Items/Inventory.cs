using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Items
{
    public class Inventory : MonoBehaviour
    {
        public const int SlotCount = 4;

        [Serializable]
        public class Slot
        {
            public ItemId itemId;
            public int count;
            public bool IsEmpty => count <= 0;
        }

        private readonly Slot[] slots = new Slot[SlotCount];
        private readonly HashSet<ItemId> keyItems = new HashSet<ItemId>();

        public IReadOnlyList<Slot> Slots => slots;
        public IReadOnlyCollection<ItemId> KeyItems => keyItems;

        /// <summary>Fired with the slot index whenever a slot's contents change (added, stacked, or removed). UI hooks into this.</summary>
        public event Action<int> OnSlotChanged;
        public event Action<ItemId> OnKeyItemAdded;

        public bool HasCarKeys => keyItems.Contains(ItemId.CarKeys);

        public bool HasCarjackParts =>
            keyItems.Contains(ItemId.ElectricalTape) &&
            keyItems.Contains(ItemId.Screwdriver) &&
            keyItems.Contains(ItemId.WireCutters);

        /// <summary>Win condition: car keys, or all three carjack parts.</summary>
        public bool CanEscape => HasCarKeys || HasCarjackParts;

        public bool AddItem(ItemId itemId)
        {
            if (ItemDatabase.GetCategory(itemId) == ItemCategory.KeyItem)
            {
                if (keyItems.Add(itemId))
                {
                    OnKeyItemAdded?.Invoke(itemId);
                }
                return true; // key items are carried outside the inventory slots, so they always "fit"
            }

            // Stack onto an existing slot of the same item type first.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].itemId == itemId)
                {
                    slots[i].count++;
                    OnSlotChanged?.Invoke(i);
                    return true;
                }
            }

            // Otherwise take the first empty slot.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = new Slot { itemId = itemId, count = 1 };
                    OnSlotChanged?.Invoke(i);
                    return true;
                }
            }

            return false; // all slots full and none matched
        }

        /// <summary>Removes one item from the given slot. Returns false if the slot is empty or out of range.</summary>
        public bool RemoveFromSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return false;

            Slot slot = slots[index];
            if (slot == null || slot.IsEmpty) return false;

            slot.count--;
            if (slot.count <= 0)
            {
                slots[index] = null;
            }

            OnSlotChanged?.Invoke(index);
            return true;
        }
    }
}
