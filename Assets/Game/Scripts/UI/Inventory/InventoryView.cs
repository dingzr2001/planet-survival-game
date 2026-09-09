using System.Collections.Generic;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Water.Runtime;
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
        private const float PromptRowHeight = 26f;
        private const string WaterBottleTextureResource = "Water/WaterBottle";
        private const float FeedbackDurationSeconds = 2.5f;

        private PlayerInventory _playerInventory;
        private PlayerWaterBottle _waterBottle;
        private InventorySkin _skin;
        private bool _isPanelOpen;
        private bool _isWaterBottleSelected;
        private string _selectedStackId;
        private string _waterFeedback = string.Empty;
        private string _interactionPrompt = string.Empty;
        private float _waterFeedbackExpiresAt;
        private Vector2 _scrollPosition;
        private Texture2D _waterBottleTexture;
        private GUIStyle _slotStyle;
        private GUIStyle _quantityStyle;
        private GUIStyle _quantityShadowStyle;
        private GUIStyle _hotkeyStyle;
        private GUIStyle _placeholderStyle;
        private GUIStyle _promptStyle;

        public bool HasWaterBottle => _waterBottle != null;

        /// <summary>
        /// Shows the current interaction hint on top of the quick bar, so every on-screen
        /// instruction lives in one place instead of floating over the world.
        /// </summary>
        public void SetInteractionPrompt(string prompt)
        {
            _interactionPrompt = prompt ?? string.Empty;
        }

        public void Bind(PlayerInventory playerInventory, InventorySkin skin, PlayerWaterBottle waterBottle = null)
        {
            _playerInventory = playerInventory;
            _skin = skin;
            _waterBottle = waterBottle;
            _waterBottleTexture = Resources.Load<Texture2D>(WaterBottleTextureResource);
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

            if (Input.GetKeyDown(KeyCode.Q))
            {
                TryDrinkWater();
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
            int visibleSlotCount = QuickBarConfiguration.SlotCount + (_waterBottle != null ? 1 : 0);
            float width = visibleSlotCount * (QuickSlotSize + SlotSpacing) + SlotSpacing;
            bool hasPrompt = !string.IsNullOrWhiteSpace(_interactionPrompt);
            float promptHeight = hasPrompt ? PromptRowHeight : 0f;
            float height = QuickSlotSize + 40f + promptHeight;
            var area = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 6f, width, height);
            PanelBackground.Draw(area);

            if (hasPrompt)
            {
                var promptRow = new Rect(area.x + SlotSpacing, area.y + 4f, area.width - SlotSpacing * 2f, PromptRowHeight - 6f);
                GUI.Label(promptRow, $"Press E to {_interactionPrompt}", _promptStyle);
            }

            float slotsTop = area.y + promptHeight + SlotSpacing;
            float itemSlotsStart = area.x + SlotSpacing;
            if (_waterBottle != null)
            {
                var bottleSlot = new Rect(itemSlotsStart, slotsTop, QuickSlotSize, QuickSlotSize);
                if (DrawWaterBottleSlot(bottleSlot, "Q", false))
                {
                    TryDrinkWater();
                }

                itemSlotsStart += QuickSlotSize + SlotSpacing;
            }

            for (int i = 0; i < QuickBarConfiguration.SlotCount; i++)
            {
                var slot = new Rect(
                    itemSlotsStart + i * (QuickSlotSize + SlotSpacing),
                    slotsTop,
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
            if (!string.IsNullOrEmpty(_waterFeedback) && Time.unscaledTime < _waterFeedbackExpiresAt)
            {
                GUI.Label(new Rect(area.x, area.y - 24f, area.width, 20f), _waterFeedback, _hotkeyStyle);
            }
        }

        private void DrawInventoryPanel()
        {
            float width = Mathf.Min(520f, Screen.width - 32f);
            float height = Mathf.Min(460f, Screen.height - 140f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            PanelBackground.Draw(area, new Color(0.05f, 0.07f, 0.09f, 0.94f));

            GUILayout.BeginArea(new Rect(area.x + 14f, area.y + 12f, area.width - 28f, area.height - 24f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Inventory    {_playerInventory.Inventory.UsedSlots} / " +
                            $"{_playerInventory.Inventory.TotalSlots} slots  ·  " +
                            $"{_playerInventory.Inventory.UsedCapacity} / " +
                            $"{_playerInventory.Inventory.TotalCapacity} capacity");
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
            int equipmentOffset = _waterBottle != null ? 1 : 0;
            int itemCount = _playerInventory.Inventory.TotalSlots + equipmentOffset;
            for (int rowStart = 0; rowStart < itemCount; rowStart += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int index = rowStart + column;
                    if (index >= itemCount)
                    {
                        GUILayout.Space(InventorySlotSize + SlotSpacing);
                        continue;
                    }

                    Rect slot = GUILayoutUtility.GetRect(InventorySlotSize, InventorySlotSize,
                        GUILayout.Width(InventorySlotSize), GUILayout.Height(InventorySlotSize));
                    if (equipmentOffset == 1 && index == 0)
                    {
                        if (DrawWaterBottleSlot(slot, string.Empty, _isWaterBottleSelected))
                        {
                            _isWaterBottleSelected = true;
                            _selectedStackId = null;
                        }

                        GUILayout.Space(SlotSpacing);
                        continue;
                    }

                    int stackIndex = index - equipmentOffset;
                    ItemStack stack = stackIndex < stacks.Count ? stacks[stackIndex] : null;
                    if (DrawSlot(slot, stack, string.Empty,
                            stack != null && stack.StackId == _selectedStackId) && stack != null)
                    {
                        _selectedStackId = stack.StackId;
                        _isWaterBottleSelected = false;
                    }

                    GUILayout.Space(SlotSpacing);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(SlotSpacing);
            }

            GUILayout.EndScrollView();
        }

        private void DrawSelectedItemActions()
        {
            if (_isWaterBottleSelected && _waterBottle != null)
            {
                DrawWaterBottleActions();
                return;
            }

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

        private void DrawWaterBottleActions()
        {
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(64f));
            Rect preview = GUILayoutUtility.GetRect(56f, 56f, GUILayout.Width(56f), GUILayout.Height(56f));
            DrawWaterBottleSlot(preview, string.Empty, false);
            GUILayout.BeginVertical();
            GUILayout.Label("Personal Water Bottle");
            GUILayout.Label(
                $"{_waterBottle.Container.CurrentMilliliters} / {_waterBottle.Container.CapacityMilliliters} mL · " +
                $"Drink {PlayerWaterBottle.DrinkVolumeMilliliters} mL",
                _placeholderStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUI.enabled = _waterBottle.CanDrink;
            if (GUILayout.Button("Drink", GUILayout.Width(64f), GUILayout.Height(32f)))
            {
                TryDrinkWater();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private bool DrawWaterBottleSlot(Rect slot, string hotkey, bool selected)
        {
            bool hovered = slot.Contains(Event.current.mousePosition);
            Color previousColor = GUI.color;
            GUI.color = ResolveSlotTint(true, hovered);
            bool clicked = GUI.Button(slot, GUIContent.none, _slotStyle);
            GUI.color = previousColor;

            float padding = _skin != null ? _skin.IconPadding : 7f;
            if (_waterBottleTexture != null)
            {
                var content = new Rect(slot.x + padding, slot.y + padding,
                    slot.width - padding * 2f, slot.height - padding * 2f);
                GUI.DrawTexture(content, _waterBottleTexture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(slot, "Water", _placeholderStyle);
            }

            if (!string.IsNullOrEmpty(hotkey))
            {
                GUI.Label(new Rect(slot.x + 6f, slot.y + 3f, 18f, 16f), hotkey, _hotkeyStyle);
            }

            string volume = $"{_waterBottle.Container.CurrentMilliliters} mL";
            var amount = new Rect(slot.x, slot.yMax - 21f, slot.width - 6f, 16f);
            GUI.Label(new Rect(amount.x + 1f, amount.y + 1f, amount.width, amount.height),
                volume, _quantityShadowStyle);
            GUI.Label(amount, volume, _quantityStyle);

            if (selected)
            {
                Color outline = _skin != null ? _skin.SelectionColor : new Color(1f, .72f, .28f);
                GUI.DrawTexture(slot, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, outline,
                    new Vector4(SelectionOutlineWidth, SelectionOutlineWidth, SelectionOutlineWidth, SelectionOutlineWidth),
                    Vector4.zero);
            }

            return clicked;
        }

        private void TryDrinkWater()
        {
            if (_waterBottle == null)
            {
                return;
            }

            WaterDrinkResult result = _waterBottle.TryDrink();
            _waterFeedback = result switch
            {
                WaterDrinkResult.Succeeded =>
                    $"Drank {PlayerWaterBottle.DrinkVolumeMilliliters} mL · Thirst restored",
                WaterDrinkResult.NotEnoughWater => $"At least {PlayerWaterBottle.DrinkVolumeMilliliters} mL is needed.",
                WaterDrinkResult.NotThirsty => "You are not thirsty.",
                WaterDrinkResult.ActorUnavailable => "You cannot drink right now.",
                _ => "Water bottle is unavailable."
            };
            _waterFeedbackExpiresAt = Time.unscaledTime + FeedbackDurationSeconds;
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
                SpriteIcon.Draw(content, stack.Definition.Icon);
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
                // Slots and the frame art are both square, so the whole texture is simply scaled to
                // the slot. A nine-sliced border would keep its corners at texture-pixel size and
                // therefore read far thicker than the source art at these slot sizes.
                _slotStyle = new GUIStyle();
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

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            _promptStyle.normal.textColor = new Color(1f, 0.86f, 0.5f);
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
