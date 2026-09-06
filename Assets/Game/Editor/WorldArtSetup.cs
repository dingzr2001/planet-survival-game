using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.World.Generation;
using UnityEditor;
using UnityEngine;

namespace PlanetSurvival.Editor
{
    public static class WorldArtSetup
    {
        private const string ConfigurationDirectory = "Assets/Game/Configuration";
        private const string VisualSettingsPath = ConfigurationDirectory + "/DefaultWorldVisuals.asset";
        private const string GroundPath = "Assets/Game/Art/World/Ground/MartianRegolith.png";
        private const string PlayerPath = "Assets/Game/Art/World/Characters/Explorer.png";
        private const string PlayerWalkPath = "Assets/Game/Art/World/Characters/ExplorerWalkDirectional8.png";
        private const string HorizonPath = "Assets/Game/Resources/World/MartianHorizon.png";
        private const string ResourceDirectory = "Assets/Game/Art/World/Resources";
        private const int PlayerDirectionCount = 4;
        private const int PlayerFramesPerDirection = 8;
        private const byte OpaqueAlphaThreshold = 128;

        [MenuItem("Planet Survival/Setup World Art")]
        public static void CreateOrUpdate()
        {
            WorldVisualSettings settings = GetOrCreateWorldVisualSettings();
            int assigned = AssignResourceSprites();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"World art ready at '{AssetDatabase.GetAssetPath(settings)}'; {assigned} resource sprite(s) assigned.");
        }

