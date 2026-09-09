namespace PlanetSurvival.Building.Domain
{
    public enum BuildFailure
    {
        None,

        /// <summary>The buildable asset is missing or misconfigured.</summary>
        InvalidBuildable,

        /// <summary>The backpack does not hold the full construction cost.</summary>
        MissingResources,

        /// <summary>Another site, building, or obstacle already covers one of the cells.</summary>
        Blocked,

        /// <summary>The chosen spot is out of the player's building reach.</summary>
        OutOfReach,

        /// <summary>The refunded materials do not fit back into the backpack.</summary>
        InventoryFull,

        /// <summary>The site is finished, so it can no longer be cancelled.</summary>
        NotUnderConstruction
    }
}
