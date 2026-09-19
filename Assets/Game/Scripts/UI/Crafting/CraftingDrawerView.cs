using System.Collections.Generic;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Runtime;
using PlanetSurvival.Crafting.Application;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Crafting.Domain;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.Crafting
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// A compact survival-game drawer anchored to the left edge. Crafting produces backpack items
    /// immediately; building hands a selected structure to the world placement controller.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftingDrawerView : MonoBehaviour
    {
        private enum DrawerTab
        {
            Crafting,
            Building
        }

        private const KeyCode CraftingKey = KeyCode.C;
        private const KeyCode BuildingKey = KeyCode.B;
        private const float DrawerWidth = 460f;
        private const float DrawerTop = 88f;
        private const float DrawerBottomMargin = 116f;
        private const float TabWidth = 76f;
        private const float TabHeight = 44f;
        private const float RowHeight = 104f;
        private const float ProductIconSize = 62f;
        private const float MaterialIconSize = 25f;
        private const float AnimationSpeed = 7.5f;

        private BuildingPlacementController _buildingController;
        private PlayerInventory _playerInventory;
        private CraftingCatalog _craftingCatalog;
        private CraftingService _craftingService;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private InventoryView _inventoryView;
        private DrawerTab _activeTab;
        private BuildableDefinition _pendingBuildable;
        private CraftingRecipe _pendingRecipe;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;
        private bool _controlsLocked;
        private float _openProgress;
        private Vector2 _craftingScroll;
        private Vector2 _buildingScroll;
        private string _feedback = string.Empty;

        private GUIStyle _headerStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _descriptionStyle;
        private GUIStyle _materialStyle;
        private GUIStyle _materialMissingStyle;
        private GUIStyle _feedbackStyle;
        private GUIStyle _emptyStyle;

        public bool IsOpen { get; private set; }

        public void Bind(
            BuildingPlacementController buildingController,
            PlayerInventory playerInventory,
            CraftingCatalog craftingCatalog,
            PlanarPlayerMotor playerMotor = null,
            PlayerInteractor playerInteractor = null)
        {
            _buildingController = buildingController;
            _playerInventory = playerInventory;
            _craftingCatalog = craftingCatalog;
            _playerMotor = playerMotor;
            _playerInteractor = playerInteractor;
            _craftingService = playerInventory != null
                ? new CraftingService(playerInventory.Inventory, playerInventory.QuickBar)
                : null;
        }

        public void OpenCrafting()
        {
            Open(DrawerTab.Crafting);
        }

        public void OpenBuilding()
        {
            Open(DrawerTab.Building);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            ReleaseControls();
        }

        public CraftingResult Craft(CraftingRecipe recipe)
        {
            if (_craftingService == null || _craftingCatalog == null)
            {
                return CraftingResult.Fail(CraftingFailure.InvalidRecipe, "Handheld crafting is unavailable.");
            }

            CraftingResult result = _craftingService.Craft(recipe, _craftingCatalog.Conditions);
            _feedback = result.Succeeded
                ? $"Crafted {recipe.DisplayName}."
                : result.Message;
            return result;
        }

        private void Open(DrawerTab tab)
        {
            if (tab == DrawerTab.Crafting && (_craftingCatalog == null || _craftingService == null))
            {
                return;
            }

            if (tab == DrawerTab.Building && _buildingController == null)
            {
                return;
            }

            if (IsOpen && _activeTab == tab)
            {
                Close();
                return;
            }

            _buildingController?.CancelPlacement();
            _activeTab = tab;
            _feedback = tab == DrawerTab.Crafting
                ? "Choose an item to make from backpack materials."
                : "Choose a structure, then place it in the world.";
            if (!IsOpen)
            {
                LockControls();
                IsOpen = true;
            }
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            _openProgress = Mathf.MoveTowards(
                _openProgress, target, AnimationSpeed * Time.unscaledDeltaTime);

            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && IsOpen)
            {
                Close();
            }
            else if (Input.GetKeyDown(CraftingKey))
            {
                OpenCrafting();
            }
            else if (Input.GetKeyDown(BuildingKey))
            {
                if (_buildingController != null && _buildingController.IsPlacing)
                {
                    _buildingController.CancelPlacement();
                }
                else
                {
                    OpenBuilding();
                }
            }
        }

        private void OnDisable()
        {
            IsOpen = false;
            _openProgress = 0f;
            ReleaseControls();
        }

        private void OnDestroy()
        {
            ReleaseControls();
        }

        private void OnGUI()
        {
            if (_playerInventory == null)
            {
                return;
            }

            EnsureStyles();
            float height = Mathf.Max(260f, Screen.height - DrawerTop - DrawerBottomMargin);
            float easedProgress = 1f - Mathf.Pow(1f - _openProgress, 3f);
            float drawerX = Mathf.Lerp(-DrawerWidth, 0f, easedProgress);
            var drawerRect = new Rect(drawerX, DrawerTop, DrawerWidth, height);

            if (_openProgress > 0.001f)
            {
                DrawDrawer(drawerRect);
            }

            DrawTabs(drawerRect.xMax, DrawerTop + 18f);
            ApplyPendingActions();
        }

        private void DrawTabs(float x, float y)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = Time.timeScale > 0f && _craftingCatalog != null;
            if (GUI.Button(new Rect(x, y, TabWidth, TabHeight), "CRAFT\n[C]",
                    IsOpen && _activeTab == DrawerTab.Crafting ? _activeTabStyle : _tabStyle))
            {
                OpenCrafting();
            }

            GUI.enabled = Time.timeScale > 0f && _buildingController != null;
            if (GUI.Button(new Rect(x, y + TabHeight + 6f, TabWidth, TabHeight), "BUILD\n[B]",
                    IsOpen && _activeTab == DrawerTab.Building ? _activeTabStyle : _tabStyle))
            {
                OpenBuilding();
            }

            GUI.enabled = previousEnabled;
        }

        private void DrawDrawer(Rect rect)
        {
            PanelBackground.Draw(rect, new Color(.035f, .045f, .052f, .97f));
            GUILayout.BeginArea(new Rect(rect.x + 16f, rect.y + 14f, rect.width - 30f, rect.height - 26f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(_activeTab == DrawerTab.Crafting ? "FIELD CRAFTING" : "CONSTRUCTION", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", GUILayout.Width(30f), GUILayout.Height(26f)))
            {
                Close();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            if (_activeTab == DrawerTab.Crafting)
            {
                DrawCraftingList(rect.height - 98f);
            }
            else
            {
                DrawBuildingList(rect.height - 98f);
            }

            GUILayout.Space(5f);
            GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _feedback : GUI.tooltip, _feedbackStyle);
            GUILayout.EndArea();
        }

        private void DrawCraftingList(float height)
        {
            _craftingScroll = GUILayout.BeginScrollView(_craftingScroll, GUI.skin.box,
                GUILayout.Height(Mathf.Max(170f, height)));
            IReadOnlyList<CraftingRecipe> recipes = _craftingCatalog.Recipes;
            if (recipes.Count == 0)
            {
                GUILayout.Label("No handheld recipes are available.", _emptyStyle);
            }

            for (int i = 0; i < recipes.Count; i++)
            {
                CraftingRecipe recipe = recipes[i];
                if (recipe == null || !recipe.IsValid(out string _))
                {
                    continue;
                }

                CraftingResult availability = _craftingService.CanCraft(recipe, _craftingCatalog.Conditions);
                DrawRecipeRow(recipe, availability);
            }

            GUILayout.EndScrollView();
        }

        private void DrawRecipeRow(CraftingRecipe recipe, CraftingResult availability)
        {
            CraftingItemAmount output = recipe.Outputs[0];
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(RowHeight));
            Color previousColor = GUI.color;
            GUI.color = availability.Succeeded ? Color.white : new Color(1f, 1f, 1f, .46f);
            SpriteIcon.Draw(ReserveIcon(ProductIconSize), output.Item.Icon);
            GUI.color = previousColor;

            GUILayout.Space(8f);
            GUILayout.BeginVertical();
            string outputAmount = output.Quantity > 1 ? $"  ×{output.Quantity}" : string.Empty;
            GUILayout.Label(recipe.DisplayName + outputAmount, _nameStyle);
            if (!string.IsNullOrWhiteSpace(output.Item.Description))
            {
                GUILayout.Label(output.Item.Description, _descriptionStyle, GUILayout.MaxWidth(238f));
            }
            DrawMaterials(recipe.Inputs);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUI.enabled = availability.Succeeded;
            if (GUILayout.Button(new GUIContent("MAKE", availability.Message),
                    GUILayout.Width(68f), GUILayout.Height(34f)))
            {
                _pendingRecipe = recipe;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawBuildingList(float height)
        {
            _buildingScroll = GUILayout.BeginScrollView(_buildingScroll, GUI.skin.box,
                GUILayout.Height(Mathf.Max(170f, height)));
            BuildingCatalog catalog = _buildingController.Catalog;
            if (catalog == null || catalog.Buildables.Count == 0)
            {
                GUILayout.Label("No structures are available.", _emptyStyle);
            }
            else
            {
                IReadOnlyList<BuildableDefinition> buildables = catalog.Buildables;
                for (int i = 0; i < buildables.Count; i++)
                {
                    BuildableDefinition buildable = buildables[i];
                    if (buildable == null || !buildable.IsValid(out string _))
                    {
                        continue;
                    }

                    DrawBuildableRow(buildable, _buildingController.Service.CanAfford(buildable));
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawBuildableRow(BuildableDefinition buildable, bool affordable)
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(RowHeight));
            Color previousColor = GUI.color;
            GUI.color = affordable ? Color.white : new Color(1f, 1f, 1f, .46f);
            SpriteIcon.Draw(ReserveIcon(ProductIconSize), buildable.MenuIcon);
            GUI.color = previousColor;

            GUILayout.Space(8f);
            GUILayout.BeginVertical();
            GUILayout.Label(buildable.DisplayName, _nameStyle);
            if (!string.IsNullOrWhiteSpace(buildable.Description))
            {
                GUILayout.Label(buildable.Description, _descriptionStyle, GUILayout.MaxWidth(238f));
            }
            DrawMaterials(buildable.Cost);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(72f));
            GUILayout.Label($"{buildable.Footprint.x}×{buildable.Footprint.y}\n{buildable.BuildSeconds:0}s",
                _descriptionStyle);
            GUI.enabled = affordable;
            if (GUILayout.Button("PLACE", GUILayout.Height(34f)))
            {
                _pendingBuildable = buildable;
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawMaterials(IReadOnlyList<CraftingItemAmount> materials)
        {
            InventoryModel inventory = _playerInventory.Inventory;
            GUILayout.BeginHorizontal(GUILayout.Height(MaterialIconSize + 4f));
            for (int i = 0; i < materials.Count; i++)
            {
                CraftingItemAmount material = materials[i];
                int owned = inventory.GetQuantity(material.Item.ItemId);
                Rect icon = ReserveIcon(MaterialIconSize);
                GUI.Label(icon, new GUIContent(string.Empty, material.Item.DisplayName));

                Color previousColor = GUI.color;
                GUI.color = owned >= material.Quantity ? Color.white : new Color(1f, 1f, 1f, .42f);
                SpriteIcon.Draw(icon, material.Item.Icon);
                GUI.color = previousColor;

                GUILayout.Space(3f);
                GUILayout.Label($"{owned}/{material.Quantity}",
                    owned >= material.Quantity ? _materialStyle : _materialMissingStyle,
                    GUILayout.Height(MaterialIconSize));
                GUILayout.Space(10f);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void ApplyPendingActions()
        {
            CraftingRecipe recipe = _pendingRecipe;
            BuildableDefinition buildable = _pendingBuildable;
            _pendingRecipe = null;
            _pendingBuildable = null;

            if (recipe != null)
            {
                Craft(recipe);
            }

            if (buildable != null)
            {
                Close();
                _openProgress = 0f;
                _buildingController.BeginPlacement(buildable);
            }
        }

        private void LockControls()
        {
            if (_controlsLocked)
            {
                return;
            }

            _inventoryView = GetComponent<InventoryView>();
            _restoreMotor = _playerMotor != null && _playerMotor.enabled;
            _restoreInteractor = _playerInteractor != null && _playerInteractor.enabled;
            _restoreInventoryView = _inventoryView != null && _inventoryView.enabled;
            if (_playerMotor != null) _playerMotor.enabled = false;
            if (_playerInteractor != null) _playerInteractor.enabled = false;
            if (_inventoryView != null) _inventoryView.enabled = false;
            _controlsLocked = true;
        }

        private void ReleaseControls()
        {
            if (!_controlsLocked)
            {
                return;
            }

            if (_playerMotor != null) _playerMotor.enabled = _restoreMotor;
            if (_playerInteractor != null) _playerInteractor.enabled = _restoreInteractor;
            if (_inventoryView != null) _inventoryView.enabled = _restoreInventoryView;
            _inventoryView = null;
            _restoreMotor = false;
            _restoreInteractor = false;
            _restoreInventoryView = false;
            _controlsLocked = false;
        }

        private static Rect ReserveIcon(float size)
        {
            return GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(.74f, .91f, 1f);
            _tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _activeTabStyle = new GUIStyle(_tabStyle);
            _activeTabStyle.normal.textColor = new Color(.65f, .92f, 1f);
            _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _nameStyle.normal.textColor = Color.white;
            _descriptionStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _descriptionStyle.normal.textColor = new Color(.74f, .76f, .74f);
            _materialStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _materialStyle.normal.textColor = new Color(.8f, .9f, .78f);
            _materialMissingStyle = new GUIStyle(_materialStyle);
            _materialMissingStyle.normal.textColor = new Color(.96f, .46f, .38f);
            _feedbackStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            _feedbackStyle.normal.textColor = new Color(.68f, .76f, .78f);
            _emptyStyle = new GUIStyle(_feedbackStyle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
