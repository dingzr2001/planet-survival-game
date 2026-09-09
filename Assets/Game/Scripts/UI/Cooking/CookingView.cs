using System.Collections.Generic;
using System.Text;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Cooking.Domain;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Crafting.Domain;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.Cooking
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// The panel a cooking station opens. The menu lists every dish the station can prepare from what
    /// the backpack currently holds; dishes whose ingredients are missing stay visible below, greyed
    /// out, so the player learns what to gather next.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CookingView : MonoBehaviour
    {
        private const float PanelWidth = 1020f;
        private const float PanelHeight = 600f;
        private const float MenuWidth = 640f;
        private const float RowHeight = 78f;
        private const float IconSize = 56f;
        private const float IngredientIconSize = 26f;
        private const float BatchColumnWidth = 172f;

        private readonly List<CraftingRecipe> _available = new();
        private readonly List<CraftingRecipe> _unavailable = new();
        private readonly Dictionary<string, int> _batchSizes = new();

        private PendingAction _pending;
        private CraftingRecipe _pendingRecipe;
        private int _pendingBatch = 1;

        private CookingStationDefinition _definition;
        private CookingProcess _process;
        private PlayerInventory _playerInventory;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private InventoryView _inventoryView;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;
        private string _feedback = string.Empty;
        private Vector2 _menuScroll;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _dishStyle;
        private GUIStyle _detailStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _ingredientStyle;
        private GUIStyle _ingredientShortStyle;
        private GUIStyle _batchStyle;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// A button press changes how many controls the panel draws, which IMGUI cannot tolerate in the
        /// middle of a pass, so presses are recorded here and applied once the pass is complete.
        /// </summary>
        private enum PendingAction
        {
            None,
            Cook,
            Collect,
            Cancel,
            Close
        }

        public void Open(CookingStationDefinition definition, CookingProcess process,
            PlayerInventory playerInventory, PlanarPlayerMotor playerMotor = null,
            PlayerInteractor playerInteractor = null)
        {
            if (definition == null || process == null || playerInventory == null)
            {
                Debug.LogError($"{nameof(CookingView)} needs a station, its process, and the player backpack.", this);
                return;
            }

            Close();
            _definition = definition;
            _process = process;
            _playerInventory = playerInventory;
            _feedback = DefaultFeedback();
            _menuScroll = Vector2.zero;
            _pending = PendingAction.None;
            _pendingRecipe = null;
            _pendingBatch = 1;
            _batchSizes.Clear();
            CaptureAndLockControls(playerMotor, playerInteractor);
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            ReleaseControls();
            _definition = null;
            _process = null;
            _playerInventory = null;
        }

        /// <summary>Starts one batch. Exposed so tests and other input paths do not go through IMGUI.</summary>
        public CraftingResult Cook(CraftingRecipe recipe, int batchCount = 1)
        {
            if (_process == null || _playerInventory == null || _definition == null)
            {
                return CraftingResult.Fail(CraftingFailure.InvalidRecipe, "The cooking panel is closed.");
            }

            CraftingResult result = _process.Start(
                recipe, _playerInventory.Inventory, _definition.Conditions, batchCount);
            _feedback = result.Succeeded
                ? $"Cooking {batchCount} × {recipe.DisplayName}…"
                : result.Message;
            if (result.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
            }

            return result;
        }

        public CraftingResult Collect()
        {
            if (_process == null || _playerInventory == null)
            {
                return CraftingResult.Fail(CraftingFailure.NothingReady, "The cooking panel is closed.");
            }

            string dish = _process.ActiveRecipe != null ? _process.ActiveRecipe.DisplayName : "the dish";
            CraftingResult result = _process.Collect(_playerInventory.Inventory);
            if (result.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
                _feedback = $"{dish} moved to your backpack.";
            }
            else
            {
                _feedback = result.Message;
            }

            return result;
        }

        public CraftingResult Cancel()
        {
            if (_process == null || _playerInventory == null)
            {
                return CraftingResult.Fail(CraftingFailure.NothingReady, "The cooking panel is closed.");
            }

            CraftingResult result = _process.Cancel(_playerInventory.Inventory);
            if (result.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
                _feedback = "Cooking cancelled; the ingredients are back in your backpack.";
            }
            else
            {
                _feedback = result.Message;
            }

            return result;
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            ReleaseControls();
        }

        private void OnGUI()
        {
            if (!IsOpen || _definition == null || _process == null || _playerInventory == null)
            {
                return;
            }

            EnsureStyles();
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .68f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.045f, .04f, .035f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{_definition.DisplayName.ToUpperInvariant()}  ·  COOKING", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(76f), GUILayout.Height(26f)))
            {
                _pending = PendingAction.Close;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            float contentHeight = Mathf.Max(180f, panel.height - 130f);
            GUILayout.BeginHorizontal();
            DrawMenu(Mathf.Min(MenuWidth, panel.width - 340f), contentHeight);
            GUILayout.Space(12f);
            DrawStationPanel(contentHeight);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            // Hovering an ingredient icon names it here, since the rows themselves show art only.
            GUILayout.Label(string.IsNullOrEmpty(GUI.tooltip) ? _feedback : GUI.tooltip, _detailStyle);
            GUILayout.EndArea();
            ApplyPendingAction();
        }

        private void ApplyPendingAction()
        {
            PendingAction pending = _pending;
            CraftingRecipe recipe = _pendingRecipe;
            int batch = _pendingBatch;
            _pending = PendingAction.None;
            _pendingRecipe = null;
            _pendingBatch = 1;

            switch (pending)
            {
                case PendingAction.Cook:
                    Cook(recipe, batch);
                    break;
                case PendingAction.Collect:
                    Collect();
                    break;
                case PendingAction.Cancel:
                    Cancel();
                    break;
                case PendingAction.Close:
                    Close();
                    break;
            }
        }

        private void DrawMenu(float width, float height)
        {
            SortRecipes();
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width), GUILayout.Height(height));
            _menuScroll = GUILayout.BeginScrollView(_menuScroll);

            GUILayout.Label($"READY TO COOK ({_available.Count})", _sectionStyle);
            if (_available.Count == 0)
            {
                GUILayout.Label("Your backpack holds no ingredients this station can use.", _mutedStyle);
            }

            for (int i = 0; i < _available.Count; i++)
            {
                DrawRecipeRow(_available[i], true);
            }

            if (_unavailable.Count > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("MISSING INGREDIENTS", _sectionStyle);
                for (int i = 0; i < _unavailable.Count; i++)
                {
                    DrawRecipeRow(_unavailable[i], false);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>Splits the station menu into what the backpack can make right now and what it cannot.</summary>
        private void SortRecipes()
        {
            _available.Clear();
            _unavailable.Clear();
            InventoryModel inventory = _playerInventory.Inventory;
            IReadOnlyList<CraftingRecipe> recipes = _definition.Recipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                CraftingRecipe recipe = recipes[i];
                if (recipe == null || !_definition.Supports(recipe))
                {
                    continue;
                }

                if (_process.Validate(recipe, inventory, _definition.Conditions).Succeeded)
                {
                    _available.Add(recipe);
                }
                else
                {
                    _unavailable.Add(recipe);
                }
            }
        }

        private void DrawRecipeRow(CraftingRecipe recipe, bool available)
        {
            int maxBatches = CookingProcess.MaxBatches(recipe, _playerInventory.Inventory);
            int batch = ClampBatch(recipe, maxBatches);

            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(RowHeight));
            Color previousColor = GUI.color;
            GUI.color = available ? Color.white : new Color(1f, 1f, 1f, .4f);
            SpriteIcon.Draw(ReserveIcon(IconSize), PrimaryOutputIcon(recipe));
            GUI.color = previousColor;

            GUILayout.Space(8f);
            GUILayout.BeginVertical();
            GUILayout.Label(OutputSummary(recipe, batch), available ? _dishStyle : _mutedStyle);
            DrawIngredients(recipe, batch);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            DrawBatchControls(recipe, batch, maxBatches, available);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Ingredients read as icons rather than names: the artwork identifies the item faster than text,
        /// and the owned/required count next to it stays the only number the player has to read.
        /// </summary>
        private void DrawIngredients(CraftingRecipe recipe, int batch)
        {
            InventoryModel inventory = _playerInventory.Inventory;
            GUILayout.BeginHorizontal(GUILayout.Height(IngredientIconSize + 4f));
            IReadOnlyList<CraftingItemAmount> inputs = recipe.Inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                CraftingItemAmount input = inputs[i];
                int owned = inventory.GetQuantity(input.Item.ItemId);
                int required = input.Quantity * batch;

                Rect icon = ReserveIcon(IngredientIconSize);
                // An invisible label carries the item name as a tooltip, since the row no longer spells it out.
                GUI.Label(icon, new GUIContent(string.Empty, input.Item.DisplayName));
                Color previousColor = GUI.color;
                GUI.color = owned >= required ? Color.white : new Color(1f, 1f, 1f, .45f);
                SpriteIcon.Draw(icon, input.Item.Icon);
                GUI.color = previousColor;

                GUILayout.Space(4f);
                GUILayout.Label($"{owned}/{required}",
                    owned >= required ? _ingredientStyle : _ingredientShortStyle,
                    GUILayout.Height(IngredientIconSize));
                GUILayout.Space(12f);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{recipe.DurationSeconds * batch:0}s", _detailStyle,
                GUILayout.Height(IngredientIconSize));
            GUILayout.EndHorizontal();
        }

        private void DrawBatchControls(CraftingRecipe recipe, int batch, int maxBatches, bool available)
        {
            bool idle = _process.State == CookingState.Idle;
            GUILayout.BeginVertical(GUILayout.Width(BatchColumnWidth));
            GUILayout.BeginHorizontal();
            GUI.enabled = available && idle && batch > 1;
            if (GUILayout.Button("−", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                SetBatch(recipe, batch - 1);
            }

            GUI.enabled = true;
            GUILayout.Label($"×{batch}", _batchStyle, GUILayout.Width(40f), GUILayout.Height(24f));
            GUI.enabled = available && idle && batch < maxBatches;
            if (GUILayout.Button("+", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                SetBatch(recipe, batch + 1);
            }

            if (GUILayout.Button("MAX", GUILayout.Width(52f), GUILayout.Height(24f)))
            {
                SetBatch(recipe, maxBatches);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUI.enabled = available && idle;
            if (GUILayout.Button($"COOK ×{batch}", GUILayout.Height(30f)))
            {
                _pending = PendingAction.Cook;
                _pendingRecipe = recipe;
                _pendingBatch = batch;
            }

            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        /// <summary>Keeps the chosen batch size inside what the backpack can still supply.</summary>
        private int ClampBatch(CraftingRecipe recipe, int maxBatches)
        {
            if (!_batchSizes.TryGetValue(recipe.RecipeId, out int batch))
            {
                batch = 1;
            }

            return Mathf.Clamp(batch, 1, Mathf.Max(1, maxBatches));
        }

        private void SetBatch(CraftingRecipe recipe, int batch)
        {
            _batchSizes[recipe.RecipeId] = Mathf.Max(1, batch);
        }

        private static Rect ReserveIcon(float size)
        {
            return GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        }

        private void DrawStationPanel(float height)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(height));
            GUILayout.Label("STATION", _sectionStyle);
            GUILayout.Space(6f);

            CraftingRecipe recipe = _process.ActiveRecipe;
            if (recipe == null)
            {
                GUILayout.Label("Idle. Pick a dish from the menu to start cooking.", _mutedStyle);
                GUILayout.EndVertical();
                return;
            }

            Rect icon = GUILayoutUtility.GetRect(96f, 96f, GUILayout.Height(96f), GUILayout.ExpandWidth(true));
            SpriteIcon.Draw(icon, PrimaryOutputIcon(recipe));
            GUILayout.Space(8f);
            GUILayout.Label(OutputSummary(recipe, _process.BatchCount), _dishStyle);

            if (_process.State == CookingState.Cooking)
            {
                DrawProgressMeter(_process.Progress);
                GUILayout.Label($"{_process.RemainingSeconds:0.0}s remaining", _detailStyle);
                GUILayout.Space(8f);
                if (GUILayout.Button("CANCEL", GUILayout.Height(32f)))
                {
                    _pending = PendingAction.Cancel;
                }
            }
            else
            {
                DrawProgressMeter(1f);
                GUILayout.Label("Ready to serve.", _detailStyle);
                GUILayout.Space(8f);
                if (GUILayout.Button("COLLECT", GUILayout.Height(36f)))
                {
                    _pending = PendingAction.Collect;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private static void DrawProgressMeter(float normalized)
        {
            Rect meter = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(meter, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(.1f, .09f, .08f), Vector4.zero, new Vector4(4f, 4f, 4f, 4f));
            float filled = Mathf.Clamp01(normalized);
            if (filled <= 0f)
            {
                return;
            }

            var fill = new Rect(meter.x + 2f, meter.y + 2f, (meter.width - 4f) * filled, meter.height - 4f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(.95f, .58f, .22f), Vector4.zero, new Vector4(3f, 3f, 3f, 3f));
        }

        private static Sprite PrimaryOutputIcon(CraftingRecipe recipe)
        {
            IReadOnlyList<CraftingItemAmount> outputs = recipe.Outputs;
            return outputs.Count > 0 && outputs[0].Item != null ? outputs[0].Item.Icon : null;
        }

        private static string OutputSummary(CraftingRecipe recipe, int batch)
        {
            IReadOnlyList<CraftingItemAmount> outputs = recipe.Outputs;
            if (outputs.Count == 0)
            {
                return recipe.DisplayName;
            }

            var summary = new StringBuilder();
            for (int i = 0; i < outputs.Count; i++)
            {
                if (i > 0)
                {
                    summary.Append(" + ");
                }

                summary.Append(outputs[i].Item.DisplayName)
                    .Append(" ×")
                    .Append(outputs[i].Quantity * Mathf.Max(1, batch));
            }

            return summary.ToString();
        }

        private string DefaultFeedback()
        {
            return _process.State switch
            {
                CookingState.Cooking => "The station is busy.",
                CookingState.Ready => "A finished dish is waiting to be collected.",
                _ => "Pick a dish to start cooking."
            };
        }

        private void CaptureAndLockControls(PlanarPlayerMotor playerMotor, PlayerInteractor playerInteractor)
        {
            _playerMotor = playerMotor;
            _playerInteractor = playerInteractor;
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
            _playerMotor = null;
            _playerInteractor = null;
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
            _headerStyle.normal.textColor = new Color(1f, .87f, .72f);
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _sectionStyle.normal.textColor = new Color(.94f, .74f, .45f);
            _dishStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _dishStyle.normal.textColor = Color.white;
            _detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _detailStyle.normal.textColor = new Color(.78f, .78f, .74f);
            _mutedStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            _mutedStyle.normal.textColor = new Color(.6f, .6f, .58f);
            _ingredientStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            _ingredientStyle.normal.textColor = new Color(.85f, .89f, .82f);
            _ingredientShortStyle = new GUIStyle(_ingredientStyle);
            _ingredientShortStyle.normal.textColor = new Color(.94f, .45f, .38f);
            _batchStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _batchStyle.normal.textColor = Color.white;
        }
    }
}
