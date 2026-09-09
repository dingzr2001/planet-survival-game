namespace PlanetSurvival.Crafting.Domain
{
    public enum CraftingFailure
    {
        None,
        InvalidRecipe,
        MissingCondition,
        MissingIngredients,
        InventoryFull,

        /// <summary>A timed station is already working on, or holding the output of, another craft.</summary>
        StationBusy,

        /// <summary>A timed station was asked for an output it has not finished.</summary>
        NothingReady
    }
}
