using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Oxygen.Definitions;
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
    public sealed class ElectrolyzerView : InteractionPanelView
    {
        private const string OxygenTextureResource = "Oxygen/Oxygen";
        private const string HydrogenTextureResource = "Hydrogen/Hydrogen";
        private const int WaterTransferMilliliters = 500;
        private const float PanelWidth = 640f;
        private const float PanelHeight = 620f;

        private static readonly Color WaterFill = new(.25f, .58f, .92f);
        private static readonly Color PowerFill = new(.95f, .78f, .3f);
        private static readonly Color OxygenFill = new(.35f, .8f, 1f);
        private static readonly Color HydrogenFill = new(.68f, .55f, .95f);

        private Electrolyzer _electrolyzer;
        private PlayerInventory _inventory;
        private PlayerWaterBottle _waterBottle;
        private PlayerSpaceSuit _spaceSuit;
        private Sprite _panelIcon;
        private Texture2D _oxygenTexture;
        private Texture2D _hydrogenTexture;
        private string _feedback = string.Empty;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Fluid;

        private void Awake()
        {
            _oxygenTexture = Resources.Load<Texture2D>(OxygenTextureResource);
            _hydrogenTexture = Resources.Load<Texture2D>(HydrogenTextureResource);
        }

        public void Open(Electrolyzer electrolyzer, PlayerInventory inventory, PlayerWaterBottle waterBottle,
            PlayerSpaceSuit spaceSuit, PlanarPlayerMotor motor = null, PlayerInteractor interactor = null,
            Sprite panelIcon = null)
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
            _panelIcon = panelIcon;
            _feedback = "Electrolysis needs feed water and a power-pole connection.";
            BeginSession(motor, interactor);
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
            _feedback = collected > 0
                ? $"Took {collected} hydrogen bottle(s)."
                : "Not enough hydrogen for a bottle, or the backpack is full.";
            if (collected > 0) _inventory.RefreshQuickBarAssignments();
            return collected;
        }

        protected override void OnClosed()
        {
            _electrolyzer = null;
            _inventory = null;
            _waterBottle = null;
            _spaceSuit = null;
            _panelIcon = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _electrolyzer == null || _inventory == null)
            {
                return;
            }

            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            bool close = InteractionPanel.DrawHeader(panel, _panelIcon, "WATER ELECTROLYZER", StateText(), Styles);

            GUILayout.BeginArea(InteractionPanel.ContentArea(panel));
            ElectrolyzerDefinition definition = _electrolyzer.Definition;
            GUILayout.Label("INPUTS", Styles.Section);
            InteractionPanel.LayoutMeter("FEED WATER", _electrolyzer.StoredWaterMilliliters / 1000f,
                definition.WaterCapacityMilliliters / 1000f, "L", WaterFill, Styles);
            InteractionPanel.LayoutMeter("ELECTRICITY", _electrolyzer.StoredElectricity,
                definition.ElectricityCapacity, "u", PowerFill, Styles);
            GUILayout.Space(10f);

            GUILayout.Label("OUTPUTS", Styles.Section);
            InteractionPanel.LayoutMeter("OXYGEN", _electrolyzer.StoredOxygenLiters,
                definition.OxygenCapacityLiters, "L", OxygenFill, Styles, _oxygenTexture);
            InteractionPanel.LayoutMeter("HYDROGEN", _electrolyzer.StoredHydrogenLiters,
                definition.HydrogenCapacityLiters, "L", HydrogenFill, Styles, _hydrogenTexture);
            GUILayout.Label(
                $"Bottled oxygen: {_electrolyzer.StoredOxygenItems} / {definition.OxygenItemCapacity}" +
                $" × {definition.OxygenLitersPerItem:0.#} L", Styles.Detail);
            GUILayout.Space(12f);

            int iceChunks = _inventory.Inventory.GetQuantity(definition.IceItem.ItemId);
            GUILayout.Label("FEED THE TANK", Styles.Section);
            GUILayout.BeginHorizontal();
            GUI.enabled = iceChunks > 0 &&
                          _electrolyzer.RemainingWaterCapacity >= definition.WaterMillilitersPerIceChunk;
            if (GUILayout.Button($"MELT ICE ({iceChunks})", GUILayout.Height(32f))) LoadIce(1);
            GUI.enabled = _waterBottle?.Container != null && _waterBottle.Container.CurrentMilliliters > 0 &&
                          _electrolyzer.RemainingWaterCapacity > 0;
            if (GUILayout.Button("POUR IN 0.5 L", GUILayout.Height(32f))) AddWater(WaterTransferMilliliters);
            GUI.enabled = _waterBottle?.Container != null && _electrolyzer.StoredWaterMilliliters > 0 &&
                          _waterBottle.Container.RemainingCapacityMilliliters > 0;
            if (GUILayout.Button("DRAW 0.5 L", GUILayout.Height(32f))) DrawWater(WaterTransferMilliliters);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("TAKE THE GAS", Styles.Section);
            GUILayout.BeginHorizontal();
            GUI.enabled = _electrolyzer.StoredOxygenLiters > 0f;
            if (GUILayout.Button("FILL SUIT O₂", GUILayout.Height(32f))) FillSuitOxygen();
            GUI.enabled = _electrolyzer.StoredOxygenItems > 0;
            if (GUILayout.Button($"TAKE BOTTLES ({_electrolyzer.StoredOxygenItems})", GUILayout.Height(32f)))
            {
                CollectOxygen(_electrolyzer.StoredOxygenItems);
            }
            GUI.enabled = _electrolyzer.StoredHydrogenLiters > 0f;
            if (GUILayout.Button("BOTTLE H₂", GUILayout.Height(32f))) CollectHydrogen(1);
            if (GUILayout.Button("VENT H₂", GUILayout.Height(32f))) VentHydrogen();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            InteractionPanel.DrawFooter(panel, _feedback,
                "Ice, water, the oxygen buffer and the bottle bin all accept network connections.", Styles);
            if (close)
            {
                Close();
            }
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
    }
}
