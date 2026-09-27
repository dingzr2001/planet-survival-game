using System.Collections.Generic;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.Power.Runtime;
using UnityEngine;

namespace PlanetSurvival.UI.Power
{
    /// <summary>Right-click configuration panel for directional power-pole inputs and ordered outputs.</summary>
    [DisallowMultipleComponent]
    public sealed class PowerPoleView : InteractionPanelView
    {
        private const float PanelWidth = 980f;
        private const float PanelHeight = 640f;
        private const float ModuleHeight = 400f;
        private const float EndpointTileSize = 56f;
        private const float EndpointTileGap = 6f;
        private const int EndpointTileColumns = 4;

        private PowerPoleStation _station;
        private PowerPoleSystem _system;
        private bool _inputPickerOpen;
        private bool _outputPickerOpen;
        private Vector2 _inputScroll;
        private Vector2 _outputScroll;
        private Vector2 _inputPickerScroll;
        private Vector2 _outputPickerScroll;
        private GUIStyle _endpointNumber;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Logistics;

        public void Open(PowerPoleStation station, PowerPoleSystem system, GameObject player)
        {
            if (station?.Pole == null || system == null || player == null)
            {
                Debug.LogError($"{nameof(PowerPoleView)} needs a pole, the power system and the player.", this);
                return;
            }

            Close();
            _station = station;
            _system = system;
            _inputPickerOpen = false;
            _outputPickerOpen = false;
            BeginSession(player);
        }

        protected override void OnClosed()
        {
            _station = null;
            _system = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _station?.Pole == null || _system == null) return;
            EnsureStyles();
            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            bool close = InteractionPanel.DrawHeader(panel, _station.Site?.Definition.MenuIcon,
                $"POWER POLE {_station.Pole.PoleNumber:00}", StateText(), Styles);

            GUILayout.BeginArea(InteractionPanel.ContentArea(panel));
            GUILayout.BeginHorizontal();
            DrawInputs();
            GUILayout.Space(14f);
            DrawCurrent();
            GUILayout.Space(14f);
            DrawOutputs();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            PowerPole pole = _station.Pole;
            InteractionPanel.DrawFooter(panel,
                $"INPUT {pole.LastInputPower:0.##} W  →  DELIVERED {pole.LastDeliveredPower:0.##} W  ·  " +
                $"full-power outputs {pole.LastPoweredOutputs}/{pole.OutputEndpointIds.Count}",
                "Outputs run top to bottom; each must receive its full requested power.", Styles);
            if (close)
            {
                Close();
            }
        }

        private string StateText()
        {
            PowerPole pole = _station.Pole;
            if (pole.LastInputPower <= .0001f) return "No input: add a generator or another pole on the left.";
            if (pole.OutputEndpointIds.Count == 0) return "Input available: add a consumer on the right.";
            return pole.LastPoweredOutputs == pole.OutputEndpointIds.Count
                ? "All outputs powered."
                : $"Output limited: {pole.LastPoweredOutputs} of {pole.OutputEndpointIds.Count} outputs run.";
        }

