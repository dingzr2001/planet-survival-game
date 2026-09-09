namespace PlanetSurvival.Farming.Domain
{
    public enum FarmingFailure
    {
        None,
        InvalidCrop,

        /// <summary>Something is already growing in the slot.</summary>
        SlotOccupied,

        /// <summary>The slot was asked for a harvest but holds no crop.</summary>
        SlotEmpty,
        MissingSeed,
        NotEnoughWater,

        /// <summary>The crop is planted but has not finished growing.</summary>
        NotRipe,
        InventoryFull
    }
}
