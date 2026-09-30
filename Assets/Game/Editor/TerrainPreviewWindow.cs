using System;
using System.Collections.Generic;
using System.IO;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Generation.Landforms;
using PlanetSurvival.World.Ground;
using UnityEditor;
using UnityEngine;

namespace PlanetSurvival.Editor
{
    /// <summary>
    /// Draws a top-down map of the generated surface for any seed, so landform and patch settings can be
    /// tuned without walking the world. The same renderer runs headless through
    /// <see cref="RenderFromCommandLine"/> for batch review.
    /// </summary>
    public sealed class TerrainPreviewWindow : EditorWindow
    {
        private const string TerrainSettingsPath = "Assets/Game/Configuration/DefaultTerrainSettings.asset";
        private const string TerrainPatchSettingsPath = "Assets/Game/Configuration/DefaultTerrainPatches.asset";
        private const string OutputDirectoryArgument = "-terrainPreviewDir";
        private const string SeedsArgument = "-terrainPreviewSeeds";
        private const string SizeArgument = "-terrainPreviewSize";

        private static readonly Color PlainColor = new(.55f, .3f, .2f);
        private static readonly Color BasinColor = new(.36f, .19f, .13f);
        private static readonly Color IceLakeColor = new(.78f, .88f, .96f);
        private static readonly Color LavaLakeColor = new(.95f, .35f, .08f);
        private static readonly Color RockGroundColor = new(.34f, .33f, .33f);
        private static readonly Color RockBasinColor = new(.24f, .22f, .22f);
        private static readonly Color MountainColor = new(.47f, .43f, .41f);
        private static readonly Color StartColor = new(.2f, 1f, .35f);
        private static readonly Color[] PatchColors =
        {
            new(.85f, .45f, .2f), new(.7f, .85f, 1f), new(.3f, .28f, .26f), new(.45f, .4f, .36f),
            new(.62f, .56f, .5f), new(.9f, .8f, .3f), new(.5f, .7f, .4f), new(.7f, .4f, .7f)
        };

        [SerializeField] private int _seed;
        [SerializeField] private float _areaSize = 600f;
        [SerializeField] private int _resolution = 512;
        [SerializeField] private bool _showPatches = true;
        private Texture2D _preview;
        private string _statistics = string.Empty;

        [MenuItem("Planet Survival/Terrain Preview")]
        public static void Open()
        {
            GetWindow<TerrainPreviewWindow>("Terrain Preview");
        }

        /// <summary>
        /// Batch entry point. Writes one PNG and one statistics line per seed, for example:
        /// <c>-executeMethod PlanetSurvival.Editor.TerrainPreviewWindow.RenderFromCommandLine
        /// -terrainPreviewDir /tmp/previews -terrainPreviewSeeds 8128,1,2 -terrainPreviewSize 800</c>
        /// </summary>
        public static void RenderFromCommandLine()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string directory = ReadArgument(arguments, OutputDirectoryArgument) ?? "Library/TerrainPreviews";
            float areaSize = float.TryParse(ReadArgument(arguments, SizeArgument), out float size) ? size : 600f;
            Directory.CreateDirectory(directory);

            if (!TryLoadSettings(out TerrainGenerationSettings generation, out TerrainPatchSettings patches,
                    out string error))
            {
                Debug.LogError($"Terrain preview: {error}");
                return;
            }

            var seeds = new List<int>();
            string seedList = ReadArgument(arguments, SeedsArgument);
            if (string.IsNullOrWhiteSpace(seedList))
            {
                seeds.Add(generation.Seed);
            }
            else
            {
                foreach (string entry in seedList.Split(','))
                {
                    if (int.TryParse(entry.Trim(), out int seed))
                    {
                        seeds.Add(seed);
                    }
                }
            }

            foreach (int seed in seeds)
            {
                Texture2D texture = Render(seed, generation, patches, areaSize, Mathf.RoundToInt(areaSize), true,
                    out string statistics);
                string path = Path.Combine(directory, $"terrain_{seed}.png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                DestroyImmediate(texture);
                Debug.Log($"Terrain preview seed {seed}: {statistics} -> {path}");
            }
        }

        private void OnEnable()
        {
            if (_seed == 0 && TryLoadSettings(out TerrainGenerationSettings generation, out _, out _))
            {
                _seed = generation.Seed;
            }
        }

        private void OnDisable()
        {
            if (_preview != null)
            {
                DestroyImmediate(_preview);
            }
        }

        private void OnGUI()
        {
            _seed = EditorGUILayout.IntField("Seed", _seed);
            _areaSize = Mathf.Max(50f, EditorGUILayout.FloatField("Area (m)", _areaSize));
            _resolution = Mathf.Clamp(EditorGUILayout.IntField("Resolution (px)", _resolution), 64, 2048);
            _showPatches = EditorGUILayout.Toggle("Show patches", _showPatches);

            if (GUILayout.Button("Render"))
            {
                if (TryLoadSettings(out TerrainGenerationSettings generation, out TerrainPatchSettings patches,
                        out string error))
                {
                    if (_preview != null)
                    {
                        DestroyImmediate(_preview);
                    }

                    _preview = Render(_seed, generation, patches, _areaSize, _resolution, _showPatches,
                        out _statistics);
                }
                else
                {
                    _statistics = error;
                }
            }

            if (!string.IsNullOrEmpty(_statistics))
            {
                EditorGUILayout.HelpBox(_statistics, MessageType.Info);
            }

            if (_preview != null)
            {
                Rect area = GUILayoutUtility.GetAspectRect(1f);
                GUI.DrawTexture(area, _preview, ScaleMode.ScaleToFit);
            }
        }

