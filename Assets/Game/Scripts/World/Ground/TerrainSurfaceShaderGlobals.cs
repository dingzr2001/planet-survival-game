using System.Collections.Generic;
using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Hands the craters around the player to every shader that draws on the ground (TerrainSurface.cginc),
    /// so the ground, the sprites and the rocks standing on it all sink by the same amount. Global shader
    /// state, owned by the <see cref="TerrainChunkStreamer"/> that follows the player.
    /// </summary>
    public static class TerrainSurfaceShaderGlobals
    {
        /// <summary>Most craters handed to the shaders at once; matches SURFACE_MAX_CRATERS.</summary>
        public const int MaximumCraters = 32;

        /// <summary>
        /// Regions around the player whose craters are handed over. Two rings of crater regions reach far
        /// beyond the widest view, so nothing on screen is drawn flat where it should dip.
        /// </summary>
        public const int RegionRadius = 2;

        private static readonly int CratersId = Shader.PropertyToID("_SurfaceCraters");
        private static readonly int CraterCountId = Shader.PropertyToID("_SurfaceCraterCount");
        private static readonly int VerticalScaleId = Shader.PropertyToID("_SurfaceVerticalScale");
        private static readonly int ProfileId = Shader.PropertyToID("_SurfaceCraterProfile");
        private static readonly int ReachId = Shader.PropertyToID("_SurfaceCraterReach");

        private static readonly Vector4[] Buffer = new Vector4[MaximumCraters];
        private static readonly List<Crater> Nearby = new();

        /// <summary>Uploads the craters around (x, z), or clears them when <paramref name="surface"/> is null.</summary>
        public static void Upload(TerrainSurface surface, float x, float z)
        {
            Nearby.Clear();
            surface?.Craters?.CollectNear(x, z, RegionRadius, Nearby);
            int count = Mathf.Min(Nearby.Count, MaximumCraters);
            if (Nearby.Count > MaximumCraters)
            {
                Debug.LogWarning($"{Nearby.Count} craters lie around ({x:0}, {z:0}); only {MaximumCraters} are drawn sunken.");
            }

            for (int i = 0; i < MaximumCraters; i++)
            {
                Buffer[i] = i < count
                    ? new Vector4(Nearby[i].CenterX, Nearby[i].CenterZ, Nearby[i].Radius, 0f)
                    : Vector4.zero;
            }

            Shader.SetGlobalVectorArray(CratersId, Buffer);
            Shader.SetGlobalFloat(CraterCountId, count);
            Shader.SetGlobalFloat(VerticalScaleId, surface != null ? surface.VerticalScale : 1f);
            Shader.SetGlobalVector(ProfileId, new Vector4(
                CraterRelief.Depth, CraterRelief.RimHeight, CraterRelief.RimWidth, CraterRelief.WallStart));
            Shader.SetGlobalFloat(ReachId, CraterRelief.Reach);
        }
    }
}
