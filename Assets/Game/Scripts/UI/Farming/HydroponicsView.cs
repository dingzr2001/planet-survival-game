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
    public sealed class HydroponicsView : MonoBehaviour
    {
        private const float PanelWidth = 620f;
        private const float PanelHeight = 440f;
        private const float RowHeight = 54f;

        private HydroponicsRack _rack;
        private CropDefinition _crop;
        private LiquidContainer _waterSupply;
        private GameClock _clock;
        private PlayerInventory _playerInventory;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private string _status = string.Empty;
        private PendingAction _pending;
        private HydroponicsSlot _pendingSlot;
        private GUIStyle _headerStyle;
        private GUIStyle _primaryLabelStyle;
        private GUIStyle _secondaryLabelStyle;

        public bool IsOpen { get; private set; }

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
                Debug.LogError($"{nameof(HydroponicsView)} needs the rack, its crop, the reserve, the clock and the backpack.", this);
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
            _status = DefaultStatus();
            CaptureAndLockPlayerControls(playerMotor, playerInteractor);
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            ReleasePlayerControls();
            _rack = null;
            _crop = null;
            _waterSupply = null;
            _clock = null;
            _playerInventory = null;
            _pendingSlot = null;
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
                _status = $"Tray {slot.Index + 1} planted · ripe in {_crop.GrowthGameHours:0} game hours.";
            }
            else
            {
                _status = result.Message;
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
                _status = $"Harvested {quantity} × {cropName} from tray {slot.Index + 1}.";
            }
            else
            {
                _status = result.Message;
            }

            return result;
        }

        private void OnDestroy() => ReleasePlayerControls();

        private void OnGUI()
        {
            if (!IsOpen || _rack == null)
            {
                return;
            }

            EnsureStyles();

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .62f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.03f, .07f, .05f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, panel.width - 40f, panel.height - 32f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("HYDROPONICS RACK", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(72f), GUILayout.Height(26f)))
            {
                _pending = PendingAction.Close;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            GUILayout.Label(
                $"{_crop.DisplayName.ToUpperInvariant()} · {_crop.SeedQuantity} seed + " +
                $"{_crop.WaterMilliliters / 1000f:0.0} L → {_crop.HarvestQuantity} in {_crop.GrowthGameHours:0} game hours",
                _primaryLabelStyle);
            GUILayout.Label($"Seeds in backpack: {CarriedSeeds()}", _secondaryLabelStyle);
            GUILayout.Space(10f);

            for (int i = 0; i < _rack.Slots.Count; i++)
            {
                DrawSlotRow(_rack.Slots[i]);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("POD RESERVE", _primaryLabelStyle);
            DrawProgress(
                _waterSupply.CapacityMilliliters <= 0
                    ? 0f
                    : (float)_waterSupply.CurrentMilliliters / _waterSupply.CapacityMilliliters,
                new Color(.23f, .55f, .78f));
            GUILayout.Label($"{_waterSupply.CurrentMilliliters / 1000f:0.0} L available", _secondaryLabelStyle);
            GUILayout.Space(6f);
            GUILayout.Label(_status, _secondaryLabelStyle);
            GUILayout.EndArea();

            ApplyPendingAction();
        }

        private void DrawSlotRow(HydroponicsSlot slot)
        {
            double now = _clock.ElapsedDays;
            GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
            GUILayout.BeginVertical();
            GUILayout.Label($"TRAY {slot.Index + 1}", _primaryLabelStyle);
            if (!slot.IsPlanted)
            {
                GUILayout.Label("Empty", _secondaryLabelStyle);
            }
            else if (slot.IsRipe(now))
            {
                GUILayout.Label($"{slot.Crop.DisplayName} · ripe", _secondaryLabelStyle);
                DrawProgress(1f, new Color(.45f, .85f, .5f));
            }
            else
            {
                GUILayout.Label($"{slot.Crop.DisplayName} · {slot.RemainingGameHours(now):0.0} h left",
                    _secondaryLabelStyle);
                DrawProgress(slot.Progress(now), new Color(.5f, .74f, .38f));
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

        private string DefaultStatus()
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

        private static void DrawProgress(float normalized, Color fillColor)
        {
            Rect meter = GUILayoutUtility.GetRect(10f, 14f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(meter, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(.08f, .11f, .13f), Vector4.zero, new Vector4(4f, 4f, 4f, 4f));
            float clamped = Mathf.Clamp01(normalized);
            if (clamped > 0f)
            {
                var fill = new Rect(meter.x + 2f, meter.y + 2f, (meter.width - 4f) * clamped, meter.height - 4f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                    fillColor, Vector4.zero, new Vector4(3f, 3f, 3f, 3f));
            }
        }

        private void CaptureAndLockPlayerControls(PlanarPlayerMotor playerMotor, PlayerInteractor playerInteractor)
        {
            ReleasePlayerControls();
            _playerMotor = playerMotor;
            _playerInteractor = playerInteractor;
            _restoreMotor = _playerMotor != null && _playerMotor.enabled;
            _restoreInteractor = _playerInteractor != null && _playerInteractor.enabled;
            if (_playerMotor != null)
            {
                _playerMotor.enabled = false;
            }

            if (_playerInteractor != null)
            {
                _playerInteractor.enabled = false;
            }
        }

        private void ReleasePlayerControls()
        {
            if (_playerMotor != null)
            {
                _playerMotor.enabled = _restoreMotor;
            }

            if (_playerInteractor != null)
            {
                _playerInteractor.enabled = _restoreInteractor;
            }

            _playerMotor = null;
            _playerInteractor = null;
            _restoreMotor = false;
            _restoreInteractor = false;
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(.78f, 1f, .84f);
            _primaryLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _primaryLabelStyle.normal.textColor = Color.white;
            _secondaryLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _secondaryLabelStyle.normal.textColor = new Color(.74f, .84f, .78f);
        }
    }
}
