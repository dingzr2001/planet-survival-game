using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Water.Runtime;
using UnityEngine;

namespace PlanetSurvival.UI.Farming
{
    /// <summary>Operations panel for manual planter inputs and oxygen collection.</summary>
    [DisallowMultipleComponent]
    public sealed class PlanterBoxView : MonoBehaviour
    {
        private const int WaterTransferMilliliters = 500;
        private const float PanelWidth = 580f;
        private const float PanelHeight = 470f;

        private PlanterBox _planter;
        private PlayerInventory _inventory;
        private PlayerWaterBottle _waterBottle;
        private PlayerSpaceSuit _spaceSuit;
        private PlanarPlayerMotor _motor;
        private PlayerInteractor _interactor;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private string _feedback;
        private GUIStyle _header;
        private GUIStyle _detail;

        public bool IsOpen { get; private set; }

        public void Open(PlanterBox planter, PlayerInventory inventory, PlayerWaterBottle waterBottle,
            PlayerSpaceSuit spaceSuit, PlanarPlayerMotor motor = null, PlayerInteractor interactor = null)
        {
            if (planter == null || inventory == null)
            {
                Debug.LogError($"{nameof(PlanterBoxView)} needs a planter and player inventory.", this);
                return;
            }

            Close();
            _planter = planter;
            _inventory = inventory;
            _waterBottle = waterBottle;
            _spaceSuit = spaceSuit;
            _motor = motor;
            _interactor = interactor;
            _restoreMotor = _motor != null && _motor.enabled;
            _restoreInteractor = _interactor != null && _interactor.enabled;
            if (_motor != null) _motor.enabled = false;
            if (_interactor != null) _interactor.enabled = false;
            _feedback = "Water and CO₂ must both reach their minimum marks before photosynthesis starts.";
            IsOpen = true;
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
            _feedback = filled > 0f ? $"Transferred {filled:0.#} L oxygen to the suit." : "No oxygen could be transferred.";
            return filled;
        }

        public void Close()
        {
            IsOpen = false;
            if (_motor != null) _motor.enabled = _restoreMotor;
            if (_interactor != null) _interactor.enabled = _restoreInteractor;
            _planter = null;
            _inventory = null;
            _waterBottle = null;
            _spaceSuit = null;
            _motor = null;
            _interactor = null;
        }

        private void OnDisable() => Close();
        private void OnDestroy() => Close();

        private void OnGUI()
        {
            if (!IsOpen || _planter == null) return;
            EnsureStyles();
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .68f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            PanelBackground.Draw(panel, new Color(.025f, .075f, .045f, .98f));
            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("PLANTER BOX", _header);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(76), GUILayout.Height(26)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label(StateText(), _detail);
            DrawMeter("WATER", _planter.StoredWaterMilliliters, _planter.Definition.WaterCapacityMilliliters,
                _planter.Definition.MinimumWaterMilliliters, new Color(.2f, .55f, .9f));
            DrawMeter("CARBON DIOXIDE", _planter.StoredCarbonDioxideLiters,
                _planter.Definition.CarbonDioxideCapacityLiters, _planter.Definition.MinimumCarbonDioxideLiters,
                new Color(.65f, .65f, .65f));
            DrawMeter("OXYGEN OUTPUT", _planter.StoredOxygenLiters, _planter.Definition.OxygenCapacityLiters, 0f,
                new Color(.35f, .85f, .65f));
            GUILayout.Space(12);
            int canisters = _inventory.Inventory.GetQuantity(_planter.Definition.CarbonDioxideCanister.ItemId);
            GUILayout.BeginHorizontal();
            GUI.enabled = _waterBottle?.Container != null && _waterBottle.Container.CurrentMilliliters > 0
                && _planter.RemainingWaterCapacity > 0;
            if (GUILayout.Button("ADD 0.5 L WATER", GUILayout.Height(34))) AddWater(WaterTransferMilliliters);
            GUI.enabled = canisters > 0 &&
                          _planter.RemainingCarbonDioxideCapacity >=
                          _planter.Definition.CarbonDioxideLitersPerCanister;
            if (GUILayout.Button($"LOAD CO₂ ({canisters})", GUILayout.Height(34))) LoadCarbonDioxide(1);
            GUI.enabled = _planter.StoredOxygenLiters > 0f;
            if (GUILayout.Button("FILL SUIT O₂", GUILayout.Height(34))) FillSuitOxygen();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Water/CO₂ inputs and oxygen output also accept pipe-network connections.", _detail);
            GUILayout.Label(_feedback, _detail);
            GUILayout.EndArea();
        }

        private string StateText() => _planter.State switch
        {
            PlanterBoxState.NeedsWater => "Stopped: water is below the required minimum.",
            PlanterBoxState.NeedsCarbonDioxide => "Stopped: carbon dioxide is below the required minimum.",
            PlanterBoxState.OxygenStorageFull => "Stopped: oxygen output storage is full.",
            _ => $"Producing {_planter.Definition.OxygenLitersPerSecond:0.##} L oxygen/s."
        };

        private static void DrawMeter(string label, float value, float capacity, float minimum, Color color)
        {
            GUILayout.Label(
                $"{label}  {value:0.#} / {capacity:0.#}  " +
                (minimum > 0f ? $"· minimum {minimum:0.#}" : string.Empty));
            Rect meter = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(meter, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.08f, .1f, .08f), Vector4.zero, new Vector4(4, 4, 4, 4));
            float fill = capacity > 0f ? Mathf.Clamp01(value / capacity) : 0f;
            if (fill > 0f) GUI.DrawTexture(new Rect(meter.x + 2, meter.y + 2, (meter.width - 4) * fill, meter.height - 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, Vector4.zero, new Vector4(3, 3, 3, 3));
            if (minimum > 0f)
            {
                float x = meter.x + meter.width * Mathf.Clamp01(minimum / capacity);
                GUI.DrawTexture(new Rect(x - 1, meter.y, 2, meter.height), Texture2D.whiteTexture);
            }
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(.5f, .95f, .62f);
            _detail = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _detail.normal.textColor = new Color(.82f, .88f, .82f);
        }
    }
}
