using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Shape of the large-scale landforms: mountain ranges, basins, craters, ice lakes, and the rock ground that holds lava. Coverage is
    /// authored as a share of ground rather than raw noise thresholds; <see cref="LandformSampler"/>
    /// measures each seed's own elevation distribution and converts. Which surfaces the landforms carry is
    /// configured on the terrain patch settings, so this asset stays pure shape.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/World/Landform Settings", fileName = "LandformSettings")]
    public sealed class LandformSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Mixed into the world seed so landforms do not correlate with resource patches.")]
        private int _seedOffset = 7919;

        [Header("Relief")]
        [SerializeField, Min(50f), Tooltip("Metres across one broad rise or hollow of the base relief.")]
        private float _reliefFeatureSize = 480f;
        [SerializeField, Range(1, 6), Tooltip("Detail layers in the base relief. More octaves give rougher outlines at a small sampling cost.")]
        private int _reliefOctaves = 4;
        [SerializeField, Min(10f), Tooltip("Metres across one bend of the domain warp that makes ranges meander instead of following the noise lattice.")]
        private float _warpFeatureSize = 260f;
        [SerializeField, Min(0f), Tooltip("How far, in metres, the warp may displace a point.")]
        private float _warpDistance = 60f;
        [SerializeField, Min(10f), Tooltip("Metres between neighbouring mountain ridge lines.")]
        private float _ridgeFeatureSize = 260f;
        [SerializeField, Range(0f, 3f), Tooltip("How strongly ridges rise above the base relief. Ridges only grow on high ground, which is what groups mountains into ranges.")]
        private float _ridgeWeight = 2f;

        [Header("Coverage")]
        [SerializeField, Range(0f, .4f), Tooltip("Approximate share of ground that is impassable mountain, before passes and craters.")]
        private float _mountainCoverage = .1f;
        [SerializeField, Range(0f, .5f), Tooltip("Approximate share of ground that is low basin, including the ice lakes inside it.")]
        private float _basinCoverage = .16f;
        [SerializeField, Range(0f, .3f), Tooltip("Approximate share of ground frozen into ice lakes. Must stay below the basin coverage.")]
        private float _iceLakeCoverage = .045f;

        [Header("Passes")]
        [SerializeField, Min(10f), Tooltip("Metres across one bend of the pass network cut through mountains.")]
        private float _passFeatureSize = 180f;
        [SerializeField, Range(0f, .3f), Tooltip("Width of each pass in noise units. The two pass networks are cut wherever the noise is within this distance of zero.")]
        private float _passWidth = .09f;

        [Header("Outlines")]
        [SerializeField, Range(0f, 6f), Tooltip("Metres of Gaussian smoothing applied to the relief before landforms are classified. Rounds the creases and corners left where pass corridors cut ridges, including inward corners; zero keeps them sharp.")]
        private float _outlineSmoothing = 3f;
        [SerializeField, Range(0f, 15f), Tooltip("Radius in metres of the opening that removes thin, tapering tongues of mountain. Mountains narrower than twice this vanish and every outward corner is rounded to this radius; valleys and passes keep their width. Zero keeps the raw outlines.")]
        private float _sliverRemoval = 5f;

        [Header("Craters")]
        [SerializeField, Min(40f), Tooltip("Metres per side of the region that may hold one crater. Must exceed twice the largest crater's reach.")]
        private float _craterCellSize = 170f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance that a crater region holds a crater.")]
        private float _craterChance = .35f;
        [SerializeField, Tooltip("Smallest and largest crater radius, in metres, measured to the rim crest.")]
        private Vector2 _craterRadiusRange = new(12f, 40f);
        [SerializeField, Range(.05f, .6f), Tooltip("Rim thickness as a share of the crater radius.")]
        private float _craterRimWidth = .22f;
        [SerializeField, Min(0f), Tooltip("Craters at least this wide get a rim tall enough to block movement; smaller craters have a low, walkable rim.")]
        private float _blockingRimMinRadius = 22f;
        [SerializeField, Range(0f, .8f), Tooltip("Share of a blocking rim broken open into walkable gaps.")]
        private float _craterRimGapShare = .3f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance that a crater floor holds an ice lake.")]
        private float _craterIceChance = .4f;

        [Header("Rock ground and lava")]
        [SerializeField, Range(0f, .6f), Tooltip("Approximate share of ground that is bare volcanic rock instead of regolith, lava lakes included. Rock ground is the only place lava lakes and fissures occur, and it never holds ice.")]
        private float _rockGroundCoverage = .3f;
        [SerializeField, Min(100f), Tooltip("Metres across one expanse of rock ground.")]
        private float _rockGroundFeatureSize = 1400f;
        [SerializeField, Min(10f), Tooltip("Metres across the bays and tongues that fray the rock-ground outline.")]
        private float _rockGroundDetailSize = 240f;
        [SerializeField, Range(0f, .2f), Tooltip("Approximate share of all ground covered by lava lakes. Lava keeps to the inner part of rock ground, so this must stay well below the rock-ground coverage.")]
        private float _lavaLakeCoverage = .035f;
        [SerializeField, Min(10f), Tooltip("Metres across one cluster of lava lakes.")]
        private float _lavaFeatureSize = 170f;
        [SerializeField, Min(0f), Tooltip("Metres around the landing site kept free of rock ground and lava.")]
        private float _rockGroundClearance = 150f;

        [Header("Starting area")]
        [SerializeField, Min(0f), Tooltip("Radius around the landing site kept as flat, open plain.")]
        private float _startFlatRadius = 26f;
        [SerializeField, Min(1f), Tooltip("Distance over which the flat starting area blends back into the generated relief.")]
        private float _startBlendDistance = 22f;
        [SerializeField, Min(0f), Tooltip("Distance from the landing site to the guaranteed starter ice lake. Its direction varies per seed.")]
        private float _starterLakeDistance = 30f;
        [SerializeField, Min(0f), Tooltip("Radius of the guaranteed starter ice lake. Zero disables it.")]
        private float _starterLakeRadius = 10f;

        public int SeedOffset => _seedOffset;
        public float ReliefFeatureSize => Mathf.Max(50f, _reliefFeatureSize);
        public int ReliefOctaves => Mathf.Clamp(_reliefOctaves, 1, 6);
        public float WarpFeatureSize => Mathf.Max(10f, _warpFeatureSize);
        public float WarpDistance => Mathf.Max(0f, _warpDistance);
        public float RidgeFeatureSize => Mathf.Max(10f, _ridgeFeatureSize);
        public float RidgeWeight => Mathf.Clamp(_ridgeWeight, 0f, 3f);
        public float MountainCoverage => Mathf.Clamp(_mountainCoverage, 0f, .4f);
        public float BasinCoverage => Mathf.Clamp(_basinCoverage, IceLakeCoverage, .5f);
        public float IceLakeCoverage => Mathf.Clamp(_iceLakeCoverage, 0f, .3f);
        public float PassFeatureSize => Mathf.Max(10f, _passFeatureSize);
        public float PassWidth => Mathf.Clamp(_passWidth, 0f, .3f);
        public float OutlineSmoothing => Mathf.Clamp(_outlineSmoothing, 0f, 6f);
        public float SliverRemoval => Mathf.Clamp(_sliverRemoval, 0f, 15f);
        public float CraterCellSize => Mathf.Max(40f, _craterCellSize);
        public float CraterChance => Mathf.Clamp01(_craterChance);
        public float MinimumCraterRadius => Mathf.Max(1f, Mathf.Min(_craterRadiusRange.x, _craterRadiusRange.y));
        public float MaximumCraterRadius => Mathf.Max(1f, Mathf.Max(_craterRadiusRange.x, _craterRadiusRange.y));
        public float CraterRimWidth => Mathf.Clamp(_craterRimWidth, .05f, .6f);
        public float BlockingRimMinRadius => Mathf.Max(0f, _blockingRimMinRadius);
        public float CraterRimGapShare => Mathf.Clamp(_craterRimGapShare, 0f, .8f);
        public float CraterIceChance => Mathf.Clamp01(_craterIceChance);
        public float RockGroundCoverage => Mathf.Clamp(_rockGroundCoverage, 0f, .6f);
        public float RockGroundFeatureSize => Mathf.Max(100f, _rockGroundFeatureSize);
        public float RockGroundDetailSize => Mathf.Max(10f, _rockGroundDetailSize);
        public float LavaLakeCoverage =>
            Mathf.Clamp(_lavaLakeCoverage, 0f, Mathf.Min(.2f, RockGroundCoverage * VolcanicField.LavaInteriorShare));
        public float LavaFeatureSize => Mathf.Max(10f, _lavaFeatureSize);
        public float RockGroundClearance => Mathf.Max(0f, _rockGroundClearance);
        public float StartFlatRadius => Mathf.Max(0f, _startFlatRadius);
        public float StartBlendDistance => Mathf.Max(1f, _startBlendDistance);
        public float StarterLakeDistance => Mathf.Max(0f, _starterLakeDistance);
        public float StarterLakeRadius => Mathf.Max(0f, _starterLakeRadius);

        /// <summary>Overrides the coverage targets. Used by editor setup and tests; gameplay reads only.</summary>
        public void ConfigureCoverage(float mountainCoverage, float basinCoverage, float iceLakeCoverage)
        {
            _mountainCoverage = Mathf.Clamp(mountainCoverage, 0f, .4f);
            _iceLakeCoverage = Mathf.Clamp(iceLakeCoverage, 0f, .3f);
            _basinCoverage = Mathf.Clamp(basinCoverage, _iceLakeCoverage, .5f);
        }

        /// <summary>Overrides the outline smoothing. Used by tests; gameplay reads only.</summary>
        public void ConfigureOutlineSmoothing(float metres, float sliverRemovalMetres)
        {
            _outlineSmoothing = Mathf.Clamp(metres, 0f, 6f);
            _sliverRemoval = Mathf.Clamp(sliverRemovalMetres, 0f, 15f);
        }

        /// <summary>Overrides crater frequency. Used by editor setup and tests; gameplay reads only.</summary>
        public void ConfigureCraters(float craterChance, float craterIceChance)
        {
            _craterChance = Mathf.Clamp01(craterChance);
            _craterIceChance = Mathf.Clamp01(craterIceChance);
        }

        /// <summary>Overrides the rock-ground and lava coverage. Used by tests; gameplay reads only.</summary>
        public void ConfigureRockGround(float rockGroundCoverage, float lavaLakeCoverage, float clearance)
        {
            _rockGroundCoverage = Mathf.Clamp(rockGroundCoverage, 0f, .6f);
            _lavaLakeCoverage = Mathf.Clamp(lavaLakeCoverage, 0f, .2f);
            _rockGroundClearance = Mathf.Max(0f, clearance);
        }

        private void OnValidate()
        {
            _basinCoverage = Mathf.Max(_basinCoverage, _iceLakeCoverage);
            _lavaLakeCoverage = Mathf.Min(_lavaLakeCoverage, _rockGroundCoverage * VolcanicField.LavaInteriorShare);
            float largestReach = MaximumCraterRadius * (1f + CraterRimWidth * 3f);
            if (_craterCellSize < largestReach * 2f)
            {
                Debug.LogWarning(
                    $"{name}: crater cells of {_craterCellSize} m are smaller than twice the largest crater reach " +
                    $"({largestReach:0.#} m); craters could be cut off at cell borders.", this);
            }
        }
    }
}
