using System;
using System.IO;
using PlanetSurvival.Player.Animation;
using PlanetSurvival.World.Generation;
using UnityEditor;
using UnityEngine;

namespace PlanetSurvival.Editor
{
    /// <summary>
    /// Converts the reviewed ImageGen source sheets into tightly cropped transparent sprites and wires
    /// them into one reusable rig definition. Keeping this deterministic makes regenerating one direction safe.
    /// </summary>
    public static class ModularCharacterArtSetup
    {
        public const string RigDefinitionPath = "Assets/Game/Configuration/ExplorerModularRig.asset";

        private const string SourceDirectory = "ArtSource/Characters/ExplorerModular";
        private const string OutputDirectory = "Assets/Game/Art/World/Characters/Modular";
        private const string WorldVisualSettingsPath = "Assets/Game/Configuration/DefaultWorldVisuals.asset";
        private const int Padding = 8;

        private static readonly DirectionSource[] Directions =
        {
            new("Down", "LimbsDown.png", "TorsoDown.png"),
            new("Left", "LimbsLeft.png", "TorsoLeft.png"),
            new("Right", "LimbsRight.png", "TorsoRight.png"),
            new("Up", "LimbsUp.png", "TorsoUp.png")
        };

        private static readonly PartCell[] LimbParts =
        {
            new("UpperArm", 0, 1),
            new("Forearm", 1, 1),
            new("Hand", 2, 1),
            new("Thigh", 0, 0),
            new("LowerLeg", 1, 0),
            new("Boot", 2, 0)
        };

        [MenuItem("Planet Survival/Setup Modular Character Art")]
        public static void CreateOrUpdate()
        {
            Directory.CreateDirectory(OutputDirectory);
            for (int i = 0; i < Directions.Length; i++)
            {
                ProcessDirection(Directions[i]);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            DirectionalRigSprites down = ImportDirection("Down");
            DirectionalRigSprites left = ImportDirection("Left");
            DirectionalRigSprites right = ImportDirection("Right");
            DirectionalRigSprites up = ImportDirection("Up");

            ModularPlayerRigDefinition definition =
                AssetDatabase.LoadAssetAtPath<ModularPlayerRigDefinition>(RigDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ModularPlayerRigDefinition>();
                definition.name = "Explorer Modular Rig";
                AssetDatabase.CreateAsset(definition, RigDefinitionPath);
            }

            definition.Configure(down, left, right, up);
            EditorUtility.SetDirty(definition);

            WorldVisualSettings settings =
                AssetDatabase.LoadAssetAtPath<WorldVisualSettings>(WorldVisualSettingsPath)
                ?? WorldArtSetup.GetOrCreateWorldVisualSettings();
            settings.ConfigureModularPlayerRig(definition);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"Modular explorer art and rig are ready at '{RigDefinitionPath}'.");
        }

        [MenuItem("Planet Survival/Capture Modular Character Preview")]
        public static void CapturePreview()
        {
            WorldVisualSettings settings =
                AssetDatabase.LoadAssetAtPath<WorldVisualSettings>(WorldVisualSettingsPath);
            if (settings == null || settings.ModularPlayerRig == null)
            {
                throw new InvalidOperationException("Run Setup Modular Character Art before capturing a preview.");
            }

            var player = new GameObject("Modular Character Preview");
            PlayerAnimationController animation = PlayerAnimationController.Create(
                player.transform, settings.PlayerSprite, settings.PlayerHeight,
                settings.PlayerAnimationSheet, settings.PlayerFramesPerDirection,
                settings.PlayerFrameRects, settings.PlayerFramePivots);
            animation.ConfigureTools(settings.PlayerToolAnimations);
            animation.ConfigureModularRig(settings.ModularPlayerRig);

            var cameraObject = new GameObject("Modular Character Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.32f, .29f, .26f);
            camera.transform.position = new Vector3(0f, .9f, -10f);

            animation.SetMovement(Vector2.down);
            animation.Advance(.18f);
            RenderPreview(camera, "/tmp/explorer-modular-down.png");
            camera.orthographicSize = 9.5f;
            RenderPreview(camera, "/tmp/explorer-modular-game-scale.png");
            camera.orthographicSize = 1.15f;
            animation.SetMovement(Vector2.left);
            RenderPreview(camera, "/tmp/explorer-modular-left.png");
            animation.SetMovement(Vector2.right);
            RenderPreview(camera, "/tmp/explorer-modular-right.png");
            animation.SetMovement(Vector2.up);
            RenderPreview(camera, "/tmp/explorer-modular-up.png");
            animation.SetMovement(Vector2.down);
            if (animation.TryPlayToolAction("pickaxe"))
            {
                animation.Advance(.13f);
                RenderPreview(camera, "/tmp/explorer-modular-mining-windup.png");
                animation.Advance(.17f);
                RenderPreview(camera, "/tmp/explorer-modular-mining-impact.png");
                animation.Advance(.19f);
                RenderPreview(camera, "/tmp/explorer-modular-mining-recover.png");
            }

            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            Debug.Log("Captured four-direction modular explorer and three mining poses in /tmp.");
        }

        private static void RenderPreview(Camera camera, string path)
        {
            var renderTexture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
            var output = new Texture2D(768, 768, TextureFormat.RGBA32, false, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            output.ReadPixels(new Rect(0f, 0f, 768f, 768f), 0, 0, false);
            output.Apply(false, false);
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(output));
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        private static void ProcessDirection(DirectionSource direction)
        {
            Texture2D torso = LoadSource(direction.TorsoFile);
            WriteCutout(torso, new RectInt(0, 0, torso.width, torso.height),
                OutputPath(direction.Name, "Torso"));
            UnityEngine.Object.DestroyImmediate(torso);

            Texture2D limbs = LoadSource(direction.LimbFile);
            if (limbs.width % 3 != 0 || limbs.height % 2 != 0)
            {
                UnityEngine.Object.DestroyImmediate(limbs);
                throw new InvalidOperationException(
                    $"Modular limb sheet '{direction.LimbFile}' must use an exact 3 by 2 grid.");
            }

            int cellWidth = limbs.width / 3;
            int cellHeight = limbs.height / 2;
            for (int i = 0; i < LimbParts.Length; i++)
            {
                PartCell part = LimbParts[i];
                WriteCutout(limbs,
                    new RectInt(part.Column * cellWidth, part.Row * cellHeight, cellWidth, cellHeight),
                    OutputPath(direction.Name, part.Name));
            }

            UnityEngine.Object.DestroyImmediate(limbs);
        }

        private static Texture2D LoadSource(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(SourceDirectory, fileName));
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Modular character source was not found: {path}", path);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException($"Could not decode modular character source '{path}'.");
            }

            return texture;
        }

