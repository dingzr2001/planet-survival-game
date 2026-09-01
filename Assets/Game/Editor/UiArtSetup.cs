using System.IO;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.UI.Inventory;
using UnityEditor;
using UnityEngine;

namespace PlanetSurvival.Editor
{
    /// <summary>
    /// Applies the import settings the UI art depends on and wires it into configuration assets.
    /// Asset paths are referenced here only; runtime code always reaches the art through the
    /// generated <see cref="InventorySkin"/> and each <see cref="ItemDefinition"/>.
    /// </summary>
    public static class UiArtSetup
    {
        private const string ConfigurationDirectory = "Assets/Game/Configuration";
        private const string ItemIconDirectory = "Assets/Game/Art/UI/Icons/Items";
        private const string SlotBackgroundPath = "Assets/Game/Art/UI/HUD/InventoryCell.png";
        private const string InventorySkinPath = ConfigurationDirectory + "/DefaultInventorySkin.asset";
        private const string IconPropertyPath = "_icon";

        private const int IconMaxSize = 256;

        // Nine-sliced corners are drawn at their source-pixel size, so the frame must import smaller
        // than the narrowest slot (64 px) or opposing corners would overlap and tear the frame.
        private const int SlotBackgroundMaxSize = 128;
        private const int SlotBorderPixels = 10;
        private const float IconPadding = 7f;

        [MenuItem("Planet Survival/Setup UI Art")]
        public static void CreateOrUpdate()
        {
            InventorySkin skin = GetOrCreateInventorySkin();
            int assigned = AssignItemIcons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"UI art ready: inventory skin at '{AssetDatabase.GetAssetPath(skin)}', {assigned} item icon(s) assigned.");
        }

        public static InventorySkin GetOrCreateInventorySkin()
        {
            Texture2D background = ImportSlotBackground();
            InventorySkin skin = AssetDatabase.LoadAssetAtPath<InventorySkin>(InventorySkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<InventorySkin>();
                AssetDatabase.CreateAsset(skin, InventorySkinPath);
            }

            skin.Configure(background, SlotBorderPixels, IconPadding);
            EditorUtility.SetDirty(skin);
            return skin;
        }

        /// <summary>
        /// Assigns every item the icon that shares its asset name, e.g. 'RawStone.asset' takes
        /// 'RawStone.png'. Items without a matching icon keep whatever they already reference.
        /// </summary>
        public static int AssignItemIcons()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ItemDefinition)}", new[] { ConfigurationDirectory });
            int assigned = 0;

            foreach (string guid in guids)
            {
                string itemPath = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                if (item == null)
                {
                    continue;
                }

                string iconPath = $"{ItemIconDirectory}/{Path.GetFileNameWithoutExtension(itemPath)}.png";
                if (!ConfigureTextureImport(iconPath, TextureImporterType.Sprite, IconMaxSize, false))
                {
                    continue;
                }

                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (icon == null)
                {
                    Debug.LogWarning($"'{iconPath}' did not import as a sprite; '{itemPath}' keeps its current icon.");
                    continue;
                }

                if (TryAssignIcon(item, icon))
                {
                    assigned++;
                }
            }

            return assigned;
        }

        private static Texture2D ImportSlotBackground()
        {
            if (!ConfigureTextureImport(SlotBackgroundPath, TextureImporterType.GUI, SlotBackgroundMaxSize, true))
            {
                return null;
            }

            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(SlotBackgroundPath);
            if (background == null)
            {
                Debug.LogWarning($"Slot background '{SlotBackgroundPath}' could not be loaded; slots will use the built-in GUI skin.");
            }

            return background;
        }

        /// <summary>Returns false when the texture is missing, so callers can skip it without throwing.</summary>
        private static bool ConfigureTextureImport(string path, TextureImporterType type, int maxSize, bool required)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                if (required)
                {
                    Debug.LogWarning($"No texture found at '{path}'.");
                }

                return false;
            }

            bool matchesTarget = importer.textureType == type
                                 && !importer.mipmapEnabled
                                 && importer.maxTextureSize == maxSize
                                 && importer.wrapMode == TextureWrapMode.Clamp
                                 && importer.alphaIsTransparency;
            if (matchesTarget)
            {
                return true;
            }

            importer.textureType = type;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            if (type == TextureImporterType.Sprite)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }

            importer.SaveAndReimport();
            return true;
        }

        private static bool TryAssignIcon(ItemDefinition item, Sprite icon)
        {
            var serialized = new SerializedObject(item);
            SerializedProperty property = serialized.FindProperty(IconPropertyPath);
            if (property == null)
            {
                Debug.LogError($"{nameof(ItemDefinition)} no longer has a '{IconPropertyPath}' field; update {nameof(UiArtSetup)}.");
                return false;
            }

            if (property.objectReferenceValue == icon)
            {
                return false;
            }

            property.objectReferenceValue = icon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
