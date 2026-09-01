using System;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class SurvivalHudView : MonoBehaviour
    {
        private const float ScreenMargin = 14f;
        private const float PanelPadding = 12f;
        private const float GaugeSize = 74f;
        private const float ColumnSpacing = 10f;
        private const float ReadoutHeight = 32f;
        private const float HeaderHeight = 20f;
        private const int GaugesPerRow = 4;
        private const float DangerThreshold = 0.25f;
        private const float WarningThreshold = 0.5f;

        private static readonly Color TrackColor = new(1f, 1f, 1f, 0.12f);
        private static readonly Color DangerColor = new(1f, 0.31f, 0.28f);
        private static readonly Color MutedTextColor = new(0.72f, 0.78f, 0.84f);

        private readonly VitalDisplay[] _vitals = new VitalDisplay[4];
        private readonly RingTexture _track = new();
        private PlayerSurvival _survival;
        private GameClock _clock;
        private string _timeText = string.Empty;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _percentStyle;

        public void Bind(PlayerSurvival survival, GameClock clock)
        {
            Unsubscribe();
            _survival = survival;
            _clock = clock;

            if (_survival == null || _clock == null)
            {
                Debug.LogError($"{nameof(SurvivalHudView)} requires both survival stats and a game clock.", this);
                enabled = false;
                return;
            }

            BindVital(0, "Health", new Color(1f, 0.36f, 0.42f), _survival.Stats.Health);
            BindVital(1, "Sanity", new Color(0.62f, 0.55f, 1f), _survival.Stats.Sanity);
            BindVital(2, "Hunger", new Color(1f, 0.71f, 0.32f), _survival.Stats.Hunger);
            BindVital(3, "Thirst", new Color(0.36f, 0.79f, 1f), _survival.Stats.Thirst);
            _clock.TimeChanged += RefreshTime;
            RefreshTime();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            if (_survival == null)
            {
                return;
            }

            EnsureStyles();

            int columns = Mathf.Min(GaugesPerRow, _vitals.Length);
            int rows = Mathf.CeilToInt(_vitals.Length / (float)columns);
            float columnWidth = GaugeSize + ColumnSpacing;
            float panelWidth = columns * columnWidth - ColumnSpacing + PanelPadding * 2f;
            float rowHeight = GaugeSize + ReadoutHeight;
            float panelHeight = HeaderHeight + rows * rowHeight + (rows - 1) * ColumnSpacing + PanelPadding * 2f;
            var panel = new Rect(Screen.width - panelWidth - ScreenMargin, ScreenMargin, panelWidth, panelHeight);

            PanelBackground.Draw(panel);
            var header = new Rect(panel.x + PanelPadding, panel.y + PanelPadding, panelWidth - PanelPadding * 2f, HeaderHeight);
            GUI.Label(header, _timeText, _headerStyle);

            float contentX = panel.x + PanelPadding;
            float contentY = header.yMax;

            for (int i = 0; i < _vitals.Length; i++)
            {
                int row = i / columns;
                int column = i % columns;
                var cell = new Rect(
                    contentX + column * columnWidth,
                    contentY + row * (rowHeight + ColumnSpacing),
                    GaugeSize,
                    rowHeight);
                DrawVital(_vitals[i], cell);
            }
        }

        private void BindVital(int index, string label, Color color, Vital vital)
        {
            var display = new VitalDisplay(label, color, vital);
            _vitals[index] = display;
            vital.Changed += display.Refresh;
            display.Refresh(vital.Current, vital.EffectiveMaximum);
        }

        private void RefreshTime()
        {
            string rescue = _clock.RescueAvailable ? "Rescue ready" : $"Rescue {_clock.DaysUntilRescue}d";
            _timeText = $"D{_clock.CurrentDay}  {_clock.Hour:00}:{_clock.Minute:00}   |   {rescue}";
        }

        private void DrawVital(VitalDisplay vital, Rect cell)
        {
            var gauge = new Rect(cell.x, cell.y, GaugeSize, GaugeSize);
            float normalized = vital.Normalized;
            bool danger = normalized <= DangerThreshold;
            Color fillColor = danger
                ? Color.Lerp(vital.Color, DangerColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f))
                : normalized <= WarningThreshold
                    ? Color.Lerp(vital.Color, DangerColor, 0.3f)
                    : vital.Color;

            GUI.DrawTexture(gauge, _track.Get(1f), ScaleMode.StretchToFill, true, 0f, TrackColor, Vector4.zero, Vector4.zero);
            GUI.DrawTexture(gauge, vital.Ring.Get(normalized), ScaleMode.StretchToFill, true, 0f, fillColor, Vector4.zero, Vector4.zero);

            _labelStyle.normal.textColor = fillColor;
            GUI.Label(new Rect(gauge.x, gauge.center.y - 9f, GaugeSize, 18f), vital.Label, _labelStyle);

            var valueRect = new Rect(cell.x, gauge.yMax + 3f, GaugeSize, 15f);
            GUI.Label(valueRect, $"{vital.Current:0} / {vital.Maximum:0}", _valueStyle);

            _percentStyle.normal.textColor = danger ? DangerColor : MutedTextColor;
            GUI.Label(new Rect(valueRect.x, valueRect.yMax, GaugeSize, 14f), $"{normalized * 100f:0}%", _percentStyle);
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _headerStyle.normal.textColor = MutedTextColor;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12
            };
            _valueStyle.normal.textColor = Color.white;

            _percentStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
        }

        private void Unsubscribe()
        {
            for (int i = 0; i < _vitals.Length; i++)
            {
                if (_vitals[i] != null)
                {
                    _vitals[i].Vital.Changed -= _vitals[i].Refresh;
                    _vitals[i].Dispose();
                    _vitals[i] = null;
                }
            }

            _track.Dispose();

            if (_clock != null)
            {
                _clock.TimeChanged -= RefreshTime;
            }
        }

        private sealed class VitalDisplay : IDisposable
        {
            public VitalDisplay(string label, Color color, Vital vital)
            {
                Label = label;
                Color = color;
                Vital = vital;
            }

            public string Label { get; }
            public Color Color { get; }
            public Vital Vital { get; }
            public RingTexture Ring { get; } = new();
            public float Current { get; private set; }
            public float Maximum { get; private set; }
            public float Normalized => Maximum <= 0f ? 0f : Mathf.Clamp01(Current / Maximum);

            public void Refresh(float current, float maximum)
            {
                Current = current;
                Maximum = maximum;
            }

            public void Dispose()
            {
                Ring.Dispose();
            }
        }
    }
}
