using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// How landforms look: the shape and textures of the 3D mountain meshes, the ground shader's shadows and
    /// basins, and the marks around craters. Ice lakes take the look of their surface, like any sheet-ice
    /// patch. Kept apart from <see cref="Generation.Landforms.LandformSettings"/> so tuning a colour or a
    /// cliff height never regenerates the world or moves a collision border, and apart from the surfaces so
    /// the mountain look can change without touching what gameplay considers diggable.
    /// </summary>
    [Serializable]
    public sealed class LandformAppearance
    {
        [Header("Mountain shape")]
        [SerializeField, Min(0f), Tooltip("Metres the cliff rises from the collision border before the mountain top begins.")]
        private float _cliffHeight = 2.4f;
        [SerializeField, Range(0f, .8f), Tooltip("How much the cliff height wanders along an edge, as a share of the cliff height. A wall of even height reads as a cut slab.")]
        private float _cliffHeightVariation = .35f;
        [SerializeField, Min(0f), Tooltip("Metres the mountain top keeps rising, gently, further in from its cliffs.")]
        private float _plateauRise = .5f;
        [SerializeField, Min(0f), Tooltip("Metres a massif rises per unit of landform elevation above the mountain level. Large ranges, whose elevation climbs far above that level, swell into broad summits; thin ridges and crater rims stay low.")]
        private float _massifRise = 2.5f;
        [SerializeField, Min(0f), Tooltip("Metres from valley floor to the highest crests laid over mountain tops.")]
        private float _ridgeHeight = 4.5f;
        [SerializeField, Min(4f), Tooltip("Rough metres between neighbouring main crests on mountain tops; side ridges branch off at finer scales.")]
        private float _ridgeSpacing = 34f;
        [SerializeField, Min(0f), Tooltip("Amplitude, in metres, of the noise that roughens every landform border.")]
        private float _edgeRoughness = .35f;
        [SerializeField, Min(1f), Tooltip("How much taller mountain meshes are built than their nominal heights. Sprites are drawn facing the camera at full height, while real geometry under the 75° camera shows only cos(75°) of its height; 1 / cos(75°) ≈ 3.86 puts walls at the same on-screen scale as the sprites standing beside them.")]
        private float _verticalExaggeration = 3.8637f;

        [SerializeField, Min(1f), Tooltip("How much steeper than their true shape mountains are lit, like the z-factor of a cartographic hillshade. Seen from almost straight above, true slopes barely change the light; exaggerating them makes ridges and valleys read without raising the mesh, which would hide more ground behind it.")]
        private float _reliefShading = 3.5f;

        [Header("Mountain textures")]
        [SerializeField, Tooltip("Seamless texture projected from above onto the flatter parts of the mountain mesh.")]
        private Texture2D _mountainTexture;
        [SerializeField, Min(.5f), Tooltip("World metres covered by one repeat of the mountain texture.")]
        private float _mountainTextureSize = 14f;
        [SerializeField, Tooltip("Colour multiplied over the mountain texture.")]
        private Color _mountainTint = Color.white;
        [SerializeField, Tooltip("Seamless front view of a rock wall with horizontal bedding, projected sideways onto the steep parts of the mountain mesh.")]
        private Texture2D _cliffTexture;
        [SerializeField, Min(.5f), Tooltip("World metres covered by one repeat of the cliff texture, both along and up the face.")]
        private float _cliffTextureSize = 6f;
        [SerializeField, Tooltip("Colour multiplied over the cliff texture.")]
        private Color _cliffColor = new(1f, .97f, .95f, 1f);

        [Header("Cliff foot")]
        [SerializeField, Tooltip("Cutouts of single fallen rocks, each centred on a square texture, scattered on the ground along the foot of every cliff, largest against the wall.")]
        private Texture2D[] _talusStones = Array.Empty<Texture2D>();
        [SerializeField, Tooltip("Colour multiplied over the fallen rocks. Leave white when the rock art already matches the mountain.")]
        private Color _talusTint = Color.white;
        [SerializeField, Min(0f), Tooltip("Metres the fallen rocks reach out from the foot of a cliff. Zero disables them.")]
        private float _talusWidth = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Darkness of the shadow mountains cast on the ground. Its shape follows the mountain mesh.")]
        private float _shadowStrength = .45f;

        [Header("Craters")]
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the light and shade on crater walls and rims. From above, a crater reads by its lit and shaded inner walls and the sharp line of its rim, not by colour.")]
        private float _craterReliefStrength = .6f;
        [SerializeField, Tooltip("Seamless texture of the fine dust and debris settled on crater floors, laid over the regolith towards each floor's centre.")]
        private Texture2D _craterFloorTexture;
        [SerializeField, Min(.5f), Tooltip("World metres covered by one repeat of the crater-floor texture.")]
        private float _craterFloorTextureSize = 5f;
        [SerializeField, Tooltip("Colour multiplied over the crater-floor texture; alpha sets how fully it covers the floor. Keep the floor clearly paler and greyer than the regolith: tinted to match, it reads as nothing more than darker ground.")]
        private Color _craterFloorTint = new(.95f, .9f, .86f, .9f);
        [SerializeField, Tooltip("Colour of the ejecta thrown out around a crater, lighter fresh material; alpha sets its strength at the rim.")]
        private Color _craterEjectaTint = new(.66f, .56f, .48f, .32f);
        [SerializeField, Range(1.2f, 3f), Tooltip("How far the ejecta reaches, in crater radii from the centre. Close to the rim it is a continuous blanket; further out it breaks into rays.")]
        private float _ejectaReach = 2f;
        [SerializeField, Range(0f, 1f), Tooltip("Share of one-metre cells holding a rock where the ejecta is thickest. Zero leaves craters without rocks.")]
        private float _ejectaRockDensity = .35f;

        [Header("Rock ground")]
        [SerializeField, Tooltip("Seamless bare rock laid in place of the regolith over rock ground, the only ground lava surfaces on.")]
        private Texture2D _rockGroundTexture;
        [SerializeField, Min(.5f), Tooltip("World metres covered by one repeat of the rock-ground texture.")]
        private float _rockGroundTextureSize = 12f;
        [SerializeField, Tooltip("Colour multiplied over the rock-ground texture.")]
        private Color _rockGroundTint = Color.white;

        [Header("Lava")]
        [SerializeField, Tooltip("Seamless molten rock laid over lava lakes.")]
        private Texture2D _lavaTexture;
        [SerializeField, Min(.5f), Tooltip("World metres covered by one repeat of the lava texture.")]
        private float _lavaTextureSize = 10f;
        [SerializeField, Min(0f), Tooltip("Metres per second the lava surface drifts, so lakes read as molten rather than painted.")]
        private float _lavaFlowSpeed = .12f;
        [SerializeField, Tooltip("Colour of the glow the lava casts on the ground around its shore; alpha sets its strength.")]
        private Color _lavaGlowColor = new(1f, .42f, .08f, .55f);
        [SerializeField, Min(0f), Tooltip("Metres the glow reaches out from a lava shore.")]
        private float _lavaGlowReach = 4f;
        [SerializeField, Tooltip("Colour of the cooled crust where lava meets its shore.")]
        private Color _lavaCrustColor = new(.14f, .07f, .05f, 1f);
        [SerializeField, Tooltip("Cutouts of single glowing fissures, each centred on a square texture, scattered over rock ground around the lava.")]
        private Texture2D[] _lavaFissures = Array.Empty<Texture2D>();
        [SerializeField, Range(0f, 1f), Tooltip("Share of fissure cells holding a fissure where the ground is at its most active, next to the lava.")]
        private float _fissureDensity = .55f;
        [SerializeField, Tooltip("Smallest and largest fissure, in metres across.")]
        private Vector2 _fissureSizeRange = new(3.5f, 7f);

        [Header("Ground")]
        [SerializeField, Tooltip("Colour laid over basin floors; alpha sets how strongly they darken.")]
        private Color _basinTint = new(.16f, .07f, .05f, .32f);
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the relief shading on open ground.")]
        private float _hillshadeStrength = .35f;

        // Rubble is scattered in one-metre cells and looked up to two cells away from a pixel.
        private const float TalusReach = 2.5f;

        private const float MaximumShadowLength = 8f;

        public float CliffHeight => Mathf.Max(0f, _cliffHeight);
        public float CliffHeightVariation => Mathf.Clamp(_cliffHeightVariation, 0f, .8f);
        public float PlateauRise => Mathf.Max(0f, _plateauRise);
        public float MassifRise => Mathf.Max(0f, _massifRise);
        public float RidgeHeight => Mathf.Max(0f, _ridgeHeight);
        public float RidgeSpacing => Mathf.Max(4f, _ridgeSpacing);
        public float EdgeRoughness => Mathf.Max(0f, _edgeRoughness);
        public float VerticalExaggeration => Mathf.Max(1f, _verticalExaggeration);
        public float ReliefShading => Mathf.Max(1f, _reliefShading);
        public Texture2D MountainTexture => _mountainTexture;
        public float MountainTextureSize => Mathf.Max(.5f, _mountainTextureSize);
        public Color MountainTint => _mountainTint;
        public Texture2D CliffTexture => _cliffTexture;
        public float CliffTextureSize => Mathf.Max(.5f, _cliffTextureSize);
        public Color CliffColor => _cliffColor;
        public IReadOnlyList<Texture2D> TalusStones => _talusStones ?? Array.Empty<Texture2D>();
        public Color TalusTint => _talusTint;
        public float TalusWidth => Mathf.Max(0f, _talusWidth);
        public float ShadowStrength => Mathf.Clamp01(_shadowStrength);
        public Color BasinTint => _basinTint;
        public Texture2D RockGroundTexture => _rockGroundTexture;
        public float RockGroundTextureSize => Mathf.Max(.5f, _rockGroundTextureSize);
        public Color RockGroundTint => _rockGroundTint;
        public Texture2D LavaTexture => _lavaTexture;
        public float LavaTextureSize => Mathf.Max(.5f, _lavaTextureSize);
        public float LavaFlowSpeed => Mathf.Max(0f, _lavaFlowSpeed);
        public Color LavaGlowColor => _lavaGlowColor;
        public float LavaGlowReach => Mathf.Max(0f, _lavaGlowReach);
        public Color LavaCrustColor => _lavaCrustColor;
        public IReadOnlyList<Texture2D> LavaFissures => _lavaFissures ?? Array.Empty<Texture2D>();
        public float FissureDensity => Mathf.Clamp01(_fissureDensity);
        public float MinimumFissureSize => Mathf.Max(.5f, Mathf.Min(_fissureSizeRange.x, _fissureSizeRange.y));
        public float MaximumFissureSize => Mathf.Max(.5f, Mathf.Max(_fissureSizeRange.x, _fissureSizeRange.y));
        public float CraterReliefStrength => Mathf.Clamp01(_craterReliefStrength);
        public Texture2D CraterFloorTexture => _craterFloorTexture;
        public float CraterFloorTextureSize => Mathf.Max(.5f, _craterFloorTextureSize);
        public Color CraterFloorTint => _craterFloorTint;
        public Color CraterEjectaTint => _craterEjectaTint;
        public float EjectaReach => Mathf.Clamp(_ejectaReach, 1.2f, 3f);
        public float EjectaRockDensity => Mathf.Clamp01(_ejectaRockDensity);
        public float HillshadeStrength => Mathf.Clamp01(_hillshadeStrength);

        /// <summary>The tallest a mountain mesh can stand, in metres.</summary>
        public float MaximumMountainHeight =>
            CliffHeight * (1f + CliffHeightVariation) + PlateauRise + MountainHeightProfile.BumpAmplitude
            + MassifRise * MountainHeightProfile.MaximumElevationExcess + RidgeHeight;

        /// <summary>
        /// Horizontal metres a mountain's shadow can reach on the ground. The
        /// shader's light rises at about 45°, so a shadow is about as long as the rock casting it is tall.
        /// It is capped because every metre widens each chunk's landform map; only the rare summit taller
        /// than the cap loses the far end of its shadow, and the shader fades shadows out towards the cap so
        /// the cut never shows as a line.
        /// </summary>
        public float ShadowReach => Mathf.Min(MaximumMountainHeight, MaximumShadowLength);

        /// <summary>
        /// The furthest a ground-shader sample reaches beyond its pixel: a shadow march, a rubble cell or a
        /// fissure cell, plus the border roughness.
        /// </summary>
        public float SampleReach =>
            Mathf.Max(Mathf.Max(ShadowReach, TalusReach), FissureCellSize + MaximumFissureSize * .5f) + EdgeRoughness;

        /// <summary>Metres per side of the cells fissures are scattered in, one fissure at most per cell.</summary>
        public const float FissureCellSize = 6f;

        public void ConfigureTalusStones(params Texture2D[] stones)
        {
            _talusStones = stones ?? Array.Empty<Texture2D>();
        }

        public void ConfigureCliffTexture(Texture2D texture, float textureSize)
        {
            _cliffTexture = texture;
            _cliffTextureSize = Mathf.Max(.5f, textureSize);
        }

        public void ConfigureRockGround(Texture2D texture, float textureSize)
        {
            _rockGroundTexture = texture;
            _rockGroundTextureSize = Mathf.Max(.5f, textureSize);
        }

        public void ConfigureLava(Texture2D texture, float textureSize, params Texture2D[] fissures)
        {
            _lavaTexture = texture;
            _lavaTextureSize = Mathf.Max(.5f, textureSize);
            _lavaFissures = fissures ?? Array.Empty<Texture2D>();
        }

        public void ConfigureCraterFloorTexture(Texture2D texture, float textureSize)
        {
            _craterFloorTexture = texture;
            _craterFloorTextureSize = Mathf.Max(.5f, textureSize);
        }

        public void ConfigureMountainTexture(Texture2D texture, float textureSize)
        {
            _mountainTexture = texture;
            _mountainTextureSize = Mathf.Max(.5f, textureSize);
        }
    }
}
