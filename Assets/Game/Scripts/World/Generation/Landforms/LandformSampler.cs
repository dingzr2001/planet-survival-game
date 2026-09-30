using System;
using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Evaluates the landform field at any point on the endless plane. Everything is a pure function of the
    /// seed, the settings and the landing site, so streamed chunks, gameplay queries and the editor preview
    /// all agree without sharing state.
    /// </summary>
    /// <remarks>
    /// Order of operations: warped fractal relief, with ridges grown only on high ground; pass corridors
    /// pressed below mountain height; smoothing and sliver removal of that relief (see
    /// <see cref="SmoothedElevationGrid"/>); then, per point, crater bowls and rims, the flattened landing
    /// site and the starter lake.
    /// Classification then thresholds the final elevation against levels measured from this seed's own
    /// relief, so authored coverage holds regardless of the noise range.
    /// </remarks>
    public sealed class LandformSampler
    {
        // The calibration grid spans ±CalibrationExtent metres at CalibrationStep spacing: wide enough to
        // cover several relief features, coarse enough to stay decorrelated and cheap at session start.
        private const int CalibrationSamplesPerAxis = 64;
        private const float CalibrationStep = 96f;

        // Ridges start to grow where the base relief passes HighlandStart and reach full height at
        // HighlandFull (raw fractal units). Keeping them off low ground is what groups mountains into ranges.
        private const float HighlandStart = -.1f;
        private const float HighlandFull = .25f;
        private const float RidgeSharpness = 3f;

        // A second pass network at a different scale; two overlapping networks cross far more often than
        // one, which is what keeps nearly every range breachable.
        private const float SecondPassScale = 1.37f;

        // Pass profile, as shares of the pass width in noise units: fully cut below the floor share, untouched
        // beyond the flare share, and the abs/min operations rounded over the softening share.
        private const float PassFloorShare = .35f;
        private const float PassFlareShare = 1.8f;
        private const float PassSoftening = .3f;

        private const int WarpXSalt = 101;
        private const int WarpZSalt = 211;
        private const int ReliefSalt = 307;
        private const int RidgeSalt = 401;
        private const int FirstPassSalt = 503;
        private const int SecondPassSalt = 601;
        private const int CraterSalt = 701;
        private const int StarterLakeSalt = 809;
        private const int VolcanicSalt = 907;

        // Lattice spacing of the smoothed field. The blur already removes detail finer than a couple of
        // metres, so one-metre nodes lose nothing while keeping block builds cheap.
        private const float SmoothingGridStep = 1f;

        private readonly LandformSettings _settings;
        private readonly int _seed;
        private readonly Vector2 _start;
        private readonly CraterField _craters;
        private readonly CraterLevels _craterLevels;
        private readonly float _lakeThreshold;
        private readonly float _basinThreshold;
        private readonly float _mountainThreshold;
        private readonly float _span;
        private readonly float _passLevel;
        private readonly float _plainLevel;
        private readonly Vector2 _starterLakeCenter;
        private readonly SmoothedElevationGrid _smoothed;
        private readonly VolcanicField _volcanic;

        public LandformSampler(int worldSeed, LandformSettings settings, Vector2 startCenter)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _seed = worldSeed ^ settings.SeedOffset;
            _start = startCenter;

            float[] calibration = MeasureBaseRelief();
            Array.Sort(calibration);
            _lakeThreshold = Quantile(calibration, settings.IceLakeCoverage);
            _basinThreshold = Mathf.Max(_lakeThreshold, Quantile(calibration, settings.BasinCoverage));
            _mountainThreshold = settings.MountainCoverage > 0f
                ? Mathf.Max(_basinThreshold, Quantile(calibration, 1f - settings.MountainCoverage))
                : float.PositiveInfinity;
            float top = float.IsPositiveInfinity(_mountainThreshold) ? calibration[^1] : _mountainThreshold;
            _span = Mathf.Max(1e-4f, top - _lakeThreshold);

            _passLevel = top - _span * .35f;
            _plainLevel = Mathf.Lerp(_basinThreshold, top, .4f);
            _craterLevels = new CraterLevels(
                iceFloor: _lakeThreshold - _span * .15f,
                dryFloor: Mathf.Lerp(_lakeThreshold, _basinThreshold, .5f),
                lowRim: top - _span * .2f,
                blockingRim: top + _span * .25f);
            float startReach = settings.StartFlatRadius + settings.StartBlendDistance;
            _craters = new CraterField(_seed + CraterSalt, settings, startCenter, startReach);

            float lakeAngle = GradientNoise.Hash01(_seed + StarterLakeSalt, 0, 0) * Mathf.PI * 2f;
            _starterLakeCenter = startCenter
                                 + new Vector2(Mathf.Cos(lakeAngle), Mathf.Sin(lakeAngle)) * settings.StarterLakeDistance;
            _smoothed = new SmoothedElevationGrid(
                RawRelief, SmoothingGridStep, settings.OutlineSmoothing, settings.SliverRemoval);
            _volcanic = new VolcanicField(_seed + VolcanicSalt, settings, startCenter);
        }

        public CraterField Craters => _craters;

        /// <summary>Where the ground is bare rock instead of regolith, and where lava lies on it.</summary>
        public VolcanicField Volcanic => _volcanic;
        public Vector2 StarterLakeCenter => _starterLakeCenter;

        public LandformSample Sample(float x, float z)
        {
            float raw = FinalElevation(x, z);
            return new LandformSample(
                (raw - _lakeThreshold) / _span,
                (_basinThreshold - _lakeThreshold) / _span,
                Refine(Classify(raw), x, z));
        }

        public LandformKind KindAt(float x, float z) => Refine(Classify(FinalElevation(x, z)), x, z);

        /// <summary>
        /// Adds what the elevation alone does not decide. Rock ground holds lava lakes where the lava field
        /// peaks (mountains stand above it) and never holds ice. Lakes of either kind
        /// are level liquid, so on a crater's tilted wall or rim they give way to the ground beneath; a
        /// crater's own ice floor therefore ends where its wall begins.
        /// </summary>
        private LandformKind Refine(LandformKind kind, float x, float z)
        {
            if (kind == LandformKind.Mountain)
            {
                return kind;
            }

            _volcanic.Sample(x, z, out float rockGround, out float lava);
            LandformKind refined = kind;
            if (lava > 1f)
            {
                refined = LandformKind.LavaLake;
            }
            else if (kind == LandformKind.IceLake && rockGround > 0f)
            {
                refined = LandformKind.Basin;
            }

            bool isLake = refined == LandformKind.IceLake || refined == LandformKind.LavaLake;
            if (isLake && _craters.IsOnSlope(x, z))
            {
                return kind == LandformKind.IceLake ? LandformKind.Basin : kind;
            }

            return refined;
        }

        private LandformKind Classify(float raw)
        {
            if (raw >= _mountainThreshold)
            {
                return LandformKind.Mountain;
            }

            if (raw <= _lakeThreshold && _settings.IceLakeCoverage > 0f)
            {
                return LandformKind.IceLake;
            }

            return raw <= _basinThreshold ? LandformKind.Basin : LandformKind.Plain;
        }

        /// <summary>
        /// The noise-driven relief before shaping: the source of both the sharp corners (pass corridors cut
        /// across ridge creases) and the thin tongues, and the only part that is smoothed.
        /// </summary>
        private float RawRelief(float x, float z)
        {
            float elevation = BaseRelief(x, z);
            return Mathf.Lerp(elevation, Mathf.Min(elevation, _passLevel), PassMask(x, z));
        }

        /// <summary>
        /// Shaped relief plus the hand-shaped stamps. Crater rims, the flattened landing site and the starter
        /// lake are already smooth by construction, and a crater rim is a deliberately thin ring that sliver
        /// removal would erase, so they are applied after shaping, per point.
        /// </summary>
        private float FinalElevation(float x, float z)
        {
            float elevation = _craters.Apply(x, z, _smoothed.Sample(x, z), _craterLevels);
            elevation = FlattenLandingSite(x, z, elevation);
            return CarveStarterLake(x, z, elevation);
        }

        private float BaseRelief(float x, float z)
        {
            float warpSize = _settings.WarpFeatureSize;
            float warpDistance = _settings.WarpDistance;
            float warpedX = x + warpDistance * GradientNoise.Sample(_seed + WarpXSalt, x / warpSize, z / warpSize);
            float warpedZ = z + warpDistance * GradientNoise.Sample(_seed + WarpZSalt, x / warpSize, z / warpSize);

            float reliefSize = _settings.ReliefFeatureSize;
            float relief = GradientNoise.Fbm(_seed + ReliefSalt, warpedX / reliefSize, warpedZ / reliefSize,
                _settings.ReliefOctaves);
            float ridgeSize = _settings.RidgeFeatureSize;
            float ridge = Mathf.Pow(
                GradientNoise.Ridged(_seed + RidgeSalt, warpedX / ridgeSize, warpedZ / ridgeSize), RidgeSharpness);
            float highland = Smoothstep(HighlandStart, HighlandFull, relief);
            return relief + _settings.RidgeWeight * ridge * highland;
        }

        /// <summary>
        /// One inside a pass corridor, zero outside. A corridor follows the zero crossings of a noise
        /// field, the absolute-value band Factorio uses to keep Vulcanus' lava mazes walkable.
        /// </summary>
        private float PassMask(float x, float z)
        {
            float width = _settings.PassWidth;
            if (width <= 0f)
            {
                return 0f;
            }

            // Every operation here is smooth: a hard abs or min leaves a crease, and a steep falloff gives the
            // corridor vertical walls, and either one meets the mountain's own outline at a sharp wedge.
            // Soft versions and a wide falloff flare each pass mouth into a rounded opening instead.
            float size = _settings.PassFeatureSize;
            float first = SoftAbs(GradientNoise.Sample(_seed + FirstPassSalt, x / size, z / size), width * PassSoftening);
            float secondSize = size * SecondPassScale;
            float second = SoftAbs(
                GradientNoise.Sample(_seed + SecondPassSalt, x / secondSize, z / secondSize), width * PassSoftening);
            float distance = SmoothMin(first, second, width * PassSoftening);
            return 1f - Smoothstep(width * PassFloorShare, width * PassFlareShare, distance);
        }

        private static float SoftAbs(float value, float softness) => Mathf.Sqrt(value * value + softness * softness);

        /// <summary>Polynomial smooth minimum: equal to min() away from the crossing, rounded within k of it.</summary>
        private static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f)
            {
                return Mathf.Min(a, b);
            }

            float h = Mathf.Clamp01(.5f + .5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        private float FlattenLandingSite(float x, float z, float elevation)
        {
            float distance = Vector2.Distance(new Vector2(x, z), _start);
            float flatRadius = _settings.StartFlatRadius;
            float blend = Smoothstep(flatRadius, flatRadius + _settings.StartBlendDistance, distance);
            return Mathf.Lerp(_plainLevel, elevation, blend);
        }

        private float CarveStarterLake(float x, float z, float elevation)
        {
            float radius = _settings.StarterLakeRadius;
            if (radius <= 0f)
            {
                return elevation;
            }

            float relativeDistance = Vector2.Distance(new Vector2(x, z), _starterLakeCenter) / radius;
            float weight = 1f - Smoothstep(.7f, 1.4f, relativeDistance);
            return Mathf.Lerp(elevation, Mathf.Min(elevation, _craterLevels.IceFloor), weight);
        }

        private float[] MeasureBaseRelief()
        {
            var samples = new float[CalibrationSamplesPerAxis * CalibrationSamplesPerAxis];
            float extent = CalibrationSamplesPerAxis * CalibrationStep * .5f;
            for (int z = 0; z < CalibrationSamplesPerAxis; z++)
            {
                for (int x = 0; x < CalibrationSamplesPerAxis; x++)
                {
                    samples[z * CalibrationSamplesPerAxis + x] = BaseRelief(
                        _start.x - extent + (x + .5f) * CalibrationStep,
                        _start.y - extent + (z + .5f) * CalibrationStep);
                }
            }

            return samples;
        }

        private static float Quantile(float[] sorted, float fraction)
        {
            float position = Mathf.Clamp01(fraction) * (sorted.Length - 1);
            int lower = Mathf.FloorToInt(position);
            int upper = Mathf.Min(lower + 1, sorted.Length - 1);
            return Mathf.Lerp(sorted[lower], sorted[upper], position - lower);
        }

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
