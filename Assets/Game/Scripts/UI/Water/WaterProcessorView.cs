using PlanetSurvival.Core.Time;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.UI.Water
{
    /// <summary>
    /// The panel the ice processor opens: how much ice the backpack carries, what the machine is doing,
    /// and how full the pod reserve is. Loading and draining are the only two decisions, so the panel
    /// stays a single column instead of a menu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterProcessorView : MonoBehaviour
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 380f;

        private WaterProcessor _processor;
        private ItemDefinition _iceItem;
        private LiquidContainer _waterSupply;
        private GameClock _clock;
        private PlayerInventory _playerInventory;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private int _requestedChunks = 1;
        private string _status = string.Empty;
        private PendingAction _pending;
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
            Load,
            Drain,
            Close
        }

        public void Open(WaterProcessor processor, ItemDefinition iceItem, LiquidContainer waterSupply,
            GameClock clock, PlayerInventory playerInventory, PlanarPlayerMotor playerMotor = null,
            PlayerInteractor playerInteractor = null)
        {
            if (processor == null || iceItem == null || waterSupply == null || clock == null
                || playerInventory == null)
            {
                Debug.LogError($"{nameof(WaterProcessorView)} needs the machine, its ice, a tank, the clock and the backpack.", this);
                return;
            }

            Close();
            _processor = processor;
            _iceItem = iceItem;
            _waterSupply = waterSupply;
            _clock = clock;
            _playerInventory = playerInventory;
            _requestedChunks = Mathf.Max(1, MaximumLoad());
            _pending = PendingAction.None;
            _status = DefaultStatus();
            CaptureAndLockPlayerControls(playerMotor, playerInteractor);
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            ReleasePlayerControls();
            _processor = null;
            _iceItem = null;
            _waterSupply = null;
            _clock = null;
            _playerInventory = null;
        }

        /// <summary>Starts one batch. Exposed so tests and other input paths do not go through IMGUI.</summary>
        public WaterProcessorResult Load(int chunks)
        {
            if (_processor == null || _playerInventory == null || _clock == null)
            {
                return WaterProcessorResult.InvalidRequest;
            }

            WaterProcessorResult result = _processor.Load(
                _iceItem, chunks, _playerInventory.Inventory, _clock.ElapsedDays);
            if (result == WaterProcessorResult.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
                _status = $"Processing {chunks} chunks · {WaterProcessor.ProcessingGameHours(chunks):0.0} game hours.";
            }
            else
            {
                _status = Describe(result);
            }

            return result;
        }

        /// <summary>Pours whatever has processed into the pod reserve.</summary>
        public WaterProcessorResult Drain()
        {
            if (_processor == null || _waterSupply == null)
            {
                return WaterProcessorResult.InvalidRequest;
            }

            WaterProcessorResult result = _processor.Collect(_waterSupply, out int transferred);
            _status = result == WaterProcessorResult.Succeeded
                ? _processor.PendingMilliliters > 0
                    ? $"Drained {transferred} mL. The reserve is full; {_processor.PendingMilliliters} mL stays in the tank."
                    : $"Drained {transferred} mL into the pod reserve."
                : Describe(result);
            return result;
        }

        private void OnDestroy() => ReleasePlayerControls();

        private void OnGUI()
        {
            if (!IsOpen || _processor == null)
            {
                return;
            }

            EnsureStyles();
            _processor.Advance(_clock.ElapsedDays);

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .62f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.035f, .06f, .075f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, panel.width - 40f, panel.height - 32f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("WATER PROCESSOR", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(72f), GUILayout.Height(26f)))
            {
                _pending = PendingAction.Close;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            int carried = CarriedIce();
            GUILayout.Label($"ICE IN BACKPACK · {carried} chunks", _primaryLabelStyle);
            GUILayout.Label(
                $"One chunk yields {WaterProcessor.MillilitersPerIceChunk} mL and is purified in " +
                $"{WaterProcessor.ProcessingGameHoursPerIceChunk:0.0} game hours.", _secondaryLabelStyle);
            GUILayout.Space(12f);

            DrawMachine();

            GUILayout.FlexibleSpace();
            GUILayout.Label("POD RESERVE", _primaryLabelStyle);
            DrawMeter(_waterSupply.CurrentMilliliters, _waterSupply.CapacityMilliliters, new Color(.23f, .55f, .78f));
            GUILayout.Label(
                $"{_waterSupply.CurrentMilliliters / 1000f:0.0} / {_waterSupply.CapacityMilliliters / 1000f:0.0} L",
                _secondaryLabelStyle);
            GUILayout.Space(6f);
            GUILayout.Label(_status, _secondaryLabelStyle);
            GUILayout.EndArea();

            ApplyPendingAction();
        }

        private void DrawMachine()
        {
            switch (_processor.State)
            {
                case WaterProcessorState.Processing:
                    GUILayout.Label($"PURIFYING · {_processor.ProcessingChunks} chunks", _primaryLabelStyle);
                    DrawProgress(_processor.Progress(_clock.ElapsedDays), new Color(.38f, .72f, .95f));
                    GUILayout.Label($"{_processor.RemainingGameHours(_clock.ElapsedDays):0.0} game hours remaining",
                        _secondaryLabelStyle);
                    break;

                case WaterProcessorState.Ready:
                    GUILayout.Label($"POTABLE WATER READY · {_processor.PendingMilliliters / 1000f:0.0} L",
                        _primaryLabelStyle);
                    DrawProgress(1f, new Color(.4f, .85f, .7f));
                    if (GUILayout.Button("DRAIN INTO POD RESERVE", GUILayout.Height(36f)))
                    {
                        _pending = PendingAction.Drain;
                    }
                    break;

                default:
                    DrawLoadControls();
                    break;
            }
        }

        private void DrawLoadControls()
        {
            int maximum = MaximumLoad();
            GUILayout.Label("LOAD ICE", _primaryLabelStyle);
            GUI.enabled = maximum > 0;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", GUILayout.Width(34f), GUILayout.Height(30f)))
            {
                _requestedChunks = Mathf.Max(1, _requestedChunks - 1);
            }

            _requestedChunks = Mathf.Clamp(_requestedChunks, 1, Mathf.Max(1, maximum));
            GUILayout.Label($"{_requestedChunks} chunks  ·  {_requestedChunks * WaterProcessor.MillilitersPerIceChunk / 1000f:0.0} L  ·  " +
                            $"{WaterProcessor.ProcessingGameHours(_requestedChunks):0.0} h",
                _secondaryLabelStyle, GUILayout.Height(30f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(34f), GUILayout.Height(30f)))
            {
                _requestedChunks = Mathf.Min(maximum, _requestedChunks + 1);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("LOAD", GUILayout.Height(36f)))
            {
                _pending = PendingAction.Load;
            }
            GUI.enabled = true;

            if (maximum <= 0)
            {
                GUILayout.Label("Gather ice on the surface to refill the pod reserve.", _secondaryLabelStyle);
            }
        }

        private void ApplyPendingAction()
        {
            PendingAction action = _pending;
            _pending = PendingAction.None;
            switch (action)
            {
                case PendingAction.Load:
                    Load(_requestedChunks);
                    break;
                case PendingAction.Drain:
                    Drain();
                    break;
                case PendingAction.Close:
                    Close();
                    break;
            }
        }

        private int CarriedIce()
        {
            return _playerInventory == null || _iceItem == null
                ? 0
                : _playerInventory.Inventory.GetQuantity(_iceItem.ItemId);
        }

        private int MaximumLoad()
        {
            return _playerInventory == null
                ? 0
                : WaterProcessor.MaximumLoadableChunks(_iceItem, _playerInventory.Inventory);
        }

        private string DefaultStatus()
        {
            return _processor.State switch
            {
                WaterProcessorState.Processing => "A batch is being purified.",
                WaterProcessorState.Ready => "Potable water is waiting to be drained.",
                _ => CarriedIce() > 0
                    ? "Load ice to refill the pod reserve."
                    : "No ice in the backpack."
            };
        }

        private static string Describe(WaterProcessorResult result)
        {
            return result switch
            {
                WaterProcessorResult.AlreadyProcessing => "A batch is already processing.",
                WaterProcessorResult.OutputWaiting => "Drain the potable water before loading more ice.",
                WaterProcessorResult.NotEnoughIce => "Not enough ice in the backpack.",
                WaterProcessorResult.NothingReady => "Nothing has processed yet.",
                WaterProcessorResult.TankFull => "The pod reserve is full.",
                WaterProcessorResult.InvalidRequest => "That load is out of range.",
                _ => string.Empty
            };
        }

        private static void DrawProgress(float normalized, Color fillColor)
        {
            Rect meter = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
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

        private static void DrawMeter(int current, int capacity, Color fillColor)
        {
            DrawProgress(capacity <= 0 ? 0f : (float)current / capacity, fillColor);
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
            _headerStyle.normal.textColor = new Color(.76f, .91f, 1f);
            _primaryLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _primaryLabelStyle.normal.textColor = Color.white;
            _secondaryLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _secondaryLabelStyle.normal.textColor = new Color(.72f, .8f, .84f);
        }
    }
}
