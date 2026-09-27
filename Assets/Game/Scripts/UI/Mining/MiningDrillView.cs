using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using UnityEngine;

namespace PlanetSurvival.UI.Mining
{
    /// <summary>Operations panel for hand-loading petroleum and collecting buffered ore.</summary>
    [DisallowMultipleComponent]
    public sealed class MiningDrillView : InteractionPanelView
    {
        private const float PanelWidth = 600f;
        private const float PanelHeight = 480f;

        private static readonly Color OreFill = new(.72f, .28f, .12f);
        private static readonly Color PetroleumFill = new(.78f, .58f, .16f);
        private static readonly Color ElectricityFill = new(.3f, .7f, 1f);

        private MiningDrill _drill;
        private PlayerInventory _playerInventory;
        private Sprite _panelIcon;
        private string _feedback = string.Empty;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Thermal;

        public void Open(MiningDrill drill, PlayerInventory playerInventory,
            PlanarPlayerMotor playerMotor = null, PlayerInteractor playerInteractor = null,
            Sprite panelIcon = null)
        {
            if (drill == null || playerInventory == null)
            {
                Debug.LogError($"{nameof(MiningDrillView)} needs a drill and the player backpack.", this);
                return;
            }

            Close();
            _drill = drill;
            _playerInventory = playerInventory;
            _panelIcon = panelIcon;
            _feedback = $"Load petroleum fuel or collect finished {_drill.OutputItem.DisplayName.ToLowerInvariant()}.";
            BeginSession(playerMotor, playerInteractor);
        }

        public int LoadPetroleum(int requestedItems)
        {
            if (_drill == null || _playerInventory == null)
            {
                return 0;
            }

            int loaded = _drill.LoadPetroleumItems(requestedItems, _playerInventory.Inventory);
            _feedback = loaded > 0
                ? $"Loaded {loaded} petroleum canister(s)."
                : "No petroleum was loaded; check the backpack and tank space.";
            if (loaded > 0)
            {
                _playerInventory.RefreshQuickBarAssignments();
            }

            return loaded;
        }

        public int CollectOre(int requestedItems)
        {
            if (_drill == null || _playerInventory == null)
            {
                return 0;
            }

            int collected = _drill.CollectOre(requestedItems, _playerInventory.Inventory);
            _feedback = collected > 0
                ? $"Collected {collected} {_drill.OutputItem.DisplayName.ToLowerInvariant()}."
                : "No output was collected; check the machine and backpack capacity.";
            if (collected > 0)
            {
                _playerInventory.RefreshQuickBarAssignments();
            }

            return collected;
        }

        protected override void OnClosed()
        {
            _drill = null;
            _playerInventory = null;
            _panelIcon = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _drill == null || _playerInventory == null)
            {
                return;
            }

            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            bool close = InteractionPanel.DrawHeader(panel, _panelIcon,
                _drill.Definition.DisplayName.ToUpperInvariant(), StateDescription(), Styles);

            GUILayout.BeginArea(InteractionPanel.ContentArea(panel));
            GUILayout.Label("MACHINE", Styles.Section);
            InteractionPanel.LayoutMeter("OUTPUT STORAGE", _drill.StoredOre, _drill.Definition.OreCapacity,
                _drill.OutputItem.DisplayName.ToLowerInvariant(), OreFill, Styles);
            InteractionPanel.LayoutMeter("PETROLEUM", _drill.StoredPetroleum,
                _drill.Definition.PetroleumCapacity, "u", PetroleumFill, Styles);
            InteractionPanel.LayoutMeter("ELECTRIC BUFFER", _drill.StoredElectricity,
                _drill.Definition.ElectricityCapacity, "u", ElectricityFill, Styles);
            GUILayout.Space(14f);

            int petroleumInBackpack = _playerInventory.Inventory.GetQuantity(_drill.Definition.PetroleumItem.ItemId);
            GUILayout.Label($"BACKPACK PETROLEUM: {petroleumInBackpack}", Styles.Section);
            GUILayout.BeginHorizontal();
            GUI.enabled = petroleumInBackpack > 0 &&
                          _drill.RemainingPetroleumCapacity >= _drill.Definition.PetroleumPerItem;
            if (GUILayout.Button("LOAD 1", GUILayout.Height(34f))) LoadPetroleum(1);
            if (GUILayout.Button("LOAD ALL", GUILayout.Height(34f))) LoadPetroleum(petroleumInBackpack);
            GUI.enabled = _drill.StoredOre > 0;
            if (GUILayout.Button("TAKE 1", GUILayout.Height(34f))) CollectOre(1);
            if (GUILayout.Button("TAKE ALL", GUILayout.Height(34f))) CollectOre(_drill.StoredOre);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            InteractionPanel.DrawFooter(panel, _feedback,
                "Electricity is spent before petroleum, so a powered drill saves its canisters.", Styles);
            if (close)
            {
                Close();
            }
        }

        private string StateDescription() => _drill.State switch
        {
            MiningDrillState.StorageFull => "Stopped: output storage is full.",
            MiningDrillState.Producing =>
                $"Producing {_drill.Definition.ProductionPerSecond:0.##} items/s.",
            _ => "Stopped: connect electricity or load petroleum."
        };
    }
}
