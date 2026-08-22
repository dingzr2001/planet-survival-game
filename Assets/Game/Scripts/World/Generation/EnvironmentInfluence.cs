namespace PlanetSurvival.World.Generation
{
    public readonly struct EnvironmentSample
    {
        public EnvironmentSample(float elapsedGameHours, float timeOfDay, float altitude, float oxygenRatio)
        {
            ElapsedGameHours = elapsedGameHours;
            TimeOfDay = timeOfDay;
            Altitude = altitude;
            OxygenRatio = oxygenRatio;
        }

        public float ElapsedGameHours { get; }
        public float TimeOfDay { get; }
        public float Altitude { get; }
        public float OxygenRatio { get; }
    }

    public interface IEnvironmentInfluence
    {
        void Apply(in EnvironmentSample sample);
    }
}
