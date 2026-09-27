using PlanetSurvival.Core.Time;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Water.Runtime;
using UnityEngine;

namespace PlanetSurvival.UI.Farming
{
    /// <summary>Operations panel for manual planter inputs, crop choice and oxygen collection.</summary>
    [DisallowMultipleComponent]
    public sealed class PlanterBoxView : InteractionPanelView
    {
        private const int WaterTransferMilliliters = 500;
        private const float PanelWidth = 620f;
        private const float PanelHeight = 640f;

        private static readonly Color WaterFill = new(.2f, .55f, .9f);
        private static readonly Color CarbonDioxideFill = new(.65f, .65f, .65f);
        private static readonly Color OxygenFill = new(.35f, .85f, .65f);

        private PlanterBox _planter;
        private PlayerInventory _inventory;
        private PlayerWaterBottle _waterBottle;
        private PlayerSpaceSuit _spaceSuit;
        private GameClock _clock;
        private Sprite _panelIcon;
        private string _feedback = string.Empty;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Growth;

        public void Open(PlanterBox planter, PlayerInventory inventory, GameClock clock,
            PlayerWaterBottle waterBottle, PlayerSpaceSuit spaceSuit, PlanarPlayerMotor motor = null,
            PlayerInteractor interactor = null, Sprite panelIcon = null)
        {
            if (planter == null || inventory == null)
            {
                Debug.LogError($"{nameof(PlanterBoxView)} needs a planter and player inventory.", this);
                return;
            }

            Close();
            _planter = planter;
            _inventory = inventory;
            _clock = clock;
            _waterBottle = waterBottle;
            _spaceSuit = spaceSuit;
            _panelIcon = panelIcon;
            _feedback = "Water and CO₂ must both reach their minimum marks before growth starts.";
            BeginSession(motor, interactor);
        }

        public FarmingResult Plant(CropDefinition crop)
        {
            if (_planter == null || _inventory == null || _clock == null)
                return FarmingResult.Fail(FarmingFailure.InvalidCrop, "The planter panel is closed.");
            FarmingResult result = _planter.Plant(crop, _inventory.Inventory, _clock.ElapsedDays);
            _feedback = result.Succeeded ? $"Planted {crop.DisplayName}." : result.Message;
            if (result.Succeeded) _inventory.RefreshQuickBarAssignments();
            return result;
        }

        public FarmingResult Harvest()
        {
            if (_planter == null || _inventory == null)
                return FarmingResult.Fail(FarmingFailure.SlotEmpty, "The planter panel is closed.");
            CropDefinition crop = _planter.Crop;
            FarmingResult result = _planter.Harvest(_inventory.Inventory);
            _feedback = result.Succeeded
                ? $"Harvested {crop.HarvestQuantity} × {crop.HarvestItem.DisplayName}."
                : result.Message;
            if (result.Succeeded) _inventory.RefreshQuickBarAssignments();
            return result;
        }

        public int AddWater(int maximumMilliliters)
        {
            int added = _waterBottle == null ? 0 :
                _planter.TransferWaterFrom(_waterBottle.Container, maximumMilliliters);
            _feedback = added > 0
                ? $"Added {added} mL water."
                : "No water was added; check the suit tank and planter capacity.";
            return added;
        }

        public int LoadCarbonDioxide(int requestedCanisters)
        {
            int loaded = _planter.LoadCarbonDioxideItems(requestedCanisters, _inventory.Inventory);
            _feedback = loaded > 0
                ? $"Loaded {loaded} CO₂ canister(s)."
                : "No CO₂ was loaded; check the backpack and gas capacity.";
            if (loaded > 0) _inventory.RefreshQuickBarAssignments();
            return loaded;
        }

        public float FillSuitOxygen()
        {
            float filled = _spaceSuit?.Resources == null
                ? 0f
                : _planter.TransferOxygenTo(_spaceSuit.Resources.Oxygen);
            _feedback = filled > 0f
                ? $"Transferred {filled:0.#} L oxygen to the suit."
                : "No oxygen could be transferred.";
            return filled;
        }

        protected override void OnClosed()
        {
            _planter = null;
            _inventory = null;
            _waterBottle = null;
            _spaceSuit = null;
            _clock = null;
            _panelIcon = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _planter == null || _inventory == null)
            {
                return;
            }

            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            bool close = InteractionPanel.DrawHeader(panel, _panelIcon, "PLANTER BOX", StateText(), Styles);

            GUILayout.BeginArea(InteractionPanel.ContentArea(panel));
            GUILayout.Label("CROP", Styles.Section);
            DrawCropControls();
            GUILayout.Space(12f);

            GUILayout.Label("ENVIRONMENT", Styles.Section);
            PlanterBoxDefinition definition = _planter.Definition;
            InteractionPanel.LayoutMeter("WATER", _planter.StoredWaterMilliliters,
                definition.WaterCapacityMilliliters, "mL", WaterFill, Styles, null,
                definition.MinimumWaterMilliliters);
            InteractionPanel.LayoutMeter("CARBON DIOXIDE", _planter.StoredCarbonDioxideLiters,
                definition.CarbonDioxideCapacityLiters, "L", CarbonDioxideFill, Styles, null,
                definition.MinimumCarbonDioxideLiters);
            InteractionPanel.LayoutMeter("OXYGEN OUTPUT", _planter.StoredOxygenLiters,
                definition.OxygenCapacityLiters, "L", OxygenFill, Styles);
            GUILayout.Space(12f);

