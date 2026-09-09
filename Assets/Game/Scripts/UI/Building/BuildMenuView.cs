using System.Collections.Generic;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Building.Runtime;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.Building
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// The build panel, opened with B. It lists everything the catalog offers, with what the backpack can
    /// afford on top; picking a structure closes the panel and hands it to the placement controller, so the
    /// player chooses the spot with the world in full view rather than through a menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildMenuView : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.B;
        private const float PanelWidth = 760f;
        private const float PanelHeight = 560f;
        private const float RowHeight = 76f;
        private const float IconSize = 54f;
        private const float CostIconSize = 24f;
        private const float HintHeight = 34f;

        private readonly List<BuildableDefinition> _affordable = new();
        private readonly List<BuildableDefinition> _unaffordable = new();

        private BuildingPlacementController _controller;
        private PlayerInventory _playerInventory;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private InventoryView _inventoryView;
        private BuildableDefinition _pendingSelection;
        private bool _pendingClose;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;
        private string _feedback = string.Empty;
        private Vector2 _scroll;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _detailStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _costStyle;
        private GUIStyle _costShortStyle;
        private GUIStyle _hintStyle;

        public bool IsOpen { get; private set; }

        public void Bind(BuildingPlacementController controller, PlayerInventory playerInventory,
            PlanarPlayerMotor playerMotor = null, PlayerInteractor playerInteractor = null)
        {
            _controller = controller;
            _playerInventory = playerInventory;
            _playerMotor = playerMotor;
            _playerInteractor = playerInteractor;
        }

        public void Open()
        {
            if (_controller == null || _playerInventory == null)
            {
                Debug.LogError($"{nameof(BuildMenuView)} needs a placement controller and the player backpack.", this);
                return;
            }

            _controller.CancelPlacement();
            _feedback = "Pick a structure, then choose where it goes.";
            _scroll = Vector2.zero;
            LockControls();
            IsOpen = true;
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

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        /// <summary>Closes the panel and attaches the structure to the cursor.</summary>
        public bool Select(BuildableDefinition buildable)
        {
            if (_controller == null)
            {
                return false;
            }

            Close();
            return _controller.BeginPlacement(buildable);
        }

        private void Update()
        {
            if (_controller == null || Time.timeScale <= 0f)
            {
                return;
            }

            if (Input.GetKeyDown(ToggleKey))
            {
                if (_controller.IsPlacing)
                {
                    _controller.CancelPlacement();
                }
                else
                {
                    Toggle();
                }
            }
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            ReleaseControls();
        }

        private void OnGUI()
        {
            if (_controller == null)
            {
                return;
            }

            EnsureStyles();
            if (!IsOpen)
            {
                DrawPlacementHint();
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .66f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.04f, .045f, .05f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("BUILD", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(76f), GUILayout.Height(26f)))
            {
                _pendingClose = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            DrawCatalog(panel.height - 130f);
            GUILayout.Space(6f);
            GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _feedback : GUI.tooltip, _detailStyle);
            GUILayout.EndArea();
            ApplyPendingAction();
        }

        /// <summary>The bar shown while a structure follows the cursor.</summary>
        private void DrawPlacementHint()
        {
            if (!_controller.IsPlacing)
            {
                return;
            }

            BuildResult preview = _controller.Preview;
            var bar = new Rect(0f, Screen.height - HintHeight, Screen.width, HintHeight);
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .62f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = previousColor;

            string message = preview.Succeeded
                ? $"{_controller.Selected.DisplayName}  ·  left click to start building  ·  right click or B to cancel"
                : $"{preview.Message}  ·  right click or B to cancel";
            _hintStyle.normal.textColor = preview.Succeeded
                ? new Color(.72f, .95f, .74f)
                : new Color(.96f, .58f, .52f);
            GUI.Label(bar, message, _hintStyle);
        }

        private void ApplyPendingAction()
        {
            BuildableDefinition selection = _pendingSelection;
            bool close = _pendingClose;
            _pendingSelection = null;
            _pendingClose = false;

            if (selection != null)
            {
                Select(selection);
                return;
            }

            if (close)
            {
                Close();
            }
        }

        private void DrawCatalog(float height)
        {
            SortCatalog();
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(Mathf.Max(180f, height)));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"READY TO BUILD ({_affordable.Count})", _sectionStyle);
            if (_affordable.Count == 0)
            {
                GUILayout.Label("Your backpack holds no materials for anything on this list.", _mutedStyle);
            }

            for (int i = 0; i < _affordable.Count; i++)
            {
                DrawBuildableRow(_affordable[i], true);
            }

            if (_unaffordable.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("MISSING MATERIALS", _sectionStyle);
                for (int i = 0; i < _unaffordable.Count; i++)
                {
                    DrawBuildableRow(_unaffordable[i], false);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>Splits the catalog into what the backpack can pay for right now and what it cannot.</summary>
        private void SortCatalog()
        {
            _affordable.Clear();
            _unaffordable.Clear();
            BuildingCatalog catalog = _controller.Catalog;
            BuildingService service = _controller.Service;
            if (catalog == null || service == null)
            {
                return;
            }

            IReadOnlyList<BuildableDefinition> buildables = catalog.Buildables;
            for (int i = 0; i < buildables.Count; i++)
            {
                BuildableDefinition buildable = buildables[i];
                if (buildable == null || !buildable.IsValid(out string _))
                {
                    continue;
                }

                if (service.CanAfford(buildable))
                {
                    _affordable.Add(buildable);
                }
                else
                {
                    _unaffordable.Add(buildable);
                }
            }
        }

        private void DrawBuildableRow(BuildableDefinition buildable, bool affordable)
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(RowHeight));
            Color previousColor = GUI.color;
            GUI.color = affordable ? Color.white : new Color(1f, 1f, 1f, .4f);
            SpriteIcon.Draw(ReserveIcon(IconSize), buildable.MenuIcon);
            GUI.color = previousColor;

            GUILayout.Space(8f);
            GUILayout.BeginVertical();
            GUILayout.Label(buildable.DisplayName, affordable ? _nameStyle : _mutedStyle);
            DrawCost(buildable);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(120f));
            GUILayout.Label($"{buildable.Footprint.x}×{buildable.Footprint.y} cells  ·  {buildable.BuildSeconds:0}s",
                _detailStyle);
            GUI.enabled = affordable;
            if (GUILayout.Button("PLACE", GUILayout.Height(30f)))
            {
                _pendingSelection = buildable;
            }

            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawCost(BuildableDefinition buildable)
        {
            InventoryModel inventory = _playerInventory.Inventory;
            GUILayout.BeginHorizontal(GUILayout.Height(CostIconSize + 4f));
            IReadOnlyList<CraftingItemAmount> cost = buildable.Cost;
            for (int i = 0; i < cost.Count; i++)
            {
                CraftingItemAmount material = cost[i];
                int owned = inventory.GetQuantity(material.Item.ItemId);

                Rect icon = ReserveIcon(CostIconSize);
                // The row shows art only, so an invisible label carries the material name as a tooltip.
                GUI.Label(icon, new GUIContent(string.Empty, material.Item.DisplayName));
                Color previousColor = GUI.color;
                GUI.color = owned >= material.Quantity ? Color.white : new Color(1f, 1f, 1f, .45f);
                SpriteIcon.Draw(icon, material.Item.Icon);
                GUI.color = previousColor;

                GUILayout.Space(4f);
                GUILayout.Label($"{owned}/{material.Quantity}",
                    owned >= material.Quantity ? _costStyle : _costShortStyle,
                    GUILayout.Height(CostIconSize));
                GUILayout.Space(12f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static Rect ReserveIcon(float size)
        {
            return GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        }

        private void LockControls()
        {
            _inventoryView = GetComponent<InventoryView>();
            _restoreMotor = _playerMotor != null && _playerMotor.enabled;
            _restoreInteractor = _playerInteractor != null && _playerInteractor.enabled;
            _restoreInventoryView = _inventoryView != null && _inventoryView.enabled;
            if (_playerMotor != null) _playerMotor.enabled = false;
            if (_playerInteractor != null) _playerInteractor.enabled = false;
            if (_inventoryView != null) _inventoryView.enabled = false;
        }

        private void ReleaseControls()
        {
            if (_playerMotor != null) _playerMotor.enabled = _restoreMotor;
            if (_playerInteractor != null) _playerInteractor.enabled = _restoreInteractor;
            if (_inventoryView != null) _inventoryView.enabled = _restoreInventoryView;
            _inventoryView = null;
            _restoreMotor = false;
            _restoreInteractor = false;
            _restoreInventoryView = false;
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(.78f, .92f, 1f);
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _sectionStyle.normal.textColor = new Color(.55f, .82f, .95f);
            _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _nameStyle.normal.textColor = Color.white;
            _detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _detailStyle.normal.textColor = new Color(.78f, .78f, .74f);
            _mutedStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _mutedStyle.normal.textColor = new Color(.6f, .6f, .58f);
            _costStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _costStyle.normal.textColor = new Color(.85f, .89f, .82f);
            _costShortStyle = new GUIStyle(_costStyle);
            _costShortStyle.normal.textColor = new Color(.94f, .45f, .38f);
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
