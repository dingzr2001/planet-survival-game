using PlanetSurvival.Core.Time;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class SurvivalHudView : MonoBehaviour
    {
        private const float PanelWidth = 200f;
        private const float PanelHeight = 165f;
        private const float BarHeight = 8f;
        private const float DangerThreshold = 0.25f;

        private readonly VitalDisplay[] _vitals = new VitalDisplay[4];
        private PlayerSurvival _survival;
        private GameClock _clock;
        private string _timeText = string.Empty;

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

            BindVital(0, "Health", _survival.Stats.Health);
            BindVital(1, "Sanity", _survival.Stats.Sanity);
            BindVital(2, "Hunger", _survival.Stats.Hunger);
            BindVital(3, "Thirst", _survival.Stats.Thirst);
            _clock.TimeChanged += RefreshTime;
            RefreshTime();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnGUI()
        {
            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            GUILayout.BeginArea(new Rect(8f, 8f, width, PanelHeight), GUI.skin.box);
            GUILayout.Label(_timeText, GUI.skin.box);

            for (int i = 0; i < _vitals.Length; i++)
            {
                DrawVital(_vitals[i]);
            }

            GUILayout.EndArea();
        }

        private void BindVital(int index, string label, Vital vital)
        {
            var display = new VitalDisplay(label, vital);
            _vitals[index] = display;
            vital.Changed += display.Refresh;
            display.Refresh(vital.Current, vital.EffectiveMaximum);
        }

        private void RefreshTime()
        {
            string rescue = _clock.RescueAvailable ? "Rescue ready" : $"Rescue {_clock.DaysUntilRescue}d";
            _timeText = $"D{_clock.CurrentDay} {_clock.Hour:00}:{_clock.Minute:00}  |  {rescue}";
        }

        private static void DrawVital(VitalDisplay vital)
        {
            Color previousColor = GUI.color;
            GUI.color = vital.Normalized <= DangerThreshold ? new Color(1f, 0.35f, 0.25f) : Color.white;
            GUILayout.Label($"{vital.Label}: {vital.Current:0}/{vital.Maximum:0}");
            Rect bar = GUILayoutUtility.GetRect(1f, BarHeight, GUILayout.ExpandWidth(true));
            GUI.Box(bar, GUIContent.none);
            var fill = new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * vital.Normalized, bar.height - 4f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void Unsubscribe()
        {
            for (int i = 0; i < _vitals.Length; i++)
            {
                if (_vitals[i] != null)
                {
                    _vitals[i].Vital.Changed -= _vitals[i].Refresh;
                    _vitals[i] = null;
                }
            }

            if (_clock != null)
            {
                _clock.TimeChanged -= RefreshTime;
            }
        }

        private sealed class VitalDisplay
        {
            public VitalDisplay(string label, Vital vital)
            {
                Label = label;
                Vital = vital;
            }

            public string Label { get; }
            public Vital Vital { get; }
            public float Current { get; private set; }
            public float Maximum { get; private set; }
            public float Normalized => Maximum <= 0f ? 0f : Mathf.Clamp01(Current / Maximum);

            public void Refresh(float current, float maximum)
            {
                Current = current;
                Maximum = maximum;
            }
        }
    }
}
