namespace PlanetSurvival.Water.Domain
{
    public enum WaterProcessorState
    {
        /// <summary>Nothing is loaded and no water is waiting; the machine accepts a new batch of ice.</summary>
        Idle,

        /// <summary>A batch of ice is being purified and will finish at a fixed point of expedition time.</summary>
        Processing,

        /// <summary>Potable water is held in the machine until it is poured into a tank.</summary>
        Ready
    }
}
