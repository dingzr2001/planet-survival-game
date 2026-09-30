namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// The height at which the ground is drawn. Gameplay happens on the flat plane; where the drawn ground
    /// dips or rises, whatever stands there is drawn at this height and pointer rays are resolved against it.
    /// </summary>
    public interface IGroundSurface
    {
        /// <summary>World-space height, in metres, at which the ground at (x, z) is drawn.</summary>
        float HeightAt(float x, float z);
    }
}
