using PlanetSurvival.Core.Time;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.UI.Farming
{
    /// <summary>
    /// The panel the hydroponics rack opens: one row per tray, the cost of a planting, and the reserve
    /// both the crops and the explorer drink from. It shows the water price next to the seed price so the
    /// competition between drinking and growing is visible at the moment of the decision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HydroponicsView : InteractionPanelView
    {
        private const float PanelWidth = 660f;
        private const float PanelHeight = 540f;
        private const float RowHeight = 54f;

        private static readonly Color RipeFill = new(.45f, .85f, .5f);
        private static readonly Color GrowingFill = new(.5f, .74f, .38f);
        private static readonly Color ReserveFill = new(.23f, .55f, .78f);

        private HydroponicsRack _rack;
        private CropDefinition _crop;
        private LiquidContainer _waterSupply;
        private GameClock _clock;
        private PlayerInventory _playerInventory;
        private string _feedback = string.Empty;
        private Vector2 _trayScroll;
        private PendingAction _pending;
        private HydroponicsSlot _pendingSlot;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Growth;

        /// <summary>
        /// A press changes how many controls the panel draws, which IMGUI cannot tolerate in the middle
        /// of a pass, so presses are recorded here and applied once the pass is complete.
        /// </summary>
        private enum PendingAction
        {
            None,
            Plant,
            Harvest,
            Close
        }

        public void Open(HydroponicsRack rack, CropDefinition crop, LiquidContainer waterSupply, GameClock clock,
            PlayerInventory playerInventory, PlanarPlayerMotor playerMotor = null,
            PlayerInteractor playerInteractor = null)
        {
            if (rack == null || crop == null || waterSupply == null || clock == null || playerInventory == null)
            {
                Debug.LogError(
                    $"{nameof(HydroponicsView)} needs the rack, its crop, the reserve, the clock and the backpack.",
                    this);
                return;
            }

            Close();
            _rack = rack;
            _crop = crop;
            _waterSupply = waterSupply;
            _clock = clock;
            _playerInventory = playerInventory;
            _pending = PendingAction.None;
            _pendingSlot = null;
            _trayScroll = Vector2.zero;
            _feedback = "Pick a tray: PLANT sows it, HARVEST empties it into the backpack.";
            BeginSession(playerMotor, playerInteractor);
        }

        /// <summary>Sows one tray. Exposed so tests and other input paths do not go through IMGUI.</summary>
        public FarmingResult Plant(HydroponicsSlot slot)
        {
            if (slot == null || _rack == null || _playerInventory == null)
            {
                return FarmingResult.Fail(FarmingFailure.InvalidCrop, "The hydroponics panel is closed.");
            }

            FarmingResult result = slot.Plant(_crop, _playerInventory.Inventory, _waterSupply, _clock.ElapsedDays);
            if (result.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
                _feedback = $"Tray {slot.Index + 1} planted · ripe in {_crop.GrowthGameHours:0} game hours.";
            }
            else
            {
                _feedback = result.Message;
            }

            return result;
        }

        /// <summary>Moves one ripe tray into the backpack.</summary>
        public FarmingResult Harvest(HydroponicsSlot slot)
        {
            if (slot == null || _rack == null || _playerInventory == null)
            {
                return FarmingResult.Fail(FarmingFailure.SlotEmpty, "The hydroponics panel is closed.");
            }

            string cropName = slot.IsPlanted ? slot.Crop.DisplayName : "the crop";
            int quantity = slot.IsPlanted ? slot.Crop.HarvestQuantity : 0;
            FarmingResult result = slot.Harvest(_playerInventory.Inventory, _clock.ElapsedDays);
            if (result.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
                _feedback = $"Harvested {quantity} × {cropName} from tray {slot.Index + 1}.";
            }
            else
            {
                _feedback = result.Message;
            }

            return result;
        }

        protected override void OnClosed()
        {
            _rack = null;
            _crop = null;
            _waterSupply = null;
            _clock = null;
            _playerInventory = null;
            _pendingSlot = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _rack == null)
            {
                return;
            }

            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            if (InteractionPanel.DrawHeader(panel, null, "HYDROPONICS RACK", StateText(), Styles))
            {
                _pending = PendingAction.Close;
            }

            GUILayout.BeginArea(InteractionPanel.ContentArea(panel));
            GUILayout.Label(
                $"{_crop.DisplayName.ToUpperInvariant()} · {_crop.SeedQuantity} seed + " +
                $"{_crop.WaterMilliliters / 1000f:0.0} L → {_crop.HarvestQuantity} in " +
                $"{_crop.GrowthGameHours:0} game hours", Styles.Value);
            GUILayout.Label($"Seeds in backpack: {CarriedSeeds()}", Styles.Detail);
            GUILayout.Space(10f);

            GUILayout.Label("TRAYS", Styles.Section);
            _trayScroll = GUILayout.BeginScrollView(_trayScroll);
            for (int i = 0; i < _rack.Slots.Count; i++)
            {
                DrawSlotRow(_rack.Slots[i]);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label("POD RESERVE", Styles.Section);
            InteractionPanel.LayoutMeter("WATER", _waterSupply.CurrentMilliliters / 1000f,
                _waterSupply.CapacityMilliliters / 1000f, "L", ReserveFill, Styles);
            GUILayout.EndArea();

            InteractionPanel.DrawFooter(panel, _feedback,
                "Every planting drains the same reserve the explorer drinks from.", Styles);
            ApplyPendingAction();
        }

        private void DrawSlotRow(HydroponicsSlot slot)
        {
            double now = _clock.ElapsedDays;
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.BeginVertical();
            GUILayout.Label($"TRAY {slot.Index + 1}", Styles.Value);
            if (!slot.IsPlanted)
            {
                GUILayout.Label("Empty", Styles.Detail);
            }
            else if (slot.IsRipe(now))
            {
                GUILayout.Label($"{slot.Crop.DisplayName} · ripe", Styles.Detail);
                InteractionPanel.LayoutProgress(1f, RipeFill);
            }
            else
            {
                GUILayout.Label($"{slot.Crop.DisplayName} · {slot.RemainingGameHours(now):0.0} h left", Styles.Detail);
                InteractionPanel.LayoutProgress(slot.Progress(now), GrowingFill);
            }
            GUILayout.EndVertical();

            GUILayout.Space(12f);
            if (!slot.IsPlanted)
            {
                GUI.enabled = CarriedSeeds() >= _crop.SeedQuantity
                              && _waterSupply.CurrentMilliliters >= _crop.WaterMilliliters;
                if (GUILayout.Button("PLANT", GUILayout.Width(110f), GUILayout.Height(34f)))
                {
                    _pending = PendingAction.Plant;
                    _pendingSlot = slot;
                }
                GUI.enabled = true;
            }
            else
            {
                GUI.enabled = slot.IsRipe(now);
                if (GUILayout.Button("HARVEST", GUILayout.Width(110f), GUILayout.Height(34f)))
                {
                    _pending = PendingAction.Harvest;
                    _pendingSlot = slot;
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
        }

        private void ApplyPendingAction()
        {
            PendingAction action = _pending;
            HydroponicsSlot slot = _pendingSlot;
            _pending = PendingAction.None;
            _pendingSlot = null;
            switch (action)
            {
                case PendingAction.Plant:
                    Plant(slot);
                    break;
                case PendingAction.Harvest:
                    Harvest(slot);
                    break;
                case PendingAction.Close:
                    Close();
                    break;
            }
        }

        private int CarriedSeeds()
        {
            return _playerInventory == null || _crop == null || _crop.SeedItem == null
                ? 0
                : _playerInventory.Inventory.GetQuantity(_crop.SeedItem.ItemId);
        }

        private string StateText()
        {
            int ripe = _rack.RipeCount(_clock.ElapsedDays);
            if (ripe > 0)
            {
                return $"{ripe} tray(s) ready to harvest.";
            }

            return _rack.FirstEmptySlot() != null
                ? "Select a tray to plant."
                : "Every tray is growing.";
        }
    }
}
