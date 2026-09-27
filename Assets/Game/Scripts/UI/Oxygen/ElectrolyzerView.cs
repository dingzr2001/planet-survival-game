using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Water.Runtime;
using UnityEngine;

namespace PlanetSurvival.UI.Oxygen
{
    /// <summary>Operations panel for the water electrolyzer: feed water in, take gas and bottles out.</summary>
    [DisallowMultipleComponent]
    public sealed class ElectrolyzerView : MonoBehaviour
    {
        private const string OxygenTextureResource = "Oxygen/Oxygen";
        private const string HydrogenTextureResource = "Hydrogen/Hydrogen";
        private const int WaterTransferMilliliters = 500;
        private const float PanelWidth = 600f;
        private const float PanelHeight = 520f;
        private const float GasIconSize = 26f;

        private Electrolyzer _electrolyzer;
        private PlayerInventory _inventory;
        private PlayerWaterBottle _waterBottle;
        private PlayerSpaceSuit _spaceSuit;
        private PlanarPlayerMotor _motor;
        private PlayerInteractor _interactor;
        private Texture2D _oxygenTexture;
        private Texture2D _hydrogenTexture;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private string _feedback;
        private GUIStyle _header;
        private GUIStyle _detail;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _oxygenTexture = Resources.Load<Texture2D>(OxygenTextureResource);
            _hydrogenTexture = Resources.Load<Texture2D>(HydrogenTextureResource);
        }

        public void Open(Electrolyzer electrolyzer, PlayerInventory inventory, PlayerWaterBottle waterBottle,
            PlayerSpaceSuit spaceSuit, PlanarPlayerMotor motor = null, PlayerInteractor interactor = null)
        {
            if (electrolyzer == null || inventory == null)
            {
                Debug.LogError($"{nameof(ElectrolyzerView)} needs an electrolyzer and the player backpack.", this);
                return;
            }

            Close();
            _electrolyzer = electrolyzer;
            _inventory = inventory;
            _waterBottle = waterBottle;
            _spaceSuit = spaceSuit;
            _motor = motor;
            _interactor = interactor;
            _restoreMotor = _motor != null && _motor.enabled;
            _restoreInteractor = _interactor != null && _interactor.enabled;
            if (_motor != null) _motor.enabled = false;
            if (_interactor != null) _interactor.enabled = false;
            _feedback = "Electrolysis needs feed water and a power-pole connection.";
            IsOpen = true;
        }

        public int LoadIce(int requestedChunks)
        {
            int loaded = _electrolyzer.LoadIceItems(requestedChunks, _inventory.Inventory);
            _feedback = loaded > 0
                ? $"Melted {loaded} ice chunk(s) into the feed tank."
                : "No ice was melted; check the backpack and tank capacity.";
            if (loaded > 0) _inventory.RefreshQuickBarAssignments();
            return loaded;
        }

        public int AddWater(int maximumMilliliters)
        {
            int added = _waterBottle == null
                ? 0
                : _electrolyzer.TransferWaterFrom(_waterBottle.Container, maximumMilliliters);
            _feedback = added > 0
                ? $"Poured {added} mL into the feed tank."
                : "No water was poured; check the suit tank and feed capacity.";
            return added;
        }

        public int DrawWater(int maximumMilliliters)
        {
            int drawn = _waterBottle == null
                ? 0
                : _electrolyzer.DrawWaterTo(_waterBottle.Container, maximumMilliliters);
            _feedback = drawn > 0
                ? $"Drew {drawn} mL into the suit tank."
                : "No water was drawn; check the feed tank and suit capacity.";
            return drawn;
        }

        public float FillSuitOxygen()
        {
            float filled = _spaceSuit?.Resources == null
                ? 0f
                : _electrolyzer.TransferOxygenTo(_spaceSuit.Resources.Oxygen);
            _feedback = filled > 0f
                ? $"Transferred {filled:0.#} L oxygen to the suit."
                : "No oxygen could be transferred.";
            return filled;
        }

        public int CollectOxygen(int requestedItems)
        {
            int collected = _electrolyzer.CollectOxygenItems(requestedItems, _inventory.Inventory);
            _feedback = collected > 0
                ? $"Took {collected} × {_electrolyzer.OutputItem.DisplayName}."
                : "Nothing was collected; check the bottle bin and backpack space.";
            if (collected > 0) _inventory.RefreshQuickBarAssignments();
            return collected;
        }

        public float VentHydrogen()
        {
            float vented = _electrolyzer.VentHydrogen();
            _feedback = vented > 0f
                ? $"Vented {vented:0.#} L hydrogen."
                : "The vent tank is already empty.";
            return vented;
        }

        public int CollectHydrogen(int requestedItems)
        {
            int collected = _electrolyzer.CollectHydrogenItems(requestedItems, _inventory.Inventory);
            _feedback = collected > 0 ? $"Took {collected} hydrogen bottle(s)." :
                "Not enough hydrogen for a bottle, or the backpack is full.";
            if (collected > 0) _inventory.RefreshQuickBarAssignments();
            return collected;
        }

