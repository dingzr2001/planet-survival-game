using PlanetSurvival.World.Generation.Landforms;
using PlanetSurvival.World.Presentation;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// The height at which the ground is drawn: sunk into craters (<see cref="CraterRelief"/>), flat
    /// elsewhere. Heights are built as tall as the mountain meshes, <see cref="VerticalScale"/> times their
    /// true size, so a crater looks as deep beside a sprite as a cliff looks high.
    /// </summary>
    public sealed class TerrainSurface : IGroundSurface
    {
        private readonly CraterField _craters;

        public TerrainSurface(CraterField craters, float verticalScale)
        {
            _craters = craters;
            VerticalScale = verticalScale < 1f ? 1f : verticalScale;
        }

        public CraterField Craters => _craters;
        public float VerticalScale { get; }

        public float HeightAt(float x, float z)
        {
            if (_craters == null || !_craters.TryGetNearest(x, z, out Crater crater, out float relative))
            {
                return 0f;
            }

            return CraterRelief.HeightAt(crater, relative) * VerticalScale;
        }
    }
}
