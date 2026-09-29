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
    /// <summary>
    /// Operations panel for the water electrolyzer: ice and water feed the tank on the left, the cell
    /// splits it in the middle, oxygen and hydrogen come out on the right.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElectrolyzerView : InteractionPanelView
    {
        private const string OxygenTextureResource = "Oxygen/Oxygen";
        private const string HydrogenTextureResource = "Hydrogen/Hydrogen";
        private const string WaterTextureResource = "Water/WaterBottle";
        private const int WaterTransferMilliliters = 500;
        private const float PanelWidth = 940f;
        private const float PanelHeight = 660f;

        /// <summary>Room the output column keeps clear for its headline.</summary>
        private const float HeadlineInset = 52f;

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
        private Texture2D _waterTexture;
        private string _feedback = string.Empty;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Fluid;

        private void Awake()
        {
            _oxygenTexture = Resources.Load<Texture2D>(OxygenTextureResource);
            _hydrogenTexture = Resources.Load<Texture2D>(HydrogenTextureResource);
            _waterTexture = Resources.Load<Texture2D>(WaterTextureResource);
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
            _feedback = vented > 0f ? $"Vented {vented:0.#} L hydrogen." : "The vent tank is already empty.";
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

            InteractionPanel.MachineColumns columns = InteractionPanel.DrawColumns(panel, Theme);
            float inputSlot = InteractionPanel.SlotSizeForStack(panel, columns.Inputs, 2, true);
            float outputSlot = InteractionPanel.SlotSizeForStack(panel, columns.Outputs, 2, true, HeadlineInset);
            float actionRow = InteractionPanel.SlotCaptionHeight + 12f;

            DrawIceSlot(InteractionPanel.StackedSlot(columns.Inputs, 0, 2, inputSlot, true), actionRow);
            DrawFeedWaterSlot(InteractionPanel.StackedSlot(columns.Inputs, 1, 2, inputSlot, true), actionRow);
            DrawCell(columns.Process);
            DrawOxygenSlot(
                InteractionPanel.StackedSlot(columns.Outputs, 0, 2, outputSlot, true, HeadlineInset), actionRow);
            DrawHydrogenSlot(
                InteractionPanel.StackedSlot(columns.Outputs, 1, 2, outputSlot, true, HeadlineInset), actionRow);
            InteractionPanel.Headline(columns.Outputs,
                $"⚡ O₂ {_electrolyzer.Definition.OxygenLitersPerSecond:0.##} L/s", Styles);

            InteractionPanel.DrawFooter(panel, _feedback,
                "Every tank here also accepts a pipe or power connection.", Styles);
            if (close)
            {
                Close();
            }
        }

        private void DrawIceSlot(Rect slot, float actionRow)
        {
            ElectrolyzerDefinition definition = _electrolyzer.Definition;
            int chunks = _inventory.Inventory.GetQuantity(definition.IceItem.ItemId);
            bool canMelt = chunks > 0 &&
                           _electrolyzer.RemainingWaterCapacity >= definition.WaterMillilitersPerIceChunk;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canMelt)
            {
                LoadIce(1);
            }

            InteractionPanel.SlotIcon(slot, definition.IceItem.Icon, null, "ICE", Styles);
            InteractionPanel.SlotCaption(slot, definition.IceItem.DisplayName, $"{chunks} in backpack", Styles);
            if (InteractionPanel.SlotAction(slot, actionRow, "MELT ONE", canMelt))
            {
                LoadIce(1);
            }
        }

        private void DrawFeedWaterSlot(Rect slot, float actionRow)
        {
            ElectrolyzerDefinition definition = _electrolyzer.Definition;
            bool canPour = _waterBottle?.Container != null && _waterBottle.Container.CurrentMilliliters > 0 &&
                           _electrolyzer.RemainingWaterCapacity > 0;
            bool canDraw = _waterBottle?.Container != null && _electrolyzer.StoredWaterMilliliters > 0 &&
                           _waterBottle.Container.RemainingCapacityMilliliters > 0;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canPour)
            {
                AddWater(WaterTransferMilliliters);
            }

            InteractionPanel.SlotIcon(slot, null, _waterTexture, "H₂O", Styles);
            InteractionPanel.SlotCaption(slot, "Feed Water",
                $"{_electrolyzer.StoredWaterMilliliters} / {definition.WaterCapacityMilliliters} mL", Styles);
            InteractionPanel.SlotMeter(slot, _electrolyzer.StoredWaterMilliliters,
                definition.WaterCapacityMilliliters, 0f, WaterFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, "POUR", canPour, 0, 2))
            {
                AddWater(WaterTransferMilliliters);
            }

            if (InteractionPanel.SlotAction(slot, actionRow, "DRAW", canDraw, 1, 2))
            {
                DrawWater(WaterTransferMilliliters);
            }
        }

        /// <summary>The middle column: water splitting into the two gases, with the power it is drawing.</summary>
        private void DrawCell(Rect column)
        {
            var chamberRect = new Rect(column.x + 22f, column.y + 26f, column.width - 44f, column.height - 52f);
            Rect inner = InteractionPanel.Chamber(chamberRect, "ELECTROLYSIS CELL", Styles, Theme);
            ElectrolyzerDefinition definition = _electrolyzer.Definition;
            bool running = _electrolyzer.State == ElectrolyzerState.Producing;

            float iconSize = Mathf.Min(64f, inner.height * .34f);
            float centreY = inner.y + inner.height * .22f;
            var waterRect = new Rect(inner.center.x - iconSize * .5f, centreY, iconSize, iconSize);
            if (_waterTexture != null)
            {
                GUI.DrawTexture(waterRect, _waterTexture, ScaleMode.ScaleToFit, true);
            }

            // Bubbles rise only while the cell is actually splitting water, so the picture matches the state.
            if (running)
            {
                for (int i = 0; i < 6; i++)
                {
                    float phase = Mathf.Repeat(Time.unscaledTime * .8f + i * .17f, 1f);
                    float x = inner.x + inner.width * (.22f + .11f * i);
                    float y = Mathf.Lerp(inner.yMax - 24f, waterRect.yMax, phase);
                    InteractionPanel.Fill(new Rect(x, y, 4f, 4f),
                        new Color(OxygenFill.r, OxygenFill.g, OxygenFill.b, 1f - phase));
                }
            }

            float gasSize = Mathf.Min(44f, inner.height * .24f);
            float gasY = inner.yMax - gasSize - 6f;
            DrawGasMarker(new Rect(inner.x + inner.width * .24f - gasSize * .5f, gasY, gasSize, gasSize),
                _oxygenTexture, "O₂", $"{_electrolyzer.StoredOxygenLiters:0.#} L");
            DrawGasMarker(new Rect(inner.x + inner.width * .76f - gasSize * .5f, gasY, gasSize, gasSize),
                _hydrogenTexture, "H₂", $"{_electrolyzer.StoredHydrogenLiters:0.#} L");

            InteractionPanel.ChamberStatus(chamberRect, running,
                running
                    ? $"RUNNING  ·  {definition.ElectricityPerSecond:0.#} u/s"
                    : StopReason(),
                definition.ElectricityCapacity > 0f
                    ? _electrolyzer.StoredElectricity / definition.ElectricityCapacity
                    : 0f,
                Styles, Theme);

            var powerBand = new Rect(chamberRect.x, chamberRect.yMax - 58f, chamberRect.width, 14f);
            GUI.Label(new Rect(powerBand.x + 12f, powerBand.y - 4f, powerBand.width - 24f, 18f),
                $"STORED POWER {_electrolyzer.StoredElectricity:0.#} / {definition.ElectricityCapacity:0.#} u",
                Styles.Detail);
            InteractionPanel.Meter(new Rect(powerBand.x + 12f, powerBand.yMax, powerBand.width - 24f, 6f),
                _electrolyzer.StoredElectricity, definition.ElectricityCapacity, PowerFill);
        }

        private void DrawGasMarker(Rect rect, Texture2D texture, string glyph, string amount)
        {
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(rect, glyph, Styles.Glyph);
            }

            GUI.Label(new Rect(rect.x - 20f, rect.yMax - 2f, rect.width + 40f, 18f), amount, Styles.Note);
        }

        private void DrawOxygenSlot(Rect slot, float actionRow)
        {
            ElectrolyzerDefinition definition = _electrolyzer.Definition;
            bool canTake = _electrolyzer.StoredOxygenItems > 0;
            bool canFillSuit = _electrolyzer.StoredOxygenLiters > 0f;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canTake)
            {
                CollectOxygen(_electrolyzer.StoredOxygenItems);
            }

            InteractionPanel.SlotIcon(slot, definition.OxygenItem != null ? definition.OxygenItem.Icon : null,
                _oxygenTexture, "O₂", Styles);
            InteractionPanel.SlotCaption(slot, "Oxygen Bottles",
                $"{_electrolyzer.StoredOxygenItems} / {definition.OxygenItemCapacity}", Styles);
            InteractionPanel.SlotMeter(slot, _electrolyzer.StoredOxygenLiters,
                definition.OxygenCapacityLiters, 0f, OxygenFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, "TAKE", canTake, 0, 2))
            {
                CollectOxygen(_electrolyzer.StoredOxygenItems);
            }

            if (InteractionPanel.SlotAction(slot, actionRow, "SUIT", canFillSuit, 1, 2))
            {
                FillSuitOxygen();
            }
        }

        private void DrawHydrogenSlot(Rect slot, float actionRow)
        {
            ElectrolyzerDefinition definition = _electrolyzer.Definition;
            bool hasGas = _electrolyzer.StoredHydrogenLiters > 0f;
            bool canBottle = _electrolyzer.StoredHydrogenLiters >= definition.HydrogenLitersPerItem;
            if (InteractionPanel.SlotButton(slot, false, Theme) && canBottle)
            {
                CollectHydrogen(1);
            }

            InteractionPanel.SlotIcon(slot, definition.HydrogenItem != null ? definition.HydrogenItem.Icon : null,
                _hydrogenTexture, "H₂", Styles);
            InteractionPanel.SlotCaption(slot, "Hydrogen",
                $"{_electrolyzer.StoredHydrogenLiters:0.#} / {definition.HydrogenCapacityLiters:0.#} L", Styles);
            InteractionPanel.SlotMeter(slot, _electrolyzer.StoredHydrogenLiters,
                definition.HydrogenCapacityLiters, 0f, HydrogenFill, Theme);
            if (InteractionPanel.SlotAction(slot, actionRow, "BOTTLE", canBottle, 0, 2))
            {
                CollectHydrogen(1);
            }

            if (InteractionPanel.SlotAction(slot, actionRow, "VENT", hasGas, 1, 2))
            {
                VentHydrogen();
            }
        }

        private string StopReason() => _electrolyzer.State switch
        {
            ElectrolyzerState.NeedsWater => "DRY  ·  no feed water",
            ElectrolyzerState.NeedsPower => "NO POWER",
            ElectrolyzerState.HydrogenStorageFull => "VENT TANK FULL",
            ElectrolyzerState.OxygenStorageFull => "OXYGEN STORAGE FULL",
            _ => "STANDBY"
        };

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