        public void Close()
        {
            IsOpen = false;
            if (_motor != null) _motor.enabled = _restoreMotor;
            if (_interactor != null) _interactor.enabled = _restoreInteractor;
            _electrolyzer = null;
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
            if (!IsOpen || _electrolyzer == null) return;
            EnsureStyles();
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .68f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            PanelBackground.Draw(panel, new Color(.02f, .055f, .08f, .98f));
            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("WATER ELECTROLYZER", _header);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(76), GUILayout.Height(26)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label(StateText(), _detail);
            GUILayout.Space(8);

            GUILayout.Label("INPUTS", _header);
            DrawInputMeters();
            GUILayout.Space(10);
            GUILayout.Label("OUTPUTS", _header);
            DrawMeter("OXYGEN", _electrolyzer.StoredOxygenLiters, _electrolyzer.Definition.OxygenCapacityLiters,
                "L", new Color(.35f, .8f, 1f), _oxygenTexture);
            DrawMeter("HYDROGEN", _electrolyzer.StoredHydrogenLiters,
                _electrolyzer.Definition.HydrogenCapacityLiters, "L", new Color(.68f, .55f, .95f), _hydrogenTexture);
            GUILayout.Label(
                $"Bottled oxygen: {_electrolyzer.StoredOxygenItems} / {_electrolyzer.Definition.OxygenItemCapacity}" +
                $" × {_electrolyzer.Definition.OxygenLitersPerItem:0.#} L", _detail);
            GUILayout.Space(10);

            int iceChunks = _inventory.Inventory.GetQuantity(_electrolyzer.Definition.IceItem.ItemId);
            GUILayout.BeginHorizontal();
            GUI.enabled = iceChunks > 0 &&
                          _electrolyzer.RemainingWaterCapacity >= _electrolyzer.Definition.WaterMillilitersPerIceChunk;
            if (GUILayout.Button($"MELT ICE ({iceChunks})", GUILayout.Height(32))) LoadIce(1);
            GUI.enabled = _waterBottle?.Container != null && _waterBottle.Container.CurrentMilliliters > 0 &&
                          _electrolyzer.RemainingWaterCapacity > 0;
            if (GUILayout.Button("POUR IN 0.5 L", GUILayout.Height(32))) AddWater(WaterTransferMilliliters);
            GUI.enabled = _waterBottle?.Container != null && _electrolyzer.StoredWaterMilliliters > 0 &&
                          _waterBottle.Container.RemainingCapacityMilliliters > 0;
            if (GUILayout.Button("DRAW 0.5 L", GUILayout.Height(32))) DrawWater(WaterTransferMilliliters);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _electrolyzer.StoredOxygenLiters > 0f;
            if (GUILayout.Button("FILL SUIT O₂", GUILayout.Height(32))) FillSuitOxygen();
            GUI.enabled = _electrolyzer.StoredOxygenItems > 0;
            if (GUILayout.Button($"TAKE BOTTLES ({_electrolyzer.StoredOxygenItems})", GUILayout.Height(32)))
            {
                CollectOxygen(_electrolyzer.StoredOxygenItems);
            }
            GUI.enabled = _electrolyzer.StoredHydrogenLiters > 0f;
            if (GUILayout.Button("BOTTLE H₂", GUILayout.Height(32))) CollectHydrogen(1);
            if (GUILayout.Button("VENT H₂", GUILayout.Height(32))) VentHydrogen();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label(
                "Ice and water inputs, the oxygen buffer and the bottle bin all accept network connections.",
                _detail);
            GUILayout.Label(_feedback, _detail);
            GUILayout.EndArea();
        }

        private void DrawInputMeters()
        {
            DrawMeter("FEED WATER", _electrolyzer.StoredWaterMilliliters / 1000f,
                _electrolyzer.Definition.WaterCapacityMilliliters / 1000f, "L",
                new Color(.25f, .58f, .92f), null);
            DrawMeter("ELECTRICITY", _electrolyzer.StoredElectricity,
                _electrolyzer.Definition.ElectricityCapacity, "u", new Color(.95f, .78f, .3f), null);
        }

        private string StateText() => _electrolyzer.State switch
        {
            ElectrolyzerState.NeedsWater => "Stopped: the feed tank is dry. Melt ice or pour water in.",
            ElectrolyzerState.NeedsPower =>
                $"Stopped: no stored power. Route {_electrolyzer.Definition.ElectricityPerSecond:0.#} units/s " +
                "through a power pole.",
            ElectrolyzerState.HydrogenStorageFull => "Stopped: the vent tank is full. Release the hydrogen to resume.",
            ElectrolyzerState.OxygenStorageFull => "Stopped: the gas buffer and the bottle bin are both full.",
            _ => $"Running · {_electrolyzer.Definition.OxygenLitersPerSecond:0.#} L O₂/s from " +
                 $"{_electrolyzer.Definition.WaterMillilitersPerOxygenLiter:0.#} mL water and " +
                 $"{_electrolyzer.Definition.ElectricityPerOxygenLiter:0.##} units per litre."
        };

        private static void DrawMeter(string label, float value, float capacity, string unit, Color color,
            Texture2D icon)
        {
            GUILayout.BeginHorizontal();
            Rect iconRect = GUILayoutUtility.GetRect(GasIconSize, GasIconSize,
                GUILayout.Width(GasIconSize), GUILayout.Height(GasIconSize));
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            GUILayout.BeginVertical();
            GUILayout.Label($"{label}  {value:0.#} / {capacity:0.#} {unit}");
            Rect meter = GUILayoutUtility.GetRect(10, 14, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(meter, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0,
                new Color(.08f, .09f, .1f), Vector4.zero, new Vector4(4, 4, 4, 4));
            float fill = capacity > 0f ? Mathf.Clamp01(value / capacity) : 0f;
            if (fill > 0f)
            {
                GUI.DrawTexture(new Rect(meter.x + 2, meter.y + 2, (meter.width - 4) * fill, meter.height - 4),
                    Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, Vector4.zero,
                    new Vector4(3, 3, 3, 3));
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            if (_header != null) return;
            _header = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _header.normal.textColor = new Color(.52f, .86f, 1f);
            _detail = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _detail.normal.textColor = new Color(.8f, .86f, .9f);
        }
    }
}
