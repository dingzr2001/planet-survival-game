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
    /// <summary>
    /// Operations panel for one planter: water and CO₂ go in on the left, the crop grows in the middle,
    /// oxygen comes out on the right. Laid out like the other machine panels so an input, a process and an
    /// output always sit in the same place.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlanterBoxView : InteractionPanelView
    {
        private const int WaterTransferMilliliters = 500;
        private const float PanelWidth = 900f;
        private const float PanelHeight = 620f;

        /// <summary>Room the output column keeps clear for its headline.</summary>
        private const float HeadlineInset = 52f;

        /// <summary>Floor on the growth chamber, whatever the crop list costs below it.</summary>
        private const float MinimumChamberHeight = 150f;
        private const string WaterTextureResource = "Water/WaterBottle";
        private const string OxygenTextureResource = "Oxygen/Oxygen";

        private static readonly Color WaterFill = new(.2f, .55f, .9f);
        private static readonly Color CarbonDioxideFill = new(.65f, .65f, .65f);
        private static readonly Color OxygenFill = new(.35f, .85f, .65f);

        private PlanterBox _planter;
        private PlayerInventory _inventory;
        private PlayerWaterBottle _waterBottle;
        private PlayerSpaceSuit _spaceSuit;
        private GameClock _clock;
        private Sprite _panelIcon;
        private Texture2D _waterTexture;
        private Texture2D _oxygenTexture;
        private string _feedback = string.Empty;
        private CropDefinition _pendingCrop;
        private PendingAction _pending;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Growth;

        /// <summary>
        /// Planting or harvesting changes how many controls the panel draws, which IMGUI cannot tolerate in
        /// the middle of a pass, so presses are recorded here and applied once the pass is complete.
        /// </summary>
        private enum PendingAction
        {
            None,
            Plant,
            Harvest,
            ClearDead
        }

        private void Awake()
        {
            _waterTexture = Resources.Load<Texture2D>(WaterTextureResource);
            _oxygenTexture = Resources.Load<Texture2D>(OxygenTextureResource);
        }

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
            _pending = PendingAction.None;
            _pendingCrop = null;
            _feedback = "Water and CO₂ must both pass their minimum marks before the crop grows.";
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
            _pendingCrop = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _planter == null || _inventory == null)
            {
                return;
            }

            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            bool close = InteractionPanel.DrawHeader(panel, _panelIcon, "PLANTER BOX", StateText(), Styles);

            InteractionPanel.MachineColumns columns = InteractionPanel.DrawColumns(panel, Theme);
            float inputSlot = InteractionPanel.SlotSizeForStack(panel, columns.Inputs, 2, true);
            float outputSlot = InteractionPanel.SlotSizeForStack(panel, columns.Outputs, 1, true, HeadlineInset);
            float actionRow = InteractionPanel.SlotCaptionHeight + 12f;

            DrawWaterSlot(InteractionPanel.StackedSlot(columns.Inputs, 0, 2, inputSlot, true), actionRow);
            DrawCarbonDioxideSlot(
                InteractionPanel.StackedSlot(columns.Inputs, 1, 2, inputSlot, true), actionRow);
            DrawGrowthChamber(columns.Process);
            DrawOxygenSlot(
                InteractionPanel.StackedSlot(columns.Outputs, 0, 1, outputSlot, true, HeadlineInset), actionRow);
            InteractionPanel.Headline(columns.Outputs, HeadlineText(), Styles);

            InteractionPanel.DrawFooter(panel, _feedback,
                "Both inputs and the oxygen output also accept pipe connections.", Styles);
            if (close)
            {
                Close();
            }

            ApplyPendingAction();
        }

        private void DrawWaterSlot(Rect slot, float actionRow)
        {
            PlanterBoxDefinition definition = _planter.Definition;
            bool canPour = _waterBottle?.Container != null && _waterBottle.Container.CurrentMilliliters > 0
                           && _planter.RemainingWaterCapacity > 0;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canPour)
            {
                AddWater(WaterTransferMilliliters);
            }

            InteractionPanel.SlotIcon(slot, null, _waterTexture, "H₂O", Styles);
            InteractionPanel.SlotCaption(slot, "Water",
                $"{_planter.StoredWaterMilliliters} / {definition.WaterCapacityMilliliters} mL", Styles);
            InteractionPanel.SlotMeter(slot, _planter.StoredWaterMilliliters,
                definition.WaterCapacityMilliliters, definition.MinimumWaterMilliliters, WaterFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, "ADD 0.5 L", canPour))
            {
                AddWater(WaterTransferMilliliters);
            }
        }

        private void DrawCarbonDioxideSlot(Rect slot, float actionRow)
        {
            PlanterBoxDefinition definition = _planter.Definition;
            int canisters = _inventory.Inventory.GetQuantity(definition.CarbonDioxideCanister.ItemId);
            bool canLoad = canisters > 0 &&
                           _planter.RemainingCarbonDioxideCapacity >= definition.CarbonDioxideLitersPerCanister;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canLoad)
            {
                LoadCarbonDioxide(1);
            }

            InteractionPanel.SlotIcon(slot, definition.CarbonDioxideCanister.Icon, null, "CO₂", Styles);
            InteractionPanel.SlotCaption(slot, "Carbon Dioxide",
                $"{_planter.StoredCarbonDioxideLiters:0.#} / {definition.CarbonDioxideCapacityLiters:0.#} L",
                Styles);
            InteractionPanel.SlotMeter(slot, _planter.StoredCarbonDioxideLiters,
                definition.CarbonDioxideCapacityLiters, definition.MinimumCarbonDioxideLiters,
                CarbonDioxideFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, $"LOAD CANISTER ({canisters})", canLoad))
            {
                LoadCarbonDioxide(1);
            }
        }

        private void DrawOxygenSlot(Rect slot, float actionRow)
        {
            PlanterBoxDefinition definition = _planter.Definition;
            bool canFill = _planter.StoredOxygenLiters > 0f;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canFill)
            {
                FillSuitOxygen();
            }

            InteractionPanel.SlotIcon(slot, null, _oxygenTexture, "O₂", Styles);
            InteractionPanel.SlotCaption(slot, "Oxygen",
                $"{_planter.StoredOxygenLiters:0.#} / {definition.OxygenCapacityLiters:0.#} L", Styles);
            InteractionPanel.SlotMeter(slot, _planter.StoredOxygenLiters, definition.OxygenCapacityLiters,
                0f, OxygenFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, "FILL SUIT", canFill))
            {
                FillSuitOxygen();
            }
        }

        /// <summary>
        /// The middle column: the crop itself at its current growth stage, then the one control that
        /// applies to it. An empty planter offers its crops here rather than in a list somewhere else.
        /// </summary>
        private void DrawGrowthChamber(Rect column)
        {
            float buttonBand = 40f + (_planter.IsPlanted ? 0f : 34f * Mathf.Max(0,
                _planter.Definition.SupportedCrops.Count - 1));
            // A long crop list must not squeeze the chamber out of existence.
            var chamberRect = new Rect(column.x + 22f, column.y + 30f, column.width - 44f,
                Mathf.Max(MinimumChamberHeight, column.height - 60f - buttonBand));
            Rect inner = InteractionPanel.Chamber(chamberRect, "GROWTH CHAMBER", Styles, Theme);

            Sprite crop = _planter.IsPlanted && !_planter.IsDead
                ? _planter.Crop.GrowthStageSprite(_planter.GrowthProgress)
                : _planter.Definition.EmptySprite;
            float size = Mathf.Min(inner.height, inner.width * .7f);
            SpriteIcon.Draw(new Rect(inner.center.x - size * .5f, inner.y, size, size), crop);

            // Soil line, so an empty planter still reads as a planter rather than an empty box.
            float soilY = inner.yMax - 6f;
            InteractionPanel.Fill(new Rect(inner.x + 18f, soilY, inner.width - 36f, 3f), Theme.Frame);

            bool growing = _planter.State == PlanterBoxState.Growing ||
                           _planter.State == PlanterBoxState.OxygenStorageFull;
            InteractionPanel.ChamberStatus(chamberRect, growing, ChamberStatusText(),
                _planter.IsPlanted && !_planter.IsDead ? _planter.GrowthProgress : 0f, Styles, Theme);

            DrawCropControls(new Rect(column.x + 22f, chamberRect.yMax + 12f, column.width - 44f, buttonBand));
        }

        private void DrawCropControls(Rect band)
        {
            if (_planter.IsDead)
            {
                if (GUI.Button(new Rect(band.x, band.y, band.width, 32f), "CLEAR DEAD CROP"))
                {
                    _pending = PendingAction.ClearDead;
                }

                return;
            }

            if (!_planter.IsPlanted)
            {
                for (int i = 0; i < _planter.Definition.SupportedCrops.Count; i++)
                {
                    CropDefinition crop = _planter.Definition.SupportedCrops[i];
                    int seeds = _inventory.Inventory.GetQuantity(crop.SeedItem.ItemId);
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = previousEnabled && seeds >= crop.SeedQuantity;
                    if (GUI.Button(new Rect(band.x, band.y + i * 34f, band.width, 32f),
                            $"PLANT {crop.DisplayName.ToUpperInvariant()}  ({seeds} seeds)"))
                    {
                        _pending = PendingAction.Plant;
                        _pendingCrop = crop;
                    }
                    GUI.enabled = previousEnabled;
                }

                return;
            }

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && _planter.IsMature;
            if (GUI.Button(new Rect(band.x, band.y, band.width, 32f),
                    _planter.IsMature
                        ? $"HARVEST  ({_planter.Crop.HarvestQuantity} × {_planter.Crop.HarvestItem.DisplayName})"
                        : "NOT READY TO HARVEST"))
            {
                _pending = PendingAction.Harvest;
            }
            GUI.enabled = wasEnabled;
        }

        private void ApplyPendingAction()
        {
            PendingAction action = _pending;
            CropDefinition crop = _pendingCrop;
            _pending = PendingAction.None;
            _pendingCrop = null;
            switch (action)
            {
                case PendingAction.Plant:
                    Plant(crop);
                    break;
                case PendingAction.Harvest:
                    Harvest();
                    break;
                case PendingAction.ClearDead:
                    _planter.ClearDeadCrop();
                    _feedback = "Dead crop cleared; no seed or produce was recovered.";
                    break;
            }
        }

        private string HeadlineText()
        {
            if (!_planter.IsPlanted) return "🌱 EMPTY";
            if (_planter.IsDead) return "🌱 DEAD";
            return _planter.IsMature ? "🌱 READY" : $"🌱 GROWTH {_planter.GrowthProgress:P0}";
        }

        private string ChamberStatusText()
        {
            if (!_planter.IsPlanted) return "EMPTY";
            if (_planter.IsDead) return "DEAD";
            if (_planter.IsMature) return "MATURE";
            return _planter.State == PlanterBoxState.Growing ||
                   _planter.State == PlanterBoxState.OxygenStorageFull
                ? $"GROWING  ·  {HarvestEstimate()}"
                : "PAUSED";
        }

        private string HarvestEstimate()
        {
            float remainingHours = _planter.RemainingGrowthGameHours;
            if (_clock == null)
            {
                return $"{remainingHours:0.0} game h left";
            }

            double completionDays = _clock.ElapsedDays + remainingHours / 24d;
            int day = Mathf.FloorToInt((float)completionDays) + 1;
            int totalMinutes = Mathf.FloorToInt((float)(completionDays * 24d * 60d));
            return $"ready D{day} {(totalMinutes / 60) % 24:00}:{totalMinutes % 60:00}";
        }

        private string StateText() => _planter.State switch
        {
            PlanterBoxState.Empty => "Empty: choose a crop to plant.",
            PlanterBoxState.NeedsWater =>
                $"Growth paused: add water within {_planter.RemainingEnvironmentToleranceGameHours:0.0} game hours.",
            PlanterBoxState.NeedsCarbonDioxide =>
                $"Growth paused: add CO₂ within {_planter.RemainingEnvironmentToleranceGameHours:0.0} game hours.",
            PlanterBoxState.OxygenStorageFull => "Oxygen storage is full; the crop keeps growing.",
            PlanterBoxState.Mature => "Crop mature: ready to harvest.",
            PlanterBoxState.Dead => "Crop died after its environment stayed unsuitable.",
            _ => $"Growing · producing {_planter.Definition.OxygenLitersPerSecond:0.##} L oxygen/s."
        };
    }
}
