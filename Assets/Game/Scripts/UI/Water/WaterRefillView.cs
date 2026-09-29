using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.UI.Water
{
    /// <summary>
    /// Operations panel for the landing pod dispenser: the pod reserve on the left, the line between them
    /// in the middle, the explorer's bottle on the right.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterRefillView : InteractionPanelView
    {
        private const string BottleTextureResource = "Water/WaterBottle";
        private const string DropTextureResource = "Water/WaterDrop";
        private const float PanelWidth = 820f;
        private const float PanelHeight = 460f;

        /// <summary>Room the output column keeps clear for its headline.</summary>
        private const float HeadlineInset = 52f;

        private static readonly Color BottleFill = new(.28f, .72f, .95f);
        private static readonly Color ReserveFill = new(.23f, .55f, .78f);

        private LiquidContainer _waterSupply;
        private LiquidContainer _bottle;
        private Texture2D _bottleTexture;
        private Texture2D _dropTexture;
        private string _feedback = string.Empty;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Fluid;

        private void Awake()
        {
            _bottleTexture = Resources.Load<Texture2D>(BottleTextureResource);
            _dropTexture = Resources.Load<Texture2D>(DropTextureResource);
        }

        public void Open(LiquidContainer waterSupply, LiquidContainer bottle,
            PlanarPlayerMotor playerMotor = null, PlayerInteractor playerInteractor = null)
        {
            if (waterSupply == null || bottle == null)
            {
                Debug.LogError($"{nameof(WaterRefillView)} cannot open without a water supply and bottle.", this);
                return;
            }

            Close();
            _waterSupply = waterSupply;
            _bottle = bottle;
            _feedback = bottle.RemainingCapacityMilliliters == 0
                ? "Bottle is already full."
                : "Select FILL BOTTLE to transfer water.";
            BeginSession(playerMotor, playerInteractor);
        }

        /// <summary>Moves what the reserve can spare into the bottle. Exposed so tests skip IMGUI.</summary>
        public int FillBottle()
        {
            if (_waterSupply == null || _bottle == null)
            {
                return 0;
            }

            int transferred = _bottle.FillFrom(_waterSupply);
            _feedback = transferred > 0
                ? $"Transferred {transferred} mL. Bottle sealed."
                : _bottle.RemainingCapacityMilliliters == 0
                    ? "Bottle is already full."
                    : "Landing pod water reserve is empty.";
            return transferred;
        }

        protected override void OnClosed()
        {
            _waterSupply = null;
            _bottle = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _waterSupply == null || _bottle == null)
            {
                return;
            }

            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            bool close = InteractionPanel.DrawHeader(panel, null, "WATER DISPENSER", StateText(), Styles);

            InteractionPanel.MachineColumns columns = InteractionPanel.DrawColumns(panel, Theme);
            float slotSize = Mathf.Min(
                InteractionPanel.SlotSizeForStack(panel, columns.Inputs, 1, false),
                InteractionPanel.SlotSizeForStack(panel, columns.Outputs, 1, true, HeadlineInset));
            bool canFill = _bottle.RemainingCapacityMilliliters > 0 && _waterSupply.CurrentMilliliters > 0;

            Rect reserve = InteractionPanel.StackedSlot(columns.Inputs, 0, 1, slotSize, true);
            InteractionPanel.SlotButton(reserve, false, Theme);
            InteractionPanel.SlotIcon(reserve, null, _dropTexture, "H₂O", Styles);
            InteractionPanel.SlotCaption(reserve, "Pod Reserve",
                $"{_waterSupply.CurrentMilliliters / 1000f:0.0} / " +
                $"{_waterSupply.CapacityMilliliters / 1000f:0.0} L", Styles);
            InteractionPanel.SlotMeter(reserve, _waterSupply.CurrentMilliliters,
                _waterSupply.CapacityMilliliters, 0f, ReserveFill, Theme);

            DrawTransferLine(columns.Process, canFill);

            Rect bottle = InteractionPanel.StackedSlot(columns.Outputs, 0, 1, slotSize, true, HeadlineInset);
            if (InteractionPanel.SlotButton(bottle, false, Theme) && canFill)
            {
                FillBottle();
            }

            InteractionPanel.SlotIcon(bottle, null, _bottleTexture, "BTL", Styles);
            InteractionPanel.SlotCaption(bottle, "Personal Bottle",
                $"{_bottle.CurrentMilliliters} / {_bottle.CapacityMilliliters} mL", Styles);
            InteractionPanel.SlotMeter(bottle, _bottle.CurrentMilliliters, _bottle.CapacityMilliliters, 0f,
                BottleFill, Theme);
            if (InteractionPanel.SlotAction(bottle, InteractionPanel.SlotCaptionHeight + 12f,
                    "FILL BOTTLE", canFill))
            {
                FillBottle();
            }

            InteractionPanel.Headline(columns.Outputs,
                $"💧 {_bottle.RemainingCapacityMilliliters} mL FREE", Styles);
            InteractionPanel.DrawFooter(panel, _feedback,
                "The reserve only refills from ice melted elsewhere in the pod.", Styles);
            if (close)
            {
                Close();
            }
        }

        /// <summary>The middle column: the pipe, animated only while a transfer is actually possible.</summary>
        private void DrawTransferLine(Rect column, bool flowing)
        {
            var chamberRect = new Rect(column.x + 22f, column.y + 30f, column.width - 44f, column.height - 60f);
            Rect inner = InteractionPanel.Chamber(chamberRect, "TRANSFER LINE", Styles, Theme);

            float pipeY = inner.center.y;
            InteractionPanel.Fill(new Rect(inner.x + 14f, pipeY - 3f, inner.width - 28f, 6f),
                flowing ? ReserveFill : Theme.Track);
            for (int i = 0; i < 3; i++)
            {
                GUI.Label(new Rect(inner.x + 24f + i * (inner.width - 60f) / 3f, pipeY - 18f, 28f, 32f), "▶",
                    Styles.Title);
            }

            if (flowing && _dropTexture != null)
            {
                float progress = Mathf.Repeat(Time.unscaledTime * .6f, 1f);
                float x = Mathf.Lerp(inner.x + 14f, inner.xMax - 40f, progress);
                GUI.DrawTexture(new Rect(x, pipeY - 30f, 26f, 26f), _dropTexture, ScaleMode.ScaleToFit, true);
            }

            InteractionPanel.ChamberStatus(chamberRect, flowing,
                flowing ? "READY TO POUR" : _waterSupply.CurrentMilliliters == 0 ? "RESERVE EMPTY" : "BOTTLE FULL",
                _bottle.CapacityMilliliters > 0
                    ? (float)_bottle.CurrentMilliliters / _bottle.CapacityMilliliters
                    : 0f,
                Styles, Theme);
        }

        private string StateText()
        {
            if (_waterSupply.CurrentMilliliters == 0)
            {
                return "Empty: the landing pod reserve has nothing left to pour.";
            }

            return _bottle.RemainingCapacityMilliliters == 0
                ? "Bottle full: nothing more will fit."
                : $"Ready · {_waterSupply.CurrentMilliliters / 1000f:0.0} L in the reserve.";
        }
    }
}