            int canisters = _inventory.Inventory.GetQuantity(definition.CarbonDioxideCanister.ItemId);
            GUILayout.BeginHorizontal();
            GUI.enabled = _waterBottle?.Container != null && _waterBottle.Container.CurrentMilliliters > 0
                          && _planter.RemainingWaterCapacity > 0;
            if (GUILayout.Button("ADD 0.5 L WATER", GUILayout.Height(34f))) AddWater(WaterTransferMilliliters);
            GUI.enabled = canisters > 0 &&
                          _planter.RemainingCarbonDioxideCapacity >= definition.CarbonDioxideLitersPerCanister;
            if (GUILayout.Button($"LOAD CO₂ ({canisters})", GUILayout.Height(34f))) LoadCarbonDioxide(1);
            GUI.enabled = _planter.StoredOxygenLiters > 0f;
            if (GUILayout.Button("FILL SUIT O₂", GUILayout.Height(34f))) FillSuitOxygen();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            InteractionPanel.DrawFooter(panel, _feedback,
                "Water and CO₂ inputs and the oxygen output also accept pipe-network connections.", Styles);
            if (close)
            {
                Close();
            }
        }

        private void DrawCropControls()
        {
            if (!_planter.IsPlanted)
            {
                for (int i = 0; i < _planter.Definition.SupportedCrops.Count; i++)
                {
                    CropDefinition crop = _planter.Definition.SupportedCrops[i];
                    int seeds = _inventory.Inventory.GetQuantity(crop.SeedItem.ItemId);
                    GUI.enabled = seeds >= crop.SeedQuantity;
                    if (GUILayout.Button($"PLANT {crop.DisplayName.ToUpperInvariant()} ({seeds} seeds)",
                            GUILayout.Height(32f)))
                    {
                        Plant(crop);
                    }
                    GUI.enabled = true;
                }
                return;
            }

            if (_planter.IsDead)
            {
                if (GUILayout.Button("CLEAR DEAD CROP", GUILayout.Height(32f)))
                {
                    _planter.ClearDeadCrop();
                    _feedback = "Dead crop cleared; no seed or produce was recovered.";
                }
                return;
            }

            GUILayout.Label($"{_planter.Crop.DisplayName} · {_planter.GrowthProgress:P0} grown", Styles.Value);
            InteractionPanel.LayoutProgress(_planter.GrowthProgress,
                _planter.IsMature ? new Color(.45f, .85f, .5f) : new Color(.5f, .74f, .38f));
            GUILayout.Label(HarvestEstimateText(), Styles.Detail);
            GUILayout.Label($"Harvest yield: {_planter.Crop.HarvestQuantity} × {_planter.Crop.HarvestItem.DisplayName}",
                Styles.Detail);
            GUI.enabled = _planter.IsMature;
            if (GUILayout.Button(_planter.IsMature ? "HARVEST" : "NOT READY TO HARVEST", GUILayout.Height(32f)))
            {
                Harvest();
            }
            GUI.enabled = true;
        }

        private string HarvestEstimateText()
        {
            if (_planter.IsMature)
            {
                return "Expected harvest: ready now.";
            }

            float remainingHours = _planter.RemainingGrowthGameHours;
            bool environmentSupportsGrowth =
                _planter.StoredWaterMilliliters >= _planter.Definition.MinimumWaterMilliliters &&
                _planter.StoredCarbonDioxideLiters >= _planter.Definition.MinimumCarbonDioxideLiters;
            if (!environmentSupportsGrowth)
            {
                return $"Expected harvest: unavailable while growth is paused ({remainingHours:0.0} game h remain).";
            }

            if (_clock == null)
            {
                return $"Expected harvest: in {remainingHours:0.0} game hours.";
            }

            double completionDays = _clock.ElapsedDays + remainingHours / 24d;
            int day = Mathf.FloorToInt((float)completionDays) + 1;
            int totalMinutes = Mathf.FloorToInt((float)(completionDays * 24d * 60d));
            int hour = (totalMinutes / 60) % 24;
            int minute = totalMinutes % 60;
            return $"Expected harvest: D{day} {hour:00}:{minute:00} · {remainingHours:0.0} game h remaining.";
        }

        private string StateText() => _planter.State switch
        {
            PlanterBoxState.Empty => "Empty: choose a crop to plant.",
            PlanterBoxState.NeedsWater =>
                $"Growth paused: add water within {_planter.RemainingEnvironmentToleranceGameHours:0.0} game hours.",
            PlanterBoxState.NeedsCarbonDioxide =>
                $"Growth paused: add CO₂ within {_planter.RemainingEnvironmentToleranceGameHours:0.0} game hours.",
            PlanterBoxState.OxygenStorageFull => "Oxygen storage is full; the crop continues growing.",
            PlanterBoxState.Mature => "Crop mature: ready to harvest.",
            PlanterBoxState.Dead => "Crop died after its environment remained unsuitable.",
            _ => $"Crop growing · producing {_planter.Definition.OxygenLitersPerSecond:0.##} L oxygen/s."
        };
    }
}
