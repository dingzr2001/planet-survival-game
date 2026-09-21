using System.Collections.Generic;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.Power.Runtime;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.UI;
using UnityEngine;

namespace PlanetSurvival.UI.Power
{
    /// <summary>Right-click configuration panel for directional power-pole inputs and ordered outputs.</summary>
    [DisallowMultipleComponent]
    public sealed class PowerPoleView : MonoBehaviour
    {
        private const float PanelWidth = 980f;
        private const float PanelHeight = 590f;
        private PowerPoleStation _station;
        private PowerPoleSystem _system;
        private PlanarPlayerMotor _motor;
        private PlayerInteractor _interactor;
        private InventoryView _inventory;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventory;
        private bool _inputPickerOpen;
        private bool _outputPickerOpen;
        private Vector2 _inputScroll;
        private Vector2 _outputScroll;
        private GUIStyle _title;
        private GUIStyle _section;
        private GUIStyle _detail;

        public bool IsOpen { get; private set; }

        public void Open(PowerPoleStation station, PowerPoleSystem system, GameObject player)
        {
            if (station?.Pole == null || system == null || player == null) return;
            Close();
            _station = station;
            _system = system;
            _motor = player.GetComponent<PlanarPlayerMotor>();
            _interactor = player.GetComponent<PlayerInteractor>();
            _inventory = GetComponent<InventoryView>();
            _restoreMotor = _motor != null && _motor.enabled;
            _restoreInteractor = _interactor != null && _interactor.enabled;
            _restoreInventory = _inventory != null && _inventory.enabled;
            if (_motor != null) _motor.enabled = false;
            if (_interactor != null) _interactor.enabled = false;
            if (_inventory != null) _inventory.enabled = false;
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            if (_motor != null) _motor.enabled = _restoreMotor;
            if (_interactor != null) _interactor.enabled = _restoreInteractor;
            if (_inventory != null) _inventory.enabled = _restoreInventory;
            _station = null;
            _system = null;
            _motor = null;
            _interactor = null;
            _inventory = null;
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void OnDisable() { if (IsOpen) Close(); }
        private void OnDestroy() { Close(); }

        private void OnGUI()
        {
            if (!IsOpen || _station?.Pole == null || _system == null) return;
            EnsureStyles();
            Rect panel = new((Screen.width - PanelWidth) * .5f, (Screen.height - PanelHeight) * .5f,
                PanelWidth, PanelHeight);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 20f, panel.width - 48f, panel.height - 40f));
            DrawHeader();
            GUILayout.Space(12f);
            GUILayout.BeginHorizontal();
            DrawInputs();
            GUILayout.Space(14f);
            DrawCurrent();
            GUILayout.Space(14f);
            DrawOutputs();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE  (ESC)", GUILayout.Height(34f))) Close();
            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            PowerPole pole = _station.Pole;
            GUILayout.BeginHorizontal();
            GUILayout.Label("POWER POLE", _title);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"INPUT  {pole.LastInputPower:0.##}    →    DELIVERED  {pole.LastDeliveredPower:0.##}", _detail);
            GUILayout.EndHorizontal();
            string state = pole.LastInputPower <= .0001f ? "NO INPUT" :
                pole.OutputEndpointIds.Count > 0 && pole.LastPoweredOutputs == pole.OutputEndpointIds.Count
                    ? "ALL OUTPUTS POWERED" : "OUTPUT LIMITED";
            GUILayout.Label(state + "  ·  Outputs run top to bottom; each must receive its full requested power.", _detail);
        }

        private void DrawInputs()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300f), GUILayout.Height(430f));
            GUILayout.Label("INPUTS", _section);
            GUILayout.Label("Power poles may be remote; generators must be adjacent.", _detail);
            DrawEndpointRows(_station.Pole.InputEndpointIds, true);
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
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300f), GUILayout.Height(430f));
            GUILayout.Label("OUTPUTS · PRIORITY ORDER", _section);
            GUILayout.Label("Poles may be remote; power consumers must be adjacent.", _detail);
            DrawEndpointRows(_station.Pole.OutputEndpointIds, false);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_outputPickerOpen ? "HIDE TARGETS" : "+ ADD OUTPUT", GUILayout.Height(34f)))
            {
                _outputPickerOpen = !_outputPickerOpen;
                _inputPickerOpen = false;
            }
            if (_outputPickerOpen) DrawPicker(_system.GetAvailableOutputs(_station), false);
            GUILayout.EndVertical();
        }

        private void DrawEndpointRows(IReadOnlyList<string> endpoints, bool input)
        {
            if (endpoints.Count == 0)
            {
                GUILayout.Label("No endpoint configured.", _detail);
                return;
            }
            Vector2 scroll = input ? _inputScroll : _outputScroll;
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(260f));
            for (int i = 0; i < endpoints.Count; i++)
            {
                string endpoint = endpoints[i];
                GUILayout.BeginHorizontal(GUI.skin.box);
                Rect iconRect = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f));
                SpriteIcon.Draw(iconRect, _system.GetEndpointIcon(endpoint));
                GUILayout.Label(input ? _system.GetEndpointLabel(endpoint) : $"{i + 1}. {_system.GetEndpointLabel(endpoint)}", _detail,
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
            foreach (PowerEndpointOption option in options)
            {
                GUILayout.BeginHorizontal();
                Rect iconRect = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f));
                SpriteIcon.Draw(iconRect, option.Icon);
                bool chosen = GUILayout.Button(option.Label, GUILayout.Height(28f));
                GUILayout.EndHorizontal();
                if (!chosen) continue;
                if (input) { _system.AddInput(_station, option.Id); _inputPickerOpen = false; }
                else { _system.AddOutput(_station, option.Id); _outputPickerOpen = false; }
                break;
            }
        }

        private void DrawCurrent()
        {
            PowerPole pole = _station.Pole;
            bool active = pole.LastDeliveredPower > .0001f;
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300f), GUILayout.Height(430f));
            GUILayout.Label("TRANSMISSION", _section);
            GUILayout.Label(active ? "ROUTE ONLINE" : "ROUTE STANDBY", _detail);
            Rect route = GUILayoutUtility.GetRect(260f, 180f, GUILayout.ExpandWidth(true));
            Color old = GUI.color;
            GUI.color = active ? new Color(1f, .55f, .1f) : new Color(.24f, .3f, .34f);
            GUI.DrawTexture(new Rect(route.x + 18f, route.center.y - 2f, route.width - 36f, 4f), Texture2D.whiteTexture);
            GUI.color = old;
            for (int i = 0; i < 3; i++)
                GUI.Label(new Rect(route.x + 54f + i * 65f, route.center.y - 18f, 28f, 32f), "▶", _title);
            if (active)
            {
                float progress = Mathf.Repeat(Time.realtimeSinceStartup * .7f, 1f);
                float x = Mathf.Lerp(route.x + 18f, route.xMax - 42f, progress);
                GUI.Label(new Rect(x, route.center.y - 23f, 32f, 42f), "ϟ", _title);
            }
            GUILayout.Label($"Input {pole.LastInputPower:0.##}  ·  Delivered {pole.LastDeliveredPower:0.##}", _detail);
            GUILayout.Label($"Full-power outputs: {pole.LastPoweredOutputs}/{pole.OutputEndpointIds.Count}", _detail);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Electrical routes are directional.", _detail);
            GUILayout.EndVertical();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, .72f, .25f);
            _section = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _section.normal.textColor = new Color(.5f, .84f, 1f);
            _detail = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _detail.normal.textColor = new Color(.84f, .89f, .94f);
        }
    }
}
