using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Impact craters stamped onto the relief. Noise cannot draw a round bowl with a rim, so craters are
    /// placed discretely instead: the plane is cut into square regions and each region's hash decides
    /// whether it holds one crater, where, and how large. A query only inspects the surrounding 3×3
    /// regions, so the result depends on nothing but the seed and the point.
    /// </summary>
    public sealed class CraterField
    {
        // A crater's influence ends this many rim widths beyond its crest; the rim profile is a Gaussian
        // and has fallen below 0.02 % there.
        private const float RimReachInWidths = 3f;

        // The bowl is flat out to this share of the radius, then rises to meet the rim.
        private const float FlatFloorShare = .6f;

        private const int ChanceSalt = 11;
        private const int RadiusSalt = 23;
        private const int OffsetXSalt = 37;
        private const int OffsetZSalt = 41;
        private const int IceSalt = 53;
        private const int GapCountSalt = 67;
        private const int GapAngleSalt = 71;
        private const int MaximumGapCount = 3;

        private readonly int _seed;
        private readonly LandformSettings _settings;
        private readonly Vector2 _exclusionCenter;
        private readonly float _exclusionRadius;

        /// <param name="exclusionCenter">Craters whose reach would touch this circle are not generated.</param>
        public CraterField(int seed, LandformSettings settings, Vector2 exclusionCenter, float exclusionRadius)
        {
            _seed = seed;
            _settings = settings;
            _exclusionCenter = exclusionCenter;
            _exclusionRadius = Mathf.Max(0f, exclusionRadius);
        }

        /// <summary>The crater held by one region, if any.</summary>
        public bool TryGetCrater(int regionX, int regionZ, out Crater crater)
        {
            crater = default;
            if (_settings == null || GradientNoise.Hash01(_seed + ChanceSalt, regionX, regionZ) >= _settings.CraterChance)
            {
                return false;
            }

            float cellSize = _settings.CraterCellSize;
            float radius = Mathf.Lerp(_settings.MinimumCraterRadius, _settings.MaximumCraterRadius,
                GradientNoise.Hash01(_seed + RadiusSalt, regionX, regionZ));
            float reach = ReachOf(radius);
            // Keep the whole crater inside its own region where possible so a neighbour never has to
            // be asked about more than its adjacent ring.
            float margin = Mathf.Min(reach, cellSize * .5f);
            float centerX = (regionX + Mathf.Lerp(margin / cellSize, 1f - margin / cellSize,
                GradientNoise.Hash01(_seed + OffsetXSalt, regionX, regionZ))) * cellSize;
            float centerZ = (regionZ + Mathf.Lerp(margin / cellSize, 1f - margin / cellSize,
                GradientNoise.Hash01(_seed + OffsetZSalt, regionX, regionZ))) * cellSize;

            float exclusionDistance = Vector2.Distance(new Vector2(centerX, centerZ), _exclusionCenter);
            if (exclusionDistance < reach + _exclusionRadius)
            {
                return false;
            }

            int gapCount = 1 + (int)(GradientNoise.Hash01(_seed + GapCountSalt, regionX, regionZ) * MaximumGapCount);
            crater = new Crater(
                centerX, centerZ, radius,
                GradientNoise.Hash01(_seed + IceSalt, regionX, regionZ) < _settings.CraterIceChance,
                radius >= _settings.BlockingRimMinRadius,
                Mathf.Min(gapCount, MaximumGapCount),
                GradientNoise.Hash01(_seed + GapAngleSalt, regionX, regionZ) * Mathf.PI * 2f);
            return true;
        }

        /// <summary>
        /// Share of the radius where a crater's inner wall leaves its flat floor. From here out to
        /// <see cref="SlopeReach"/> the ground is the crater's wall, rim and outer slope.
        /// </summary>
        public const float WallStart = .7f;

        /// <summary>Radii from the centre beyond which a crater leaves the ground untouched.</summary>
        public const float SlopeReach = 1.4f;

        /// <summary>
        /// True on a crater's wall, rim or outer slope, where the ground is drawn tilted: sheet ice, which is
        /// frozen standing water, never lies there.
        /// </summary>
        public bool IsOnSlope(float x, float z)
        {
            return TryGetNearest(x, z, out _, out float relative) && relative >= WallStart && relative < SlopeReach;
        }

        /// <summary>
        /// Adds every crater held by the regions within <paramref name="regionRadius"/> regions of the one
        /// containing the point.
        /// </summary>
        public void CollectNear(float x, float z, int regionRadius, List<Crater> into)
        {
            if (into == null || _settings == null || _settings.CraterChance <= 0f)
            {
                return;
            }

            float cellSize = _settings.CraterCellSize;
            int regionX = Mathf.FloorToInt(x / cellSize);
            int regionZ = Mathf.FloorToInt(z / cellSize);
            int radius = Mathf.Max(0, regionRadius);
            for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (TryGetCrater(regionX + offsetX, regionZ + offsetZ, out Crater crater))
                    {
                        into.Add(crater);
                    }
                }
            }
        }

        /// <summary>
        /// The crater whose rim is relatively closest to the point, measured in its own radii from its centre,
        /// among the craters of the point's region and its neighbours. Craters never reach further than a
        /// region, so this covers anything drawn around a crater out to about two radii.
        /// </summary>
        /// <returns>False when no crater lies in the neighbourhood.</returns>
        public bool TryGetNearest(float x, float z, out Crater nearest, out float relativeDistance)
        {
            nearest = default;
            relativeDistance = float.PositiveInfinity;
            if (_settings == null || _settings.CraterChance <= 0f)
            {
                return false;
            }

            float cellSize = _settings.CraterCellSize;
            int regionX = Mathf.FloorToInt(x / cellSize);
            int regionZ = Mathf.FloorToInt(z / cellSize);
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (!TryGetCrater(regionX + offsetX, regionZ + offsetZ, out Crater crater))
                    {
                        continue;
                    }

                    float deltaX = x - crater.CenterX;
                    float deltaZ = z - crater.CenterZ;
                    float relative = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ) / crater.Radius;
                    if (relative < relativeDistance)
                    {
                        relativeDistance = relative;
                        nearest = crater;
                    }
                }
            }

            return !float.IsPositiveInfinity(relativeDistance);
        }

        /// <summary>
        /// Reshapes a raw elevation by every crater reaching the point: floors are lowered towards their
        /// target level, rims only ever raised, so overlapping craters and the underlying relief combine
        /// without one erasing a deeper hollow.
        /// </summary>
        public float Apply(float x, float z, float elevation, in CraterLevels levels)
        {
            if (_settings == null || _settings.CraterChance <= 0f)
            {
                return elevation;
            }

            float cellSize = _settings.CraterCellSize;
            int regionX = Mathf.FloorToInt(x / cellSize);
            int regionZ = Mathf.FloorToInt(z / cellSize);
            float result = elevation;
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (TryGetCrater(regionX + offsetX, regionZ + offsetZ, out Crater crater))
                    {
                        result = ApplyCrater(crater, x, z, result, levels);
                    }
                }
            }

            return result;
        }

        private float ApplyCrater(in Crater crater, float x, float z, float elevation, in CraterLevels levels)
        {
            float deltaX = x - crater.CenterX;
            float deltaZ = z - crater.CenterZ;
            float distance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
            if (distance > ReachOf(crater.Radius))
            {
                return elevation;
            }

            float relativeDistance = distance / crater.Radius;
            float floorLevel = crater.HasIce ? levels.IceFloor : levels.DryFloor;
            float floorWeight = 1f - Smoothstep(FlatFloorShare, 1f, relativeDistance);
            float result = Mathf.Lerp(elevation, Mathf.Min(elevation, floorLevel), floorWeight);

            float rimOffset = (relativeDistance - 1f) / _settings.CraterRimWidth;
            float rimWeight = Mathf.Exp(-rimOffset * rimOffset);
            float rimLevel = levels.LowRim;
            if (crater.BlocksMovement)
            {
                float angle = Mathf.Atan2(deltaZ, deltaX);
                rimLevel = Mathf.Lerp(levels.LowRim, levels.BlockingRim, GapClosure(crater, angle));
            }

            return Mathf.Max(result, Mathf.Lerp(result, rimLevel, rimWeight));
        }

        /// <summary>
        /// Zero inside a gap in the rim, one on solid rim. Gaps are evenly spread from a per-crater start
        /// angle so every blocking crater can be entered, rather than trusting noise to open one.
        /// </summary>
        private float GapClosure(in Crater crater, float angle)
        {
            float gapShare = _settings.CraterRimGapShare;
            if (gapShare <= 0f)
            {
                return 1f;
            }

            float spacing = Mathf.PI * 2f / crater.GapCount;
            float halfGap = gapShare * Mathf.PI / crater.GapCount;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < crater.GapCount; i++)
            {
                float gapAngle = crater.FirstGapAngle + i * spacing;
                nearest = Mathf.Min(nearest, Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, gapAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad);
            }

            return Smoothstep(halfGap, halfGap * 1.5f, nearest);
        }

        private float ReachOf(float radius) => radius * (1f + _settings.CraterRimWidth * RimReachInWidths);

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
