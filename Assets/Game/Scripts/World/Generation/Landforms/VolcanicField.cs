using System;
using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Where the ground is bare volcanic rock instead of regolith, and where lava lies on it. Two fields:
    /// a very broad one lays out the rock ground itself, and a finer one clusters lava lakes, which surface
    /// in the inner part of rock ground and die out towards its rim. Both are normalised against this seed's own distribution,
    /// so authored coverage holds whatever the noise range, and both change smoothly, so the shader can
    /// turn them into distances in metres through their gradients.
    /// </summary>
    /// <remarks>
    /// The landing site is kept clear: each seed shifts the rock-ground noise to the first of a few
    /// candidate offsets that leaves everything within <see cref="LandformSettings.RockGroundClearance"/>
    /// metres of it on regolith, so a new expedition always starts on regolith and never next to lava.
    /// Shifting rather than pressing the field down keeps every edge the noise's own: a radial press made
    /// rock and lava edges near the landing site follow its circle as ruled lines.
    /// </remarks>
    public sealed class VolcanicField
    {
        /// <summary>
        /// Share of rock ground, counted from its heart outwards, where lava surfaces freely. Across the outer
        /// rim it is pressed down until, at the border with regolith, none can remain.
        /// </summary>
        public const float LavaInteriorShare = .6f;

        // Share of the finer layer in the rock-ground field; enough to fray outlines without scattering new regions.
        private const float DetailWeight = .22f;

        // Candidate noise offsets tried for a clear landing site; with rock on well under half the ground
        // the first few almost always succeed. The offsets span many rock expanses.
        private const int DomainAttempts = 32;
        private const float DomainOffsetRange = 40000f;

        // Metres between the points checked around the landing site, and how far below the border (raw
        // units) they must stay so nothing between two checked points can rise above it.
        private const float ClearanceCheckStep = 15f;
        private const float ClearanceMargin = .04f;

        // Only for a seed where no candidate is clear: metres over which a hard press around the landing
        // site fades out.
        private const float FallbackClearanceBlend = 120f;

        // Fissures scatter over ground where lava is this many times as likely as the lakes themselves,
        // so they gather around the lakes instead of covering all rock ground.
        private const float ActiveZoneScale = 3f;
        private const float MaximumActiveShare = .9f;

        private const int CalibrationSamplesPerAxis = 64;

        // Rock ground is calibrated on a grid scaled to its own features, so the quantiles see many
        // expanses rather than two or three huge ones.
        private const float RockCalibrationStepShare = .25f;
        private const float LavaCalibrationStep = 96f;

        private const int RockSalt = 911;
        private const int RockDetailSalt = 1013;
        private const int LavaSalt = 1109;
        private const int DomainSalt = 1201;

        private readonly LandformSettings _settings;
        private readonly int _seed;
        private readonly Vector2 _start;
        private readonly float _rockEdge;
        private readonly float _rockInterior;
        private readonly float _lavaActive;
        private readonly float _lavaShore;
        private readonly float _rimPressure;
        private readonly float _clearancePressure;
        private readonly Vector2 _domainOffset;
        private readonly bool _hasLava;

        public VolcanicField(int seed, LandformSettings settings, Vector2 startCenter)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _seed = seed;
            _start = startCenter;

            float coverage = settings.RockGroundCoverage;
            float[] rock = MeasureField(RawRockGround, settings.RockGroundFeatureSize * RockCalibrationStepShare);
            _rockEdge = Quantile(rock, 1f - coverage);
            _rockInterior = Mathf.Max(_rockEdge + 1e-3f, Quantile(rock, 1f - coverage * LavaInteriorShare));
            // The raw field is stationary, so calibrating before the offset is chosen measures the same thing.
            bool clear = true;
            if (coverage > 0f)
            {
                _domainOffset = PickClearDomain(out clear);
            }

            // Enough to push the highest possible raw value (both Fbm layers at their maximum of 1) below the
            // border; only used when no offset left the landing site clear.
            _clearancePressure = clear ? 0f : (1f + DetailWeight - _rockEdge) / (_rockInterior - _rockEdge) + 1f;

            // Lava and rock noise are independent, so the lava share inside the interior is the lava
            // coverage over the interior's share of all ground.
            float interiorShare = coverage * LavaInteriorShare;
            float lakeShare = interiorShare > 0f ? Mathf.Clamp01(settings.LavaLakeCoverage / interiorShare) : 0f;
            _hasLava = lakeShare > 0f;
            float[] lava = MeasureField(RawLava, LavaCalibrationStep);
            _lavaShore = Quantile(lava, 1f - lakeShare);
            float activeShare = Mathf.Min(MaximumActiveShare, lakeShare * ActiveZoneScale);
            _lavaActive = Mathf.Min(_lavaShore - 1e-3f, Quantile(lava, 1f - activeShare));
            // Enough to push the highest possible lava value (Fbm never exceeds 1) below zero on the border.
            _rimPressure = (1f - _lavaActive) / (_lavaShore - _lavaActive) + 1f;
        }

        /// <summary>
        /// The rock-ground field: below 0 on regolith, 0 on the border of rock ground, and 1 where the
        /// inner part that may hold lava begins.
        /// </summary>
        public float RockGround(float x, float z)
        {
            if (_settings.RockGroundCoverage <= 0f)
            {
                return -1f;
            }

            float rock = (RawRockGround(x, z) - _rockEdge) / (_rockInterior - _rockEdge);
            return _clearancePressure > 0f ? rock - FallbackClearance(x, z) * _clearancePressure : rock;
        }

        /// <summary>
        /// Lava activity: at most 0 far from lava, rising through the ground fissures gather on, 1 on the
        /// shore of a lava lake and above 1 in it. Always below 0 off rock ground.
        /// </summary>
        public float Lava(float x, float z)
        {
            if (!_hasLava)
            {
                return -1f;
            }

            return PressedLava(x, z, RockGround(x, z));
        }

        /// <summary>Both fields at once, evaluating the rock ground only once.</summary>
        public void Sample(float x, float z, out float rockGround, out float lava)
        {
            rockGround = RockGround(x, z);
            lava = _hasLava ? PressedLava(x, z, rockGround) : -1f;
        }

        public bool IsRockGround(float x, float z) => RockGround(x, z) > 0f;

        public bool IsLava(float x, float z) => Lava(x, z) > 1f;

        /// <remarks>
        /// Pressing lava down across the rim, rather than capping it at the rock-ground field, lets lakes
        /// shrink along their own outlines. A cap cut them along the rock field's contour, which at its
        /// scale is almost straight and showed as a ruled edge with a glowing line beside it.
        /// </remarks>
        private float PressedLava(float x, float z, float rockGround)
        {
            float lava = (RawLava(x, z) - _lavaActive) / (_lavaShore - _lavaActive);
            return lava - _rimPressure * Mathf.Max(0f, 1f - rockGround);
        }

        /// <summary>
        /// The first candidate offset whose rock-ground field stays clearly below the border across the whole
        /// clearance around the landing site; failing that, the one that comes closest.
        /// </summary>
        private Vector2 PickClearDomain(out bool clear)
        {
            Vector2 best = Vector2.zero;
            float bestHighest = float.PositiveInfinity;
            for (int attempt = 0; attempt < DomainAttempts; attempt++)
            {
                Vector2 offset = attempt == 0
                    ? Vector2.zero
                    : new Vector2(
                        (GradientNoise.Hash01(_seed + DomainSalt, attempt, 0) - .5f) * DomainOffsetRange,
                        (GradientNoise.Hash01(_seed + DomainSalt, attempt, 1) - .5f) * DomainOffsetRange);
                float highest = HighestRawAroundStart(offset);
                if (highest < _rockEdge - ClearanceMargin)
                {
                    clear = true;
                    return offset;
                }

                if (highest < bestHighest)
                {
                    bestHighest = highest;
                    best = offset;
                }
            }

            clear = false;
            return best;
        }

        private float HighestRawAroundStart(Vector2 offset)
        {
            float radius = _settings.RockGroundClearance + ClearanceCheckStep;
            int steps = Mathf.CeilToInt(radius / ClearanceCheckStep);
            float highest = float.NegativeInfinity;
            for (int z = -steps; z <= steps; z++)
            {
                for (int x = -steps; x <= steps; x++)
                {
                    var local = new Vector2(x, z) * ClearanceCheckStep;
                    if (local.magnitude <= radius)
                    {
                        Vector2 point = _start + local + offset;
                        highest = Mathf.Max(highest, RawRockGroundAt(point.x, point.y));
                    }
                }
            }

            return highest;
        }

        /// <summary>One at the landing site, fading to zero beyond the clearance radius.</summary>
        private float FallbackClearance(float x, float z)
        {
            float clearance = _settings.RockGroundClearance;
            float fromStart = Vector2.Distance(new Vector2(x, z), _start);
            return 1f - Smoothstep(clearance, clearance + FallbackClearanceBlend, fromStart);
        }

        private float RawRockGround(float x, float z) => RawRockGroundAt(x + _domainOffset.x, z + _domainOffset.y);

        private float RawRockGroundAt(float x, float z)
        {
            float size = _settings.RockGroundFeatureSize;
            float broad = GradientNoise.Fbm(_seed + RockSalt, x / size, z / size, 3);
            float detailSize = _settings.RockGroundDetailSize;
            float detail = GradientNoise.Fbm(_seed + RockDetailSalt, x / detailSize, z / detailSize, 2);
            return broad + DetailWeight * detail;
        }

        private float RawLava(float x, float z)
        {
            float size = _settings.LavaFeatureSize;
            return GradientNoise.Fbm(_seed + LavaSalt, x / size, z / size, 3);
        }

        private float[] MeasureField(Func<float, float, float> field, float step)
        {
            var samples = new float[CalibrationSamplesPerAxis * CalibrationSamplesPerAxis];
            float extent = CalibrationSamplesPerAxis * step * .5f;
            for (int z = 0; z < CalibrationSamplesPerAxis; z++)
            {
                for (int x = 0; x < CalibrationSamplesPerAxis; x++)
                {
                    samples[z * CalibrationSamplesPerAxis + x] = field(
                        _start.x - extent + (x + .5f) * step,
                        _start.y - extent + (z + .5f) * step);
                }
            }

            Array.Sort(samples);
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