        private void DrawInputs()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300f), GUILayout.Height(ModuleHeight));
            GUILayout.Label("INPUTS", Styles.Section);
            GUILayout.Label("Power poles may be remote; generators must be adjacent.", Styles.Detail);
            DrawEndpointRows(_station.Pole.InputEndpointIds, true, _inputPickerOpen ? 120f : 260f);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_inputPickerOpen ? "HIDE SOURCES" : "+ ADD INPUT", GUILayout.Height(34f)))
            {
                _inputPickerOpen = !_inputPickerOpen;
                _outputPickerOpen = false;
            }
            if (_inputPickerOpen) DrawPicker(_system.GetAvailableInputs(_station), true);
            GUILayout.EndVertical();
        }

        private void DrawOutputs()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300f), GUILayout.Height(ModuleHeight));
            GUILayout.Label("OUTPUTS · PRIORITY ORDER", Styles.Section);
            GUILayout.Label("Poles may be remote; power consumers must be adjacent.", Styles.Detail);
            DrawEndpointRows(_station.Pole.OutputEndpointIds, false, _outputPickerOpen ? 120f : 260f);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_outputPickerOpen ? "HIDE TARGETS" : "+ ADD OUTPUT", GUILayout.Height(34f)))
            {
                _outputPickerOpen = !_outputPickerOpen;
                _inputPickerOpen = false;
            }
            if (_outputPickerOpen) DrawPicker(_system.GetAvailableOutputs(_station), false);
            GUILayout.EndVertical();
        }

        private void DrawEndpointRows(IReadOnlyList<string> endpoints, bool input, float height)
        {
            if (endpoints.Count == 0)
            {
                GUILayout.Label("No endpoint configured.", Styles.Detail);
                return;
            }
            Vector2 scroll = input ? _inputScroll : _outputScroll;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            for (int i = 0; i < endpoints.Count; i++)
            {
                string endpoint = endpoints[i];
                GUILayout.BeginHorizontal(GUI.skin.box);
                Rect iconRect = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f));
                SpriteIcon.Draw(iconRect, _system.GetEndpointIcon(endpoint));
                GUILayout.Label(input ? _system.GetEndpointLabel(endpoint) : $"{i + 1}. {_system.GetEndpointLabel(endpoint)}", Styles.Detail,
                    GUILayout.Width(155f));
                if (!input && GUILayout.Button("▲", GUILayout.Width(32f))) _system.MoveOutputEarlier(_station, endpoint);
                if (!input && GUILayout.Button("▼", GUILayout.Width(32f))) _system.MoveOutputLater(_station, endpoint);
                if (GUILayout.Button("×", GUILayout.Width(32f)))
                {
                    if (input) _system.RemoveInput(_station, endpoint); else _system.RemoveOutput(_station, endpoint);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (input) _inputScroll = scroll; else _outputScroll = scroll;
        }

        private void DrawPicker(IReadOnlyList<PowerEndpointOption> options, bool input)
        {
            GUILayout.Space(6f);
            if (options.Count == 0)
            {
                GUILayout.Label("No available endpoint.", Styles.Detail);
                return;
            }

            Vector2 pickerScroll = input ? _inputPickerScroll : _outputPickerScroll;
            pickerScroll = GUILayout.BeginScrollView(pickerScroll, GUILayout.Height(150f));
            for (int i = 0; i < options.Count; i++)
            {
                if (i % EndpointTileColumns == 0) GUILayout.BeginHorizontal();

                PowerEndpointOption option = options[i];
                Rect tileRect = GUILayoutUtility.GetRect(EndpointTileSize, EndpointTileSize,
                    GUILayout.Width(EndpointTileSize), GUILayout.Height(EndpointTileSize));
                bool chosen = GUI.Button(tileRect, GUIContent.none);
                Rect iconRect = new(tileRect.x + 6f, tileRect.y + 4f, tileRect.width - 12f, tileRect.height - 14f);
                SpriteIcon.Draw(iconRect, option.Icon);
                GUI.Label(new Rect(tileRect.x + 3f, tileRect.yMax - 18f, tileRect.width - 6f, 15f),
                    option.Label, _endpointNumber);
                GUILayout.Space(EndpointTileGap);

                if (i % EndpointTileColumns == EndpointTileColumns - 1 || i == options.Count - 1)
                    GUILayout.EndHorizontal();

                if (chosen)
                {
                    if (input) { _system.AddInput(_station, option.Id); _inputPickerOpen = false; }
                    else { _system.AddOutput(_station, option.Id); _outputPickerOpen = false; }
                }
            }
            GUILayout.EndScrollView();
            if (input) _inputPickerScroll = pickerScroll; else _outputPickerScroll = pickerScroll;
        }

        private void DrawCurrent()
        {
            PowerPole pole = _station.Pole;
            bool active = pole.LastDeliveredPower > .0001f;
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300f), GUILayout.Height(ModuleHeight));
            GUILayout.Label("TRANSMISSION", Styles.Section);
            GUILayout.Label(active ? "ROUTE ONLINE" : "ROUTE STANDBY", Styles.Detail);
            Rect route = GUILayoutUtility.GetRect(260f, 180f, GUILayout.ExpandWidth(true));
            Color old = GUI.color;
            GUI.color = active ? new Color(1f, .55f, .1f) : new Color(.24f, .3f, .34f);
            GUI.DrawTexture(new Rect(route.x + 18f, route.center.y - 2f, route.width - 36f, 4f), Texture2D.whiteTexture);
            GUI.color = old;
            for (int i = 0; i < 3; i++)
                GUI.Label(new Rect(route.x + 54f + i * 65f, route.center.y - 18f, 28f, 32f), "▶", Styles.Title);
            if (active)
            {
                float progress = Mathf.Repeat(Time.realtimeSinceStartup * .7f, 1f);
                float x = Mathf.Lerp(route.x + 18f, route.xMax - 42f, progress);
                GUI.Label(new Rect(x, route.center.y - 23f, 32f, 42f), "ϟ", Styles.Title);
            }
            GUILayout.Label($"Input {pole.LastInputPower:0.##}  ·  Delivered {pole.LastDeliveredPower:0.##}", Styles.Detail);
            GUILayout.Label($"Full-power outputs: {pole.LastPoweredOutputs}/{pole.OutputEndpointIds.Count}", Styles.Detail);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Electrical routes are directional.", Styles.Detail);
            GUILayout.EndVertical();
        }

        private void EnsureStyles()
        {
            if (_endpointNumber != null) return;
            _endpointNumber = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight
            };
            _endpointNumber.normal.textColor = new Color(.84f, .89f, .94f);
        }
    }
}
