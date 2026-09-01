using System.Collections.Generic;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using UnityEngine;

namespace PlanetSurvival.UI.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryView : MonoBehaviour
    {
        private const float QuickSlotSize = 64f;
        private const float SlotSpacing = 6f;
        private const float InventorySlotSize = 88f;
        private const float SelectionOutlineWidth = 2f;

        private PlayerInventory _playerInventory;
        private InventorySkin _skin;
        private bool _isPanelOpen;
        private string _selectedStackId;
        private Vector2 _scrollPosition;
        private GUIStyle _slotStyle;
        private GUIStyle _quantityStyle;
        private GUIStyle _quantityShadowStyle;
        private GUIStyle _hotkeyStyle;
        private GUIStyle _placeholderStyle;

        public void Bind(PlayerInventory playerInventory, InventorySkin skin)
        {
            _playerInventory = playerInventory;
            _skin = skin;
            if (_playerInventory == null)
            {
                Debug.LogError($"{nameof(InventoryView)} requires a player inventory.", this);
                enabled = false;
                return;
            }

            if (_skin == null || _skin.SlotBackground == null)
            {
                Debug.LogWarning(
                    $"{nameof(InventoryView)} has no slot artwork; slots fall back to the built-in GUI skin. " +
                    "Run 'Planet Survival/Setup UI Art' to generate the inventory skin asset.", this);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                _isPanelOpen = !_isPanelOpen;
            }

            for (int i = 0; i < QuickBarConfiguration.SlotCount; i++)
            {
                KeyCode key = i == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + i);
                if (Input.GetKeyDown(key))
                {
                    TryUseQuickSlot(i);
                }
            }
        }

        private void OnGUI()
        {
            if (_playerInventory == null)
            {
                return;
            }

            EnsureStyles();
            DrawQuickBar();
            if (_isPanelOpen)
            {
                DrawInventoryPanel();
            }
        }

        private void DrawQuickBar()
        {
            float width = QuickBarConfiguration.SlotCount * (QuickSlotSize + SlotSpacing) + SlotSpacing;
            var area = new Rect((Screen.width - width) * 0.5f, Screen.height - QuickSlotSize - 46f, width, QuickSlotSize + 40f);
            PanelBackground.Draw(area);

            for (int i = 0; i < QuickBarConfiguration.SlotCount; i++)
            {
                var slot = new Rect(
                    area.x + SlotSpacing + i * (QuickSlotSize + SlotSpacing),
                    area.y + SlotSpacing,
                    QuickSlotSize,
                    QuickSlotSize);
                ItemStack stack = GetQuickStack(i);
                if (DrawSlot(slot, stack, $"{(i + 1) % 10}", false) && stack != null)
                {
                    _playerInventory.Use(stack.StackId);
                }
            }

            var caption = new Rect(area.x, area.yMax - 20f, area.width, 18f);
            GUI.Label(caption, $"{_playerInventory.Inventory.UsedCapacity} / {_playerInventory.Inventory.TotalCapacity}    [I] Inventory", _hotkeyStyle);
        }

        private void DrawInventoryPanel()
        {
            float width = Mathf.Min(520f, Screen.width - 32f);
            float height = Mathf.Min(460f, Screen.height - 140f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            PanelBackground.Draw(area, new Color(0.05f, 0.07f, 0.09f, 0.94f));

            GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 12f, area.width - 28f, area.height - 24f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Inventory    {_playerInventory.Inventory.UsedCapacity} / {_playerInventory.Inventory.TotalCapacity}");
            if (GUILayout.Button("Close", GUILayout.Width(70f)))
            {
                _isPanelOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            DrawInventoryGrid(area.width - 28f, area.height - 24f);
            DrawSelectedItemActions();
            GUILayout.EndArea();
        }

        private void DrawInventoryGrid(float panelWidth, float panelHeight)
        {
            int columns = Mathf.Max(1, Mathf.FloorToInt((panelWidth - 24f) / (InventorySlotSize + SlotSpacing)));
            float gridHeight = Mathf.Max(InventorySlotSize + 16f, panelHeight - 170f);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(gridHeight));

            IReadOnlyList<ItemStack> stacks = _playerInventory.Inventory.Stacks;
            for (int rowStart = 0; rowStart < stacks.Count; rowStart += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int index = rowStart + column;
                    if (index >= stacks.Count)
                    {
                        GUILayout.Space(InventorySlotSize + SlotSpacing);
                        continue;
                    }

                    ItemStack stack = stacks[index];
                    Rect slot = GUILayoutUtility.GetRect(InventorySlotSize, InventorySlotSize,
                        GUILayout.Width(InventorySlotSize), GUILayout.Height(InventorySlotSize));
                    if (DrawSlot(slot, stack, string.Empty, stack.StackId == _selectedStackId))
                    {
                        _selectedStackId = stack.StackId;
                    }

                    GUILayout.Space(SlotSpacing);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(SlotSpacing);
            }

            if (stacks.Count == 0)
            {
                GUILayout.Label("Inventory is empty.");
            }

            GUILayout.EndScrollView();
        }

        private void DrawSelectedItemActions()
        {
            ItemStack selected = _playerInventory.Inventory.FindStack(_selectedStackId);
            if (selected == null)
            {
                _selectedStackId = null;
                GUILayout.Label("Select an item slot to view actions.");
                return;
            }

            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(56f));
            Rect preview = GUILayoutUtility.GetRect(52f, 52f, GUILayout.Width(52f), GUILayout.Height(52f));
            DrawSlot(preview, selected, string.Empty, false);

            GUILayout.BeginVertical();
            GUILayout.Label($"{selected.Definition.DisplayName}  ×{selected.Quantity}");
            if (!string.IsNullOrWhiteSpace(selected.Definition.Description))
            {
                GUILayout.Label(selected.Definition.Description, _placeholderStyle);
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Quick", GUILayout.Width(64f), GUILayout.Height(28f)))
            {
                AssignFirstAvailableSlot(selected.StackId);
            }

            GUI.enabled = selected.Definition.CanUse;
            if (GUILayout.Button("Use", GUILayout.Width(54f), GUILayout.Height(28f)))
            {
                _playerInventory.Use(selected.StackId);
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>Draws one slot frame with its icon, quantity and hotkey. Returns true when clicked.</summary>
        private bool DrawSlot(Rect slot, ItemStack stack, string hotkey, bool selected)
        {
            bool hovered = slot.Contains(Event.current.mousePosition);
            Color previousColor = GUI.color;
            GUI.color = ResolveSlotTint(stack != null, hovered);
            bool clicked = GUI.Button(slot, GUIContent.none, _slotStyle);
            GUI.color = previousColor;

            if (stack != null)
            {
                DrawItem(slot, stack);
            }

            if (!string.IsNullOrEmpty(hotkey))
            {
                GUI.Label(new Rect(slot.x + 6f, slot.y + 3f, 18f, 16f), hotkey, _hotkeyStyle);
            }

            if (selected)
            {
                Color outline = _skin != null ? _skin.SelectionColor : new Color(1f, 0.72f, 0.28f);
                GUI.DrawTexture(slot, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, outline,
                    new Vector4(SelectionOutlineWidth, SelectionOutlineWidth, SelectionOutlineWidth, SelectionOutlineWidth),
                    Vector4.zero);
            }

            return clicked;
        }

        private void DrawItem(Rect slot, ItemStack stack)
        {
            float padding = _skin != null ? _skin.IconPadding : 7f;
            var content = new Rect(slot.x + padding, slot.y + padding, slot.width - padding * 2f, slot.height - padding * 2f);
            if (stack.Definition.Icon != null)
            {
                DrawIcon(content, stack.Definition.Icon);
            }
            else
            {
                GUI.Label(content, stack.Definition.DisplayName, _placeholderStyle);
            }

            if (stack.Quantity > 1)
            {
                var quantity = new Rect(slot.x, slot.yMax - 21f, slot.width - 7f, 16f);
                string text = $"×{stack.Quantity}";
                GUI.Label(new Rect(quantity.x + 1f, quantity.y + 1f, quantity.width, quantity.height), text, _quantityShadowStyle);
                GUI.Label(quantity, text, _quantityStyle);
            }
        }

        /// <summary>Draws a sprite through its texture rectangle so packed or trimmed sprites stay correct.</summary>
        private static void DrawIcon(Rect area, Sprite icon)
        {
            Texture2D texture = icon.texture;
            if (texture == null)
            {
                return;
            }

            Rect textureRect = icon.textureRect;
            var coordinates = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(FitAspect(area, textureRect.width / textureRect.height), texture, coordinates, true);
        }

        private static Rect FitAspect(Rect area, float aspect)
        {
            if (aspect <= 0f)
            {
                return area;
            }

            float width = area.width;
            float height = width / aspect;
            if (height > area.height)
            {
                height = area.height;
                width = height * aspect;
            }

            return new Rect(area.x + (area.width - width) * 0.5f, area.y + (area.height - height) * 0.5f, width, height);
        }

        private Color ResolveSlotTint(bool occupied, bool hovered)
        {
            if (_skin == null)
            {
                return Color.white;
            }

            Color tint = occupied ? _skin.SlotTint : _skin.EmptySlotTint;
            return hovered ? _skin.HoverTint : tint;
        }

        private void EnsureStyles()
        {
            if (_slotStyle != null)
            {
                return;
            }

            Texture2D background = _skin != null ? _skin.SlotBackground : null;
            if (background != null)
            {
                int border = _skin.SlotBorder;
                _slotStyle = new GUIStyle { border = new RectOffset(border, border, border, border) };
                _slotStyle.normal.background = background;
                _slotStyle.hover.background = background;
                _slotStyle.active.background = background;
            }
            else
            {
                _slotStyle = new GUIStyle(GUI.skin.box);
            }

            _quantityStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            _quantityStyle.normal.textColor = Color.white;

            _quantityShadowStyle = new GUIStyle(_quantityStyle);
            _quantityShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);

            _hotkeyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
            _hotkeyStyle.normal.textColor = new Color(0.78f, 0.84f, 0.9f);

            _placeholderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true
            };
            _placeholderStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
        }

        private ItemStack GetQuickStack(int slotIndex)
        {
            string stackId = _playerInventory.QuickBar.StackIds[slotIndex];
            return _playerInventory.Inventory.FindStack(stackId);
        }

        private void TryUseQuickSlot(int slotIndex)
        {
            ItemStack stack = GetQuickStack(slotIndex);
            if (stack != null)
            {
                _playerInventory.Use(stack.StackId);
            }
        }

        private void AssignFirstAvailableSlot(string stackId)
        {
            if (!_playerInventory.QuickBar.AssignFirstAvailable(stackId))
            {
                _playerInventory.QuickBar.Assign(0, stackId);
            }
        }
    }
}
