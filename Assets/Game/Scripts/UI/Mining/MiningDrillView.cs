using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Mining.Definitions;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using UnityEngine;

namespace PlanetSurvival.UI.Mining
{
    /// <summary>
    /// Operations panel for one drill: fuel on the left, the shaft it is cutting in the middle, the ore
    /// buffer on the right.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MiningDrillView : InteractionPanelView
    {
        private const float PanelWidth = 900f;
        private const float PanelHeight = 560f;

        /// <summary>Room the output column keeps clear for its headline.</summary>
        private const float HeadlineInset = 52f;

        /// <summary>Height the input column reserves at its foot for the electric buffer readout.</summary>
        private const float PowerMeterBand = 56f;

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
            _feedback = $"Load petroleum or collect finished {_drill.OutputItem.DisplayName.ToLowerInvariant()}.";
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

            InteractionPanel.MachineColumns columns = InteractionPanel.DrawColumns(panel, Theme);
            // The fuel slot shares its column with the power meter pinned to the bottom.
            Rect fuelColumn = new(columns.Inputs.x, columns.Inputs.y, columns.Inputs.width,
                columns.Inputs.height - PowerMeterBand);
            float inputSlot = InteractionPanel.SlotSizeForStack(panel, fuelColumn, 1, true);
            float outputSlot = InteractionPanel.SlotSizeForStack(panel, columns.Outputs, 1, true, HeadlineInset);
            float actionRow = InteractionPanel.SlotCaptionHeight + 12f;

            DrawPetroleumSlot(InteractionPanel.StackedSlot(fuelColumn, 0, 1, inputSlot, true), actionRow);
            DrawPowerMeter(columns.Inputs);
            DrawShaft(columns.Process);
            DrawOreSlot(
                InteractionPanel.StackedSlot(columns.Outputs, 0, 1, outputSlot, true, HeadlineInset), actionRow);
            InteractionPanel.Headline(columns.Outputs,
                $"⛏ {_drill.Definition.ProductionPerSecond:0.##} / s", Styles);

            InteractionPanel.DrawFooter(panel, _feedback,
                "Electricity is spent before petroleum, so a powered drill saves canisters.", Styles);
            if (close)
            {
                Close();
            }
        }

        private void DrawPetroleumSlot(Rect slot, float actionRow)
        {
            MiningDrillDefinition definition = _drill.Definition;
            int carried = _playerInventory.Inventory.GetQuantity(definition.PetroleumItem.ItemId);
            bool canLoad = carried > 0 && _drill.RemainingPetroleumCapacity >= definition.PetroleumPerItem;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canLoad)
            {
                LoadPetroleum(1);
            }

