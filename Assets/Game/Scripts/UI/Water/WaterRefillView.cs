using PlanetSurvival.Water.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using UnityEngine;

namespace PlanetSurvival.UI.Water
{
    [DisallowMultipleComponent]
    public sealed class WaterRefillView : MonoBehaviour
    {
        private const string BottleTextureResource = "Water/WaterBottle";
        private const string DropTextureResource = "Water/WaterDrop";
        private const float PanelWidth = 500f;
        private const float PanelHeight = 320f;

        private LiquidContainer _waterSupply;
        private LiquidContainer _bottle;
        private Texture2D _bottleTexture;
        private Texture2D _dropTexture;
        private string _status = "Select FILL BOTTLE to transfer water.";
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private GUIStyle _headerStyle;
        private GUIStyle _primaryLabelStyle;
        private GUIStyle _secondaryLabelStyle;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _bottleTexture = Resources.Load<Texture2D>(BottleTextureResource);
            _dropTexture = Resources.Load<Texture2D>(DropTextureResource);
        }

        private void OnDestroy()
        {
            ReleasePlayerControls();
        }

        public void Open(LiquidContainer waterSupply, LiquidContainer bottle,
            PlanarPlayerMotor playerMotor = null, PlayerInteractor playerInteractor = null)
        {
            if (waterSupply == null || bottle == null)
            {
                Debug.LogError($"{nameof(WaterRefillView)} cannot open without a water supply and bottle.", this);
                return;
            }

            _waterSupply = waterSupply;
            _bottle = bottle;
            CaptureAndLockPlayerControls(playerMotor, playerInteractor);
            _status = bottle.RemainingCapacityMilliliters == 0
                ? "Bottle is already full."
                : "Select FILL BOTTLE to transfer water.";
            IsOpen = true;
        }

        public int FillBottle()
        {
            if (_waterSupply == null || _bottle == null)
            {
                return 0;
            }

            int transferred = _bottle.FillFrom(_waterSupply);
            _status = transferred > 0
                ? $"Transferred {transferred} mL. Bottle sealed."
                : _bottle.RemainingCapacityMilliliters == 0
                    ? "Bottle is already full."
                    : "Landing pod water reserve is empty.";
            return transferred;
        }

        public void Close()
        {
            IsOpen = false;
            ReleasePlayerControls();
        }

        private void OnGUI()
        {
            if (!IsOpen || _waterSupply == null || _bottle == null)
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
            PanelBackground.Draw(panel, new Color(.035f, .06f, .075f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, panel.width - 40f, panel.height - 32f));
            GUILayout.BeginHorizontal();
            GUILayout.Label("WATER DISPENSER", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(72f), GUILayout.Height(26f)))
            {
                Close();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            DrawIcon(_bottleTexture, 116f);
            GUILayout.Space(18f);
            GUILayout.BeginVertical();
            GUILayout.Label("PERSONAL BOTTLE · 500 mL", _primaryLabelStyle);
            DrawMeter(_bottle.CurrentMilliliters, _bottle.CapacityMilliliters, new Color(.28f, .72f, .95f));
            GUILayout.Label($"{_bottle.CurrentMilliliters} / {_bottle.CapacityMilliliters} mL  ·  " +
                            $"{_bottle.RemainingCapacityMilliliters} mL free", _secondaryLabelStyle);
            GUILayout.Space(18f);
            GUILayout.Label("LANDING POD RESERVE", _primaryLabelStyle);
            DrawMeter(_waterSupply.CurrentMilliliters, _waterSupply.CapacityMilliliters, new Color(.23f, .55f, .78f));
            GUILayout.Label($"{_waterSupply.CurrentMilliliters / 1000f:0.0} L remaining", _secondaryLabelStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.Label(_status, _secondaryLabelStyle);
            GUI.enabled = _bottle.RemainingCapacityMilliliters > 0 && _waterSupply.CurrentMilliliters > 0;
            if (GUILayout.Button("FILL BOTTLE", GUILayout.Height(40f)))
            {
                FillBottle();
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawIcon(Texture2D texture, float size)
        {
            Rect area = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (texture != null)
            {
                GUI.DrawTexture(area, texture, ScaleMode.ScaleToFit, true);
            }

            if (_dropTexture != null)
            {
                GUI.DrawTexture(new Rect(area.xMax - 34f, area.yMax - 34f, 40f, 40f), _dropTexture,
                    ScaleMode.ScaleToFit, true);
            }
        }

        private static void DrawMeter(int current, int capacity, Color fillColor)
        {
            Rect meter = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(meter, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(.08f, .11f, .13f), Vector4.zero, new Vector4(4f, 4f, 4f, 4f));
            float normalized = capacity <= 0 ? 0f : Mathf.Clamp01((float)current / capacity);
            if (normalized > 0f)
            {
                var fill = new Rect(meter.x + 2f, meter.y + 2f, (meter.width - 4f) * normalized, meter.height - 4f);
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
            _headerStyle.normal.textColor = new Color(.76f, .91f, 1f);
            _primaryLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _primaryLabelStyle.normal.textColor = Color.white;
            _secondaryLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _secondaryLabelStyle.normal.textColor = new Color(.72f, .8f, .84f);
        }
    }
}