        private static void WriteCutout(Texture2D source, RectInt rect, string outputPath)
        {
            Color32[] sourcePixels = source.GetPixels32();
            var pixels = new Color32[rect.width * rect.height];
            for (int y = 0; y < rect.height; y++)
            {
                Array.Copy(sourcePixels, (rect.y + y) * source.width + rect.x,
                    pixels, y * rect.width, rect.width);
            }

            int minX = rect.width;
            int minY = rect.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < rect.height; y++)
            {
                for (int x = 0; x < rect.width; x++)
                {
                    int index = y * rect.width + x;
                    pixels[index] = RemoveChromaKey(pixels[index]);
                    if (pixels[index].a <= 3)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                throw new InvalidOperationException($"No visible pixels were found while creating '{outputPath}'.");
            }

            int contentWidth = maxX - minX + 1;
            int contentHeight = maxY - minY + 1;
            var output = new Texture2D(contentWidth + Padding * 2, contentHeight + Padding * 2,
                TextureFormat.RGBA32, false, false);
            var outputPixels = new Color32[output.width * output.height];
            for (int y = 0; y < contentHeight; y++)
            {
                Array.Copy(pixels, (minY + y) * rect.width + minX,
                    outputPixels, (y + Padding) * output.width + Padding, contentWidth);
            }

            output.SetPixels32(outputPixels);
            output.Apply(false, false);
            File.WriteAllBytes(outputPath, ImageConversion.EncodeToPNG(output));
            UnityEngine.Object.DestroyImmediate(output);
        }

        private static Color32 RemoveChromaKey(Color32 pixel)
        {
            if (pixel.a == 0)
            {
                return pixel;
            }

            float red = pixel.r / 255f;
            float green = pixel.g / 255f;
            float blue = pixel.b / 255f;
            float dominance = Mathf.Min(red, blue) - green;
            if (dominance <= .05f)
            {
                return pixel;
            }

            float keyAlpha = Mathf.Clamp01((.95f - dominance) / .9f);
            float alpha = pixel.a / 255f * keyAlpha;
            if (alpha <= .01f)
            {
                return new Color32(0, 0, 0, 0);
            }

            // Reverse the blend against #FF00FF to avoid a pink halo on antialiased suit edges.
            red = Mathf.Clamp01((red - (1f - keyAlpha)) / keyAlpha);
            green = Mathf.Clamp01(green / keyAlpha);
            blue = Mathf.Clamp01((blue - (1f - keyAlpha)) / keyAlpha);
            // Chroma subtraction can amplify a tiny green component into a bright fringe. Limiting it
            // to the strongest surviving red/blue channel removes spill without damaging cyan lights.
            green = Mathf.Min(green, Mathf.Max(red, blue));
            return new Color(red, green, blue, alpha);
        }

        private static DirectionalRigSprites ImportDirection(string direction)
        {
            return new DirectionalRigSprites(
                ImportSprite(OutputPath(direction, "Torso"), new Vector2(.5f, .02f)),
                ImportSprite(OutputPath(direction, "UpperArm"), new Vector2(.5f, .9f)),
                ImportSprite(OutputPath(direction, "Forearm"), new Vector2(.5f, .9f)),
                ImportSprite(OutputPath(direction, "Hand"), new Vector2(.5f, .75f)),
                ImportSprite(OutputPath(direction, "Thigh"), new Vector2(.5f, .9f)),
                ImportSprite(OutputPath(direction, "LowerLeg"), new Vector2(.5f, .9f)),
                ImportSprite(OutputPath(direction, "Boot"), new Vector2(.5f, .75f)));
        }

        private static Sprite ImportSprite(string path, Vector2 pivot)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                throw new InvalidOperationException($"Generated modular sprite '{path}' could not be imported.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string OutputPath(string direction, string part)
        {
            return $"{OutputDirectory}/Explorer{direction}{part}.png";
        }

        private readonly struct DirectionSource
        {
            public DirectionSource(string name, string limbFile, string torsoFile)
            {
                Name = name;
                LimbFile = limbFile;
                TorsoFile = torsoFile;
            }

            public string Name { get; }
            public string LimbFile { get; }
            public string TorsoFile { get; }
        }

        private readonly struct PartCell
        {
            public PartCell(string name, int column, int row)
            {
                Name = name;
                Column = column;
                Row = row;
            }

            public string Name { get; }
            public int Column { get; }
            public int Row { get; }
        }
    }
}