            InteractionPanel.SlotIcon(slot, definition.PetroleumItem.Icon, null, "FUEL", Styles);
            InteractionPanel.SlotCaption(slot, definition.PetroleumItem.DisplayName,
                $"{_drill.StoredPetroleum:0.#} / {definition.PetroleumCapacity:0.#}  ·  {carried} carried", Styles);
            InteractionPanel.SlotMeter(slot, _drill.StoredPetroleum, definition.PetroleumCapacity, 0f,
                PetroleumFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, "LOAD 1", canLoad, 0, 2))
            {
                LoadPetroleum(1);
            }

            if (InteractionPanel.SlotAction(slot, actionRow, "LOAD ALL", canLoad, 1, 2))
            {
                LoadPetroleum(carried);
            }
        }

        private void DrawPowerMeter(Rect column)
        {
            var band = new Rect(column.x + 20f, column.yMax - 52f, column.width - 40f, 18f);
            GUI.Label(band, $"ELECTRIC BUFFER  {_drill.StoredElectricity:0.#} / " +
                            $"{_drill.Definition.ElectricityCapacity:0.#} u", Styles.Detail);
            InteractionPanel.Meter(new Rect(band.x, band.yMax + 2f, band.width, 8f),
                _drill.StoredElectricity, _drill.Definition.ElectricityCapacity, ElectricityFill);
        }

        /// <summary>The middle column: the bit turning while the drill cuts, and how far into the next item.</summary>
        private void DrawShaft(Rect column)
        {
            var chamberRect = new Rect(column.x + 22f, column.y + 30f, column.width - 44f, column.height - 60f);
            Rect inner = InteractionPanel.Chamber(chamberRect, "DRILL SHAFT", Styles, Theme);
            bool cutting = _drill.State == MiningDrillState.Producing;

            // Rock face, then the bit descending into it; the bit only bites while the drill runs.
            float faceY = inner.y + inner.height * .55f;
            InteractionPanel.Fill(new Rect(inner.x + 12f, faceY, inner.width - 24f, inner.yMax - faceY - 4f),
                Theme.Track);
            float bob = cutting ? Mathf.PingPong(Time.unscaledTime * 6f, 6f) : 0f;
            float shaftX = inner.center.x - 5f;
            InteractionPanel.Fill(new Rect(shaftX, inner.y + 10f, 10f, faceY - inner.y - 4f + bob),
                cutting ? Theme.Accent : Theme.Frame);
            for (int i = 0; i < 3; i++)
            {
                float tipWidth = 26f - i * 8f;
                InteractionPanel.Fill(
                    new Rect(inner.center.x - tipWidth * .5f, faceY - 12f + i * 6f + bob, tipWidth, 5f),
                    cutting ? Theme.Accent : Theme.Frame);
            }

            if (cutting)
            {
                // Chips fly only while cutting, so the picture never implies work that is not happening.
                for (int i = 0; i < 4; i++)
                {
                    float phase = Mathf.Repeat(Time.unscaledTime * 1.6f + i * .25f, 1f);
                    float x = Mathf.Lerp(inner.center.x, inner.center.x + (i % 2 == 0 ? -1f : 1f) * 60f, phase);
                    float y = Mathf.Lerp(faceY, faceY - 26f, Mathf.Sin(phase * Mathf.PI));
                    InteractionPanel.Fill(new Rect(x, y, 3f, 3f),
                        new Color(OreFill.r, OreFill.g, OreFill.b, 1f - phase));
                }
            }

            InteractionPanel.ChamberStatus(chamberRect, cutting,
                cutting ? $"CUTTING  ·  {_drill.ProductionProgress:P0} of the next item" : StopReason(),
                cutting ? _drill.ProductionProgress : 0f, Styles, Theme);
        }

        private void DrawOreSlot(Rect slot, float actionRow)
        {
            bool canTake = _drill.StoredOre > 0;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canTake)
            {
                CollectOre(_drill.StoredOre);
            }

            InteractionPanel.SlotIcon(slot, _drill.OutputItem.Icon, null, "ORE", Styles);
            InteractionPanel.SlotCaption(slot, _drill.OutputItem.DisplayName,
                $"{_drill.StoredOre} / {_drill.Definition.OreCapacity}", Styles);
            InteractionPanel.SlotMeter(slot, _drill.StoredOre, _drill.Definition.OreCapacity, 0f, OreFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, "TAKE 1", canTake, 0, 2))
            {
                CollectOre(1);
            }

            if (InteractionPanel.SlotAction(slot, actionRow, "TAKE ALL", canTake, 1, 2))
            {
                CollectOre(_drill.StoredOre);
            }
        }

        private string StopReason() => _drill.State == MiningDrillState.StorageFull
            ? "STORAGE FULL"
            : "NO POWER OR FUEL";

        private string StateDescription() => _drill.State switch
        {
            MiningDrillState.StorageFull => "Stopped: output storage is full.",
            MiningDrillState.Producing => $"Producing {_drill.Definition.ProductionPerSecond:0.##} items/s.",
            _ => "Stopped: connect electricity or load petroleum."
        };
    }
}
