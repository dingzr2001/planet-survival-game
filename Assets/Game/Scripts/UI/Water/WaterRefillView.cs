using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.UI.Water
{
    /// <summary>Operations panel for the landing pod dispenser: read both tanks, then fill the bottle.</summary>
    [DisallowMultipleComponent]
    public sealed class WaterRefillView : InteractionPanelView
    {
        private const string BottleTextureResource = "Water/WaterBottle";
        private const string DropTextureResource = "Water/WaterDrop";
        private const float PanelWidth = 560f;
        private const float PanelHeight = 420f;
        private const float BottleIconSize = 116f;

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

            GUILayout.BeginArea(InteractionPanel.ContentArea(panel));
            GUILayout.BeginHorizontal();
            DrawBottleIcon();
            GUILayout.Space(18f);
            GUILayout.BeginVertical();
            GUILayout.Label($"PERSONAL BOTTLE · {_bottle.CapacityMilliliters} mL", Styles.Section);
            InteractionPanel.LayoutMeter("FILL", _bottle.CurrentMilliliters / 1000f,
                _bottle.CapacityMilliliters / 1000f, "L", BottleFill, Styles);
            GUILayout.Label($"{_bottle.RemainingCapacityMilliliters} mL free", Styles.Detail);
            GUILayout.Space(18f);
            GUILayout.Label("LANDING POD RESERVE", Styles.Section);
            InteractionPanel.LayoutMeter("STORED", _waterSupply.CurrentMilliliters / 1000f,
                _waterSupply.CapacityMilliliters / 1000f, "L", ReserveFill, Styles);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUI.enabled = _bottle.RemainingCapacityMilliliters > 0 && _waterSupply.CurrentMilliliters > 0;
            if (GUILayout.Button("FILL BOTTLE", GUILayout.Height(38f)))
            {
                FillBottle();
            }
            GUI.enabled = true;
            GUILayout.EndArea();

            InteractionPanel.DrawFooter(panel, _feedback,
                "The reserve only refills from ice melted elsewhere in the pod.", Styles);
            if (close)
            {
                Close();
            }
        }

        private void DrawBottleIcon()
        {
            Rect area = GUILayoutUtility.GetRect(BottleIconSize, BottleIconSize,
                GUILayout.Width(BottleIconSize), GUILayout.Height(BottleIconSize));
            if (_bottleTexture != null)
            {
                GUI.DrawTexture(area, _bottleTexture, ScaleMode.ScaleToFit, true);
            }

            if (_dropTexture != null)
            {
                GUI.DrawTexture(new Rect(area.xMax - 34f, area.yMax - 34f, 40f, 40f), _dropTexture,
                    ScaleMode.ScaleToFit, true);
            }
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
