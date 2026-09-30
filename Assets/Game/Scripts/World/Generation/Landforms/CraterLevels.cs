namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Raw elevations a crater drives its floor and rim towards. They are derived from the seed's
    /// measured thresholds, so a crater floor is guaranteed to read as lake or basin and a blocking rim
    /// as mountain whatever the noise range of that seed.
    /// </summary>
    public readonly struct CraterLevels
    {
        public CraterLevels(float iceFloor, float dryFloor, float lowRim, float blockingRim)
        {
            IceFloor = iceFloor;
            DryFloor = dryFloor;
            LowRim = lowRim;
            BlockingRim = blockingRim;
        }

        public float IceFloor { get; }
        public float DryFloor { get; }
        public float LowRim { get; }
        public float BlockingRim { get; }
    }
}
