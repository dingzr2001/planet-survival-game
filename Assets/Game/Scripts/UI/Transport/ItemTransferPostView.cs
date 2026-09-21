using System.Collections.Generic;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Transport.Domain;
using PlanetSurvival.Transport.Runtime;
using PlanetSurvival.UI.Inventory;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.UI.Transport
{
    /// <summary>Configuration and live-status panel opened by right-clicking a completed transfer post.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemTransferPostView : MonoBehaviour
    {
        private const float PanelWidth = 920f;
        private const float PanelHeight = 520f;

        private static readonly Color[] Palette =
        {
            new(.18f, .75f, 1f),
            new(1f, .42f, .22f),
            new(.38f, .9f, .38f),
            new(.9f, .3f, .82f),
            new(1f, .82f, .2f),
            new(.55f, .4f, 1f)
        };

        private ItemTransferPostStation _station;
        private ItemTransferSystem _system;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private InventoryView _inventoryView;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;
        private Vector2 _inputScroll;
        private Vector2 _outputScroll;
        private bool _inputDropdownOpen;
        private bool _outputDropdownOpen;
        private string _feedback = string.Empty;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _detailStyle;

        public bool IsOpen { get; private set; }

        public void Open(ItemTransferPostStation station, ItemTransferSystem system, GameObject player)
        {
            if (station?.Post == null || system == null || player == null)
            {
                Debug.LogError($"{nameof(ItemTransferPostView)} needs a post, transfer system, and player.", this);
                return;
            }

            Close();
            _station = station;
            _system = system;
            _inputDropdownOpen = false;
            _outputDropdownOpen = false;
            _feedback = "Choose an input on the left and an output on the right.";
            CaptureAndLockControls(player);
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            ReleaseControls();
            _station = null;
            _system = null;
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        private void OnDestroy()
        {
            ReleaseControls();
        }

        private void OnGUI()
        {
            if (!IsOpen || _station == null || _station.Post == null || _system == null)
            {
                return;
            }

            EnsureStyles();
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .7f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.035f, .05f, .065f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"TRANSFER POST {_station.Post.PostNumber:00}", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(76f), GUILayout.Height(26f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();

            DrawGroupControls();
            GUILayout.Space(14f);

            GUILayout.BeginHorizontal();
            DrawEndpointModule("INPUT", true, ref _inputScroll, ref _inputDropdownOpen);
            GUILayout.Space(12f);
            DrawCurrentModule();
            GUILayout.Space(12f);
            DrawEndpointModule("OUTPUT", false, ref _outputScroll, ref _outputDropdownOpen);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.Label(_feedback, _detailStyle);
            GUILayout.EndArea();
        }

        private void DrawGroupControls()
        {
            ItemTransferPost post = _station.Post;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"POST {post.PostNumber:00}  ·  GROUP {post.GroupNumber:00}", _sectionStyle,
                GUILayout.Width(155f));
            if (GUILayout.Button("−", GUILayout.Width(34f), GUILayout.Height(30f)))
            {
                _system.ApplyGroupToConnectedPosts(post, Mathf.Max(1, post.GroupNumber - 1), post.GroupColor);
            }
            GUILayout.Label(post.GroupNumber.ToString("00"), _headerStyle, GUILayout.Width(54f), GUILayout.Height(30f));
            if (GUILayout.Button("+", GUILayout.Width(34f), GUILayout.Height(30f)))
            {
                _system.ApplyGroupToConnectedPosts(post, post.GroupNumber + 1, post.GroupColor);
            }

            GUILayout.Space(16f);
            Color previousBackground = GUI.backgroundColor;
            for (int i = 0; i < Palette.Length; i++)
            {
                GUI.backgroundColor = Palette[i];
                if (GUILayout.Button(GUIContent.none, GUILayout.Width(38f), GUILayout.Height(30f)))
                {
                    _system.ApplyGroupToConnectedPosts(post, post.GroupNumber, Palette[i]);
                }
            }
            GUI.backgroundColor = previousBackground;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawEndpointModule(string title, bool input, ref Vector2 scroll, ref bool dropdownOpen)
        {
            IReadOnlyList<TransferEndpointOption> options = input
                ? _system.GetInputOptions(_station)
                : _system.GetOutputOptions(_station);
            string selectedId = input ? _station.Post.InputEndpointId : _station.Post.OutputEndpointId;

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(270f), GUILayout.Height(330f));
            GUILayout.Label(title, _sectionStyle);
            string selectedLabel = _system.GetEndpointLabel(selectedId);
            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = input ? new Color(.22f, .58f, .82f) : new Color(.18f, .72f, .48f);
            if (GUILayout.Button($"{selectedLabel}  {(dropdownOpen ? "▲" : "▼")}",
                    GUILayout.Height(38f)))
            {
                dropdownOpen = !dropdownOpen;
                if (input) _outputDropdownOpen = false;
                else _inputDropdownOpen = false;
            }
            GUI.backgroundColor = previousBackground;

            if (dropdownOpen)
            {
                scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(190f));
                for (int i = 0; i < options.Count; i++)
                {
                    TransferEndpointOption option = options[i];
                    bool selected = option.Id == selectedId;
                    if (GUILayout.Button(selected ? $"✓ {option.Label}" : option.Label, GUILayout.Height(32f)))
                    {
                        if (input) _system.SetInput(_station, option.Id);
                        else _system.SetOutput(_station, option.Id);
                        dropdownOpen = false;
                        _feedback = $"{title.ToLowerInvariant()} set to {option.Label}.";
                    }
                }
                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Space(18f);
                GUILayout.Label(input ? "SOURCE" : "DESTINATION", _detailStyle);
                GUILayout.Label(selectedLabel, _headerStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(input ? _system.GetInputStatus(_station) : _system.GetOutputStatus(_station),
                    _detailStyle, GUILayout.Height(70f));
            }
            GUILayout.EndVertical();
        }

        private void DrawCurrentModule()
        {
            ItemTransferPost post = _station.Post;
            ItemDefinition item = _system.GetRouteItem(_station);
            bool flowing = _system.IsRouteFlowing(_station);

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(290f), GUILayout.Height(330f));
            GUILayout.Label("CURRENT", _sectionStyle);
            GUILayout.Label(flowing ? "ROUTE ONLINE" : "ROUTE STANDBY", _detailStyle);

            Rect flowRect = GUILayoutUtility.GetRect(260f, 138f, GUILayout.ExpandWidth(true));
            DrawFlowAnimation(flowRect, item, flowing, post.GroupColor);

            if (flowing && item != null)
            {
                GUILayout.Label(item.DisplayName, _headerStyle);
                GUILayout.Label($"Buffer {post.BufferedQuantity}/{ItemTransferPost.BufferCapacity}  ·  " +
                                $"{ItemTransferPost.ItemsPerSecond:0.#} items/s", _detailStyle);
            }
            else
            {
                GUILayout.Label(item == null ? "No transferable item detected" : $"Waiting: {item.DisplayName}",
                    _headerStyle);
                GUILayout.Label(RouteStandbyReason(post), _detailStyle);
            }

            GUILayout.FlexibleSpace();
            string last = post.LastMovedItem == null
                ? "No dispatch yet"
                : $"Last: {post.LastMovedItem.DisplayName} ×{post.LastMovedQuantity}";
            GUILayout.Label($"Moved {post.TotalItemsMoved}  ·  {last}", _detailStyle);
            GUILayout.EndVertical();
        }

        private void DrawFlowAnimation(Rect rect, ItemDefinition item, bool flowing, Color groupColor)
        {
            Color oldColor = GUI.color;
            Color lineColor = flowing ? groupColor : new Color(.25f, .3f, .34f, 1f);
            GUI.color = lineColor;
            GUI.DrawTexture(new Rect(rect.x + 18f, rect.center.y - 2f, rect.width - 36f, 4f),
                Texture2D.whiteTexture);
            GUI.color = oldColor;

            for (int i = 0; i < 3; i++)
            {
                GUI.Label(new Rect(rect.x + 58f + i * 68f, rect.center.y - 16f, 28f, 32f), "▶", _headerStyle);
            }

            if (!flowing || item == null)
            {
                return;
            }

            float progress = Mathf.Repeat(Time.realtimeSinceStartup * .55f, 1f);
            float x = Mathf.Lerp(rect.x + 18f, rect.xMax - 54f, progress);
            var itemRect = new Rect(x, rect.center.y - 22f, 44f, 44f);
            GUI.color = new Color(.04f, .07f, .09f, .95f);
            GUI.DrawTexture(itemRect, Texture2D.whiteTexture);
            GUI.color = oldColor;
            if (item.Icon != null)
            {
                SpriteIcon.Draw(new Rect(itemRect.x + 4f, itemRect.y + 4f, 36f, 36f), item.Icon);
            }
            else
            {
                GUI.Label(itemRect, item.DisplayName.Substring(0, 1), _headerStyle);
            }
        }

        private string RouteStandbyReason(ItemTransferPost post)
        {
            if (string.IsNullOrEmpty(post.InputEndpointId)) return "Select an input source on the left.";
            if (string.IsNullOrEmpty(post.OutputEndpointId)) return "Select an output destination on the right.";
            if (_system.GetRouteItem(_station) == null) return "The route is valid but the source is currently empty.";
            return _system.GetOutputStatus(_station);
        }

        private void CaptureAndLockControls(GameObject player)
        {
            _playerMotor = player.GetComponent<PlanarPlayerMotor>();
            _playerInteractor = player.GetComponent<PlayerInteractor>();
            _inventoryView = GetComponent<InventoryView>();
            _restoreMotor = _playerMotor != null && _playerMotor.enabled;
            _restoreInteractor = _playerInteractor != null && _playerInteractor.enabled;
            _restoreInventoryView = _inventoryView != null && _inventoryView.enabled;
            if (_playerMotor != null) _playerMotor.enabled = false;
            if (_playerInteractor != null) _playerInteractor.enabled = false;
            if (_inventoryView != null) _inventoryView.enabled = false;
        }

        private void ReleaseControls()
        {
            if (_playerMotor != null) _playerMotor.enabled = _restoreMotor;
            if (_playerInteractor != null) _playerInteractor.enabled = _restoreInteractor;
            if (_inventoryView != null) _inventoryView.enabled = _restoreInventoryView;
            _playerMotor = null;
            _playerInteractor = null;
            _inventoryView = null;
            _restoreMotor = false;
            _restoreInteractor = false;
            _restoreInventoryView = false;
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = Color.white;
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _sectionStyle.normal.textColor = new Color(.45f, .82f, 1f);
            _detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _detailStyle.normal.textColor = new Color(.82f, .88f, .92f);
        }
    }
}
