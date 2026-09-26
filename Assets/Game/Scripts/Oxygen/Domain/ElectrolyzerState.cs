namespace PlanetSurvival.Oxygen.Domain
{
    /// <summary>Why a water electrolyzer is or is not splitting water right now.</summary>
    public enum ElectrolyzerState
    {
        /// <summary>Water and power are present and oxygen is being produced.</summary>
        Producing,

        /// <summary>The feed tank is dry; load ice or pour water in.</summary>
        NeedsWater,

        /// <summary>The input buffer is empty; connect it to a power pole.</summary>
        NeedsPower,

        /// <summary>The vent tank is full. Hydrogen has to be released before electrolysis resumes.</summary>
        HydrogenStorageFull,

        /// <summary>Both the gas buffer and the bottled-oxygen bin are full.</summary>
        OxygenStorageFull
    }
}