        [InitializeOnLoadMethod]
        private static void ScheduleMissingPlayerAnimationSetup()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += EnsurePlayerAnimationIsConfigured;
            }
        }

        private static void EnsurePlayerAnimationIsConfigured()
        {
            if (AssetImporter.GetAtPath(PlayerWalkPath) == null)
            {
                return;
            }

            WorldVisualSettings settings = AssetDatabase.LoadAssetAtPath<WorldVisualSettings>(VisualSettingsPath);
            if (settings != null
                && settings.PlayerAnimationSheet != null
                && AssetDatabase.GetAssetPath(settings.PlayerAnimationSheet) == PlayerWalkPath
                && settings.PlayerFramesPerDirection == PlayerFramesPerDirection
                && settings.PlayerFrameRects.Count == PlayerDirectionCount * PlayerFramesPerDirection
                && settings.PlayerFramePivots.Count == PlayerDirectionCount * PlayerFramesPerDirection)
            {
                return;
            }

            GetOrCreateWorldVisualSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Configured the Explorer four-direction walk animation.");
        }

        public static WorldVisualSettings GetOrCreateWorldVisualSettings()
        {
            Texture2D ground = ImportGround();
            Texture2D playerAnimation = ImportPlayerAnimation(out Rect[] frameRects, out Vector2[] framePivots);
            Sprite player = ImportSprite(PlayerPath, 2048);
            Sprite horizon = ImportSprite(HorizonPath, 4096, false, TextureImporterCompression.Uncompressed);
            WorldVisualSettings settings = AssetDatabase.LoadAssetAtPath<WorldVisualSettings>(VisualSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WorldVisualSettings>();
                settings.name = "Default World Visuals";
                AssetDatabase.CreateAsset(settings, VisualSettingsPath);
            }

            settings.Configure(ground, player, horizon);
            settings.ConfigurePlayerAnimation(
                playerAnimation, PlayerFramesPerDirection, frameRects, framePivots);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static Texture2D ImportPlayerAnimation(out Rect[] frameRects, out Vector2[] framePivots)
        {
            frameRects = System.Array.Empty<Rect>();
            framePivots = System.Array.Empty<Vector2>();
            if (AssetImporter.GetAtPath(PlayerWalkPath) is not TextureImporter importer)
            {
                Debug.LogWarning($"Player animation sheet '{PlayerWalkPath}' was not found.");
                return null;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
            Texture2D readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerWalkPath);
            CalculatePlayerFrameLayout(readableTexture, out frameRects, out framePivots);

            importer.isReadable = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerWalkPath);
        }

        private static void CalculatePlayerFrameLayout(Texture2D texture,
            out Rect[] frameRects, out Vector2[] framePivots)
        {
            int frameCount = PlayerDirectionCount * PlayerFramesPerDirection;
            frameRects = new Rect[frameCount];
            framePivots = new Vector2[frameCount];
            if (texture == null
                || texture.width % PlayerFramesPerDirection != 0
                || texture.height % PlayerDirectionCount != 0)
            {
                return;
            }

            int rowHeight = texture.height / PlayerDirectionCount;
            int cellWidth = texture.width / PlayerFramesPerDirection;
            Color32[] pixels = texture.GetPixels32();

            for (int sourceRow = 0; sourceRow < PlayerDirectionCount; sourceRow++)
            {
                int rowBottom = (PlayerDirectionCount - 1 - sourceRow) * rowHeight;
                float anchorTotal = 0f;
                float footTotal = 0f;
                for (int column = 0; column < PlayerFramesPerDirection; column++)
                {
                    int cellLeft = column * cellWidth;
                    FrameAnalysis analysis = AnalyzeFrame(
                        pixels, texture.width, rowBottom, rowHeight, cellLeft, cellLeft + cellWidth);
                    anchorTotal += analysis.AnchorX - cellLeft;
                    footTotal += analysis.MinY - rowBottom;
                }

                // Every frame in one direction shares an anchor. Limb motion must not
                // move the rendered astronaut relative to its gameplay transform.
                Vector2 rowPivot = new(
                    Mathf.Clamp01(anchorTotal / PlayerFramesPerDirection / cellWidth),
                    Mathf.Clamp01(footTotal / PlayerFramesPerDirection / rowHeight));
                for (int column = 0; column < PlayerFramesPerDirection; column++)
                {
                    int index = sourceRow * PlayerFramesPerDirection + column;
                    frameRects[index] = new Rect(column * cellWidth, rowBottom, cellWidth, rowHeight);
                    framePivots[index] = rowPivot;
                }
            }
        }

        private static FrameAnalysis AnalyzeFrame(Color32[] pixels, int textureWidth,
            int rowBottom, int rowHeight, int spanStart, int spanEnd)
        {
            int minX = spanStart;
            int maxX = spanEnd - 1;
            int minY = rowBottom + rowHeight - 1;
            int maxY = rowBottom;
            for (int y = rowBottom; y < rowBottom + rowHeight; y++)
            {
                for (int x = spanStart; x < spanEnd; x++)
                {
                    if (pixels[y * textureWidth + x].a < OpaqueAlphaThreshold)
                    {
                        continue;
                    }

                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            int bodyHeight = Mathf.Max(1, maxY - minY + 1);
            int torsoBottom = minY + Mathf.RoundToInt(bodyHeight * .34f);
            int torsoTop = minY + Mathf.RoundToInt(bodyHeight * .70f);
            long xTotal = 0;
            int sampleCount = 0;
            for (int y = torsoBottom; y <= torsoTop; y++)
            {
                for (int x = spanStart; x < spanEnd; x++)
                {
                    if (pixels[y * textureWidth + x].a >= OpaqueAlphaThreshold)
                    {
                        xTotal += x;
                        sampleCount++;
                    }
                }
            }

            float anchorX = sampleCount > 0
                ? xTotal / (float)sampleCount
                : (minX + maxX) * .5f;
            return new FrameAnalysis(minY, anchorX);
        }

        private readonly struct FrameAnalysis
        {
            public FrameAnalysis(int minY, float anchorX)
            {
                MinY = minY;
                AnchorX = anchorX;
            }

            public int MinY { get; }
            public float AnchorX { get; }
        }

        public static int AssignResourceSprites()
        {
            int assigned = 0;
            assigned += Assign("RockNode", "Rock") ? 1 : 0;
            assigned += Assign("DebrisNode", "Debris") ? 1 : 0;
            assigned += Assign("PlantNode", "Plant") ? 1 : 0;
            return assigned;
        }

        private static bool Assign(string definitionName, string spriteName)
        {
            string definitionPath = $"{ConfigurationDirectory}/{definitionName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(definitionPath);
            Sprite sprite = ImportSprite($"{ResourceDirectory}/{spriteName}.png", 2048);
            if (definition == null || sprite == null)
            {
                Debug.LogWarning($"Could not connect world art '{spriteName}' to '{definitionName}'.");
                return false;
            }

            if (definition.WorldSprite == sprite)
            {
                return false;
            }

            definition.SetWorldSprite(sprite);
            EditorUtility.SetDirty(definition);
            return true;
        }

        private static Sprite ImportSprite(string path, int maxSize,
            bool mipmapEnabled = true,
            TextureImporterCompression compression = TextureImporterCompression.CompressedHQ)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"World sprite '{path}' was not found.");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = mipmapEnabled;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = compression;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D ImportGround()
        {
            if (AssetImporter.GetAtPath(GroundPath) is not TextureImporter importer)
            {
                Debug.LogWarning($"Ground texture '{GroundPath}' was not found.");
                return null;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 16;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GroundPath);
        }
    }
}
