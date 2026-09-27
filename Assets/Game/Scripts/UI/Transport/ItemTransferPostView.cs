using System.Collections.Generic;
using PlanetSurvival.Transport.Domain;
using PlanetSurvival.Transport.Runtime;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.UI.Transport
{
    /// <summary>Configuration and live-status panel opened by right-clicking a completed transfer post.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemTransferPostView : InteractionPanelView
    {
        private const float PanelWidth = 940f;
        private const float PanelHeight = 600f;
        private const float ModuleHeight = 330f;

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
        private Vector2 _inputScroll;
        private Vector2 _outputScroll;
        private bool _inputDropdownOpen;
        private bool _outputDropdownOpen;
        private string _feedback = string.Empty;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Logistics;

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
            BeginSession(player);
        }

        protected override void OnClosed()
        {
            _station = null;
            _system = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _station == null || _station.Post == null || _system == null)
            {
                return;
            }

            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            bool close = InteractionPanel.DrawHeader(panel, _station.Site?.Definition.MenuIcon,
                $"TRANSFER POST {_station.Post.PostNumber:00}", StateText(), Styles);

            GUILayout.BeginArea(InteractionPanel.ContentArea(panel));
            DrawGroupControls();
            GUILayout.Space(12f);

            GUILayout.BeginHorizontal();
            DrawEndpointModule("INPUT", true, ref _inputScroll, ref _inputDropdownOpen);
            GUILayout.Space(12f);
            DrawCurrentModule();
            GUILayout.Space(12f);
            DrawEndpointModule("OUTPUT", false, ref _outputScroll, ref _outputDropdownOpen);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            InteractionPanel.DrawFooter(panel, _feedback,
                "Posts sharing a group number and colour move items between themselves.", Styles);
            if (close)
            {
                Close();
            }
        }

        private string StateText()
        {
            ItemTransferPost post = _station.Post;
            if (string.IsNullOrEmpty(post.InputEndpointId)) return "Idle: no input source selected.";
            if (string.IsNullOrEmpty(post.OutputEndpointId)) return "Idle: no output destination selected.";
            ItemDefinition item = _system.GetRouteItem(_station);
            if (item == null)
            {
                return "Standby: the route is valid but the source has nothing to send.";
            }

            return _system.IsRouteFlowing(_station)
                ? $"Running · {item.DisplayName} at {ItemTransferPost.ItemsPerSecond:0.#} items/s."
                : $"Standby: {item.DisplayName} is waiting on the destination.";
        }

        private void DrawGroupControls()
        {
            ItemTransferPost post = _station.Post;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"POST {post.PostNumber:00}  ·  GROUP {post.GroupNumber:00}", Styles.Section,
                GUILayout.Width(155f));
            if (GUILayout.Button("−", GUILayout.Width(34f), GUILayout.Height(30f)))
            {
                _system.ApplyGroupToConnectedPosts(post, Mathf.Max(1, post.GroupNumber - 1), post.GroupColor);
            }
            GUILayout.Label(post.GroupNumber.ToString("00"), Styles.Value, GUILayout.Width(54f), GUILayout.Height(30f));
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

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(270f), GUILayout.Height(ModuleHeight));
            GUILayout.Label(title, Styles.Section);
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
                GUILayout.Label(input ? "SOURCE" : "DESTINATION", Styles.Detail);
                GUILayout.Label(selectedLabel, Styles.Value);
                GUILayout.FlexibleSpace();
                GUILayout.Label(input ? _system.GetInputStatus(_station) : _system.GetOutputStatus(_station),
                    Styles.Detail, GUILayout.Height(70f));
            }
            GUILayout.EndVertical();
        }

        private void DrawCurrentModule()
        {
            ItemTransferPost post = _station.Post;
            ItemDefinition item = _system.GetRouteItem(_station);
            bool flowing = _system.IsRouteFlowing(_station);

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(290f), GUILayout.Height(ModuleHeight));
            GUILayout.Label("CURRENT", Styles.Section);
            GUILayout.Label(flowing ? "ROUTE ONLINE" : "ROUTE STANDBY", Styles.Detail);

            Rect flowRect = GUILayoutUtility.GetRect(260f, 138f, GUILayout.ExpandWidth(true));
            DrawFlowAnimation(flowRect, item, flowing, post.GroupColor);

            if (flowing && item != null)
            {
                GUILayout.Label(item.DisplayName, Styles.Value);
                GUILayout.Label($"Buffer {post.BufferedQuantity}/{ItemTransferPost.BufferCapacity}  ·  " +
                                $"{ItemTransferPost.ItemsPerSecond:0.#} items/s", Styles.Detail);
            }
            else
            {
                GUILayout.Label(item == null ? "No transferable item detected" : $"Waiting: {item.DisplayName}",
                    Styles.Value);
                GUILayout.Label(RouteStandbyReason(post), Styles.Detail);
            }

            GUILayout.FlexibleSpace();
            string last = post.LastMovedItem == null
                ? "No dispatch yet"
                : $"Last: {post.LastMovedItem.DisplayName} ×{post.LastMovedQuantity}";
            GUILayout.Label($"Moved {post.TotalItemsMoved}  ·  {last}", Styles.Detail);
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
                GUI.Label(new Rect(rect.x + 58f + i * 68f, rect.center.y - 16f, 28f, 32f), "▶", Styles.Title);
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
                GUI.Label(itemRect, item.DisplayName.Substring(0, 1), Styles.Glyph);
            }
        }

        private string RouteStandbyReason(ItemTransferPost post)
        {
            if (string.IsNullOrEmpty(post.InputEndpointId)) return "Select an input source on the left.";
            if (string.IsNullOrEmpty(post.OutputEndpointId)) return "Select an output destination on the right.";
            if (_system.GetRouteItem(_station) == null) return "The route is valid but the source is currently empty.";
            return _system.GetOutputStatus(_station);
        }
    }
}
