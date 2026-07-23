namespace Game.Items
{
    /// <summary>
    /// Throwables take up an inventory slot. Key items are carried permanently
    /// once picked up and never occupy a slot - they're progression flags, not inventory.
    /// </summary>
    public enum ItemCategory
    {
        Throwable,
        KeyItem
    }

    public enum ItemId
    {
        // Throwables
        Rock,
        Bottle,
        Phone,
        Radio,

        // Key items
        CarKeys,
        ElectricalTape,
        Screwdriver,
        WireCutters
    }

    /// <summary>
    /// Static lookup for item metadata. Single source of truth for which items
    /// are throwable vs. key items, and what sound each throwable makes on impact.
    /// </summary>
    public static class ItemDatabase
    {
        public static ItemCategory GetCategory(ItemId id) => id switch
        {
            ItemId.Rock or ItemId.Bottle or ItemId.Phone or ItemId.Radio => ItemCategory.Throwable,
            _ => ItemCategory.KeyItem
        };

        /// <summary>Sound emitted when a throwable lands. Throws for key items - they're never thrown.</summary>
        public static SoundType GetThrowSound(ItemId id) => id switch
        {
            ItemId.Rock => SoundType.ThrowImpact,
            ItemId.Bottle => SoundType.GlassBreak,
            ItemId.Phone => SoundType.Phone,
            ItemId.Radio => SoundType.Radio,
            _ => throw new System.ArgumentException($"{id} is not throwable and has no throw sound.")
        };

        public static string GetDisplayName(ItemId id) => id switch
        {
            ItemId.CarKeys => "Car Keys",
            ItemId.ElectricalTape => "Electrical Tape",
            ItemId.Screwdriver => "Screwdriver",
            ItemId.WireCutters => "Wire Cutters",
            _ => id.ToString()
        };
    }
}