        private static Texture2D Render(int seed, TerrainGenerationSettings generation, TerrainPatchSettings patches,
            float areaSize, int resolution, bool showPatches, out string statistics)
        {
            Vector3 start = generation.StartingAreaCenter;
            var map = new TerrainTileMap();
            // The session combines the world seed with the patch offset; the preview must do the same or
            // it would show a different world from the one the player walks.
            map.Configure(seed + patches.SeedOffset, patches, 1f, new Vector2(start.x, start.z));

            float metresPerPixel = areaSize / resolution;
            float originX = start.x - areaSize * .5f;
            float originZ = start.z - areaSize * .5f;
            var pixels = new Color[resolution * resolution];
            var elevations = new float[resolution * resolution];
            var counts = new int[System.Enum.GetValues(typeof(LandformKind)).Length];
            int rockGroundCount = 0;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = originX + (x + .5f) * metresPerPixel;
                    float worldZ = originZ + (z + .5f) * metresPerPixel;
                    LandformSample sample = map.SampleLandform(worldX, worldZ);
                    int index = z * resolution + x;
                    elevations[index] = sample.Elevation;
                    counts[(int)sample.Kind]++;
                    rockGroundCount += IsRockGround(map, worldX, worldZ) ? 1 : 0;
                    pixels[index] = ColorOf(map, sample, worldX, worldZ, showPatches);
                }
            }

            ApplyHillshade(pixels, elevations, resolution, metresPerPixel);
            MarkStart(pixels, resolution, (start.x - originX) / metresPerPixel, (start.z - originZ) / metresPerPixel);

            float total = resolution * resolution;
            float reachable = LandformReachability.ReachableOpenShare(
                map.IsBlockedAt, new Vector2(start.x, start.z), Mathf.RoundToInt(areaSize));
            statistics =
                $"plain {counts[(int)LandformKind.Plain] / total:P1}, basin {counts[(int)LandformKind.Basin] / total:P1}, " +
                $"ice lake {counts[(int)LandformKind.IceLake] / total:P1}, mountain {counts[(int)LandformKind.Mountain] / total:P1}, " +
                $"lava lake {counts[(int)LandformKind.LavaLake] / total:P1}, rock ground {rockGroundCount / total:P1}, " +
                $"reachable open ground {reachable:P1}";

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Color ColorOf(TerrainTileMap map, in LandformSample sample, float worldX, float worldZ,
            bool showPatches)
        {
            switch (sample.Kind)
            {
                case LandformKind.Mountain:
                    return MountainColor;
                case LandformKind.IceLake:
                    return IceLakeColor;
                case LandformKind.LavaLake:
                    return LavaLakeColor;
            }

            if (showPatches)
            {
                int layer = map.GetVisualLayerIndex(worldX, worldZ);
                if (layer != ClusteredTerrainLayout.BaseLayerIndex)
                {
                    return PatchColors[layer % PatchColors.Length];
                }
            }

            if (IsRockGround(map, worldX, worldZ))
            {
                return sample.Kind == LandformKind.Basin ? RockBasinColor : RockGroundColor;
            }

            return sample.Kind == LandformKind.Basin ? BasinColor : PlainColor;
        }

        private static bool IsRockGround(TerrainTileMap map, float worldX, float worldZ) =>
            map.Landforms != null && map.Landforms.Volcanic.IsRockGround(worldX, worldZ);

        private static void ApplyHillshade(Color[] pixels, float[] elevations, int resolution, float metresPerPixel)
        {
            // Relief is exaggerated because one elevation unit spans the whole lake-to-mountain range.
            float scale = 40f / metresPerPixel;
            var light = new Vector3(-.5f, .7f, .5f).normalized;
            var shaded = new Color[pixels.Length];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float left = elevations[z * resolution + Mathf.Max(0, x - 1)];
                    float right = elevations[z * resolution + Mathf.Min(resolution - 1, x + 1)];
                    float down = elevations[Mathf.Max(0, z - 1) * resolution + x];
                    float up = elevations[Mathf.Min(resolution - 1, z + 1) * resolution + x];
                    var normal = new Vector3(-(right - left) * scale, 2f, -(up - down) * scale).normalized;
                    float shade = Mathf.Clamp(Vector3.Dot(normal, light) / light.y * .9f + .1f, .45f, 1.3f);
                    Color color = pixels[z * resolution + x] * shade;
                    color.a = 1f;
                    shaded[z * resolution + x] = color;
                }
            }

            Array.Copy(shaded, pixels, pixels.Length);
        }

        private static void MarkStart(Color[] pixels, int resolution, float centerX, float centerZ)
        {
            const int radius = 3;
            for (int z = -radius; z <= radius; z++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int pixelX = Mathf.RoundToInt(centerX) + x;
                    int pixelZ = Mathf.RoundToInt(centerZ) + z;
                    if (pixelX >= 0 && pixelZ >= 0 && pixelX < resolution && pixelZ < resolution)
                    {
                        pixels[pixelZ * resolution + pixelX] = StartColor;
                    }
                }
            }
        }

        private static bool TryLoadSettings(out TerrainGenerationSettings generation, out TerrainPatchSettings patches,
            out string error)
        {
            generation = AssetDatabase.LoadAssetAtPath<TerrainGenerationSettings>(TerrainSettingsPath);
            patches = AssetDatabase.LoadAssetAtPath<TerrainPatchSettings>(TerrainPatchSettingsPath);
            if (generation == null || patches == null)
            {
                error = $"Missing '{TerrainSettingsPath}' or '{TerrainPatchSettingsPath}'. Run Planet Survival setup first.";
                return false;
            }

            if (patches.Landforms == null)
            {
                error = $"'{TerrainPatchSettingsPath}' has no landform settings. Run Planet Survival setup first.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string ReadArgument(string[] arguments, string name)
        {
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }
    }
}
