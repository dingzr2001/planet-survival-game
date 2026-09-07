using System.Collections.Generic;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.Storage
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    [DisallowMultipleComponent]
    public sealed class StorageView : MonoBehaviour
    {
        private const float PanelWidth = 980f;
        private const float PanelHeight = 620f;
        private const float SlotSize = 62f;
        private const float SlotSpacing = 5f;

        private InventorySkin _skin;
        private InventoryModel _storage;
        private PlayerInventory _playerInventory;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private InventoryView _inventoryView;
        private string _storageName = string.Empty;
        private string _selectedStackId;
        private bool _selectedFromPlayer;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;
        private string _feedback = "Select a stack to transfer items.";
        private Vector2 _playerScroll;
        private Vector2 _storageScroll;
        private GUIStyle _slotStyle;
        private GUIStyle _quantityStyle;
        private GUIStyle _placeholderStyle;

        public bool IsOpen { get; private set; }

        public void Bind(InventorySkin skin)
        {
            _skin = skin;
        }

        public void Open(string storageName, InventoryModel storage, PlayerInventory playerInventory,
            PlanarPlayerMotor playerMotor = null, PlayerInteractor playerInteractor = null)
        {
            if (storage == null || playerInventory == null)
            {
                Debug.LogError($"{nameof(StorageView)} cannot open without both inventories.", this);
                return;
            }

            Close();
            _storageName = string.IsNullOrWhiteSpace(storageName) ? "STORAGE" : storageName.ToUpperInvariant();
            _storage = storage;
            _playerInventory = playerInventory;
            _selectedStackId = null;
            _feedback = "Select a stack to transfer items.";
            CaptureAndLockControls(playerMotor, playerInteractor);
            IsOpen = true;
        }

        public InventoryOperationResult Store(string stackId, int quantity)
        {
            if (_playerInventory == null || _storage == null)
            {
                return InventoryOperationResult.Fail(InventoryFailure.InvalidItem, "Storage is not open.");
            }

            InventoryOperationResult result = InventoryTransfer.Transfer(
                _playerInventory.Inventory, _storage, stackId, quantity);
            if (result.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
            }

            return result;
        }

        public InventoryOperationResult Take(string stackId, int quantity)
        {
            if (_playerInventory == null || _storage == null)
            {
                return InventoryOperationResult.Fail(InventoryFailure.InvalidItem, "Storage is not open.");
            }

            InventoryOperationResult result = InventoryTransfer.Transfer(
                _storage, _playerInventory.Inventory, stackId, quantity);
            if (result.Succeeded)
            {
                _playerInventory.RefreshQuickBarAssignments();
            }

            return result;
        }

        public void Close()
        {
            IsOpen = false;
            ReleaseControls();
            _storage = null;
            _playerInventory = null;
            _selectedStackId = null;
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            ReleaseControls();
        }

        private void OnGUI()
        {
            if (!IsOpen || _storage == null || _playerInventory == null)
            {
                return;
            }

            EnsureStyles();
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .68f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.035f, .05f, .065f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{_storageName}  ·  ITEM STORAGE");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(76f), GUILayout.Height(26f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);

            float gridHeight = Mathf.Max(180f, panel.height - 150f);
            GUILayout.BeginHorizontal();
            DrawInventoryColumn("BACKPACK", _playerInventory.Inventory, true, ref _playerScroll, gridHeight);
            GUILayout.Space(10f);
            DrawInventoryColumn(_storageName, _storage, false, ref _storageScroll, gridHeight);
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            DrawTransferActions();
            GUILayout.EndArea();
        }

        private void DrawInventoryColumn(string title, InventoryModel inventory, bool fromPlayer,
            ref Vector2 scroll, float height)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(450f), GUILayout.Height(height));
            GUILayout.Label($"{title}    {inventory.UsedSlots}/{inventory.TotalSlots} slots  ·  " +
                            $"{inventory.UsedCapacity}/{inventory.TotalCapacity} capacity");
            scroll = GUILayout.BeginScrollView(scroll);
            const int columns = 6;
            IReadOnlyList<ItemStack> stacks = inventory.Stacks;
            for (int rowStart = 0; rowStart < inventory.TotalSlots; rowStart += columns)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    int index = rowStart + column;
                    if (index >= inventory.TotalSlots)
                    {
                        break;
                    }

                    ItemStack stack = index < stacks.Count ? stacks[index] : null;
                    Rect slot = GUILayoutUtility.GetRect(SlotSize, SlotSize,
                        GUILayout.Width(SlotSize), GUILayout.Height(SlotSize));
                    if (DrawSlot(slot, stack, stack != null && stack.StackId == _selectedStackId &&
                            _selectedFromPlayer == fromPlayer) && stack != null)
                    {
                        _selectedStackId = stack.StackId;
                        _selectedFromPlayer = fromPlayer;
                        _feedback = $"Selected {stack.Definition.DisplayName} ×{stack.Quantity}.";
                    }
                    GUILayout.Space(SlotSpacing);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(SlotSpacing);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawTransferActions()
        {
            InventoryModel source = _selectedFromPlayer ? _playerInventory.Inventory : _storage;
            ItemStack selected = source.FindStack(_selectedStackId);
            GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(58f));
            GUILayout.Label(selected == null ? _feedback :
                $"{selected.Definition.DisplayName} ×{selected.Quantity}  ·  {_feedback}");
            GUILayout.FlexibleSpace();
            GUI.enabled = selected != null;
            string verb = _selectedFromPlayer ? "STORE" : "TAKE";
            if (GUILayout.Button($"{verb} 1", GUILayout.Width(86f), GUILayout.Height(32f)))
            {
                TransferSelected(1);
            }
            if (GUILayout.Button($"{verb} ALL", GUILayout.Width(96f), GUILayout.Height(32f)))
            {
                TransferSelected(selected.Quantity);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void TransferSelected(int quantity)
        {
            InventoryOperationResult result = _selectedFromPlayer
                ? Store(_selectedStackId, quantity)
                : Take(_selectedStackId, quantity);
            _feedback = result.Succeeded ? $"Transferred {quantity} item(s)." : result.Message;
            InventoryModel source = _selectedFromPlayer ? _playerInventory.Inventory : _storage;
            if (source.FindStack(_selectedStackId) == null)
            {
                _selectedStackId = null;
            }
        }

        private bool DrawSlot(Rect area, ItemStack stack, bool selected)
        {
            bool clicked = GUI.Button(area, GUIContent.none, _slotStyle);
            if (stack != null)
            {
                Rect content = new Rect(area.x + 6f, area.y + 6f, area.width - 12f, area.height - 12f);
                if (stack.Definition.Icon != null)
                {
                    Sprite icon = stack.Definition.Icon;
                    Rect source = icon.textureRect;
                    Rect coordinates = new Rect(source.x / icon.texture.width, source.y / icon.texture.height,
                        source.width / icon.texture.width, source.height / icon.texture.height);
                    GUI.DrawTextureWithTexCoords(content, icon.texture, coordinates, true);
                }
                else
                {
                    GUI.Label(content, stack.Definition.DisplayName, _placeholderStyle);
                }

                if (stack.Quantity > 1)
                {
                    GUI.Label(new Rect(area.x, area.yMax - 20f, area.width - 5f, 18f),
                        $"×{stack.Quantity}", _quantityStyle);
                }
            }

            if (selected)
            {
                Color outline = _skin != null ? _skin.SelectionColor : new Color(1f, .72f, .28f);
                GUI.DrawTexture(area, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, outline,
                    new Vector4(2f, 2f, 2f, 2f), Vector4.zero);
            }

            return clicked;
        }

        private void CaptureAndLockControls(PlanarPlayerMotor playerMotor, PlayerInteractor playerInteractor)
        {
            _playerMotor = playerMotor;
            _playerInteractor = playerInteractor;
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
            if (_slotStyle != null) return;
            Texture2D background = _skin != null ? _skin.SlotBackground : null;
            _slotStyle = background == null ? new GUIStyle(GUI.skin.box) : new GUIStyle();
            if (background != null)
            {
                _slotStyle.normal.background = background;
                _slotStyle.hover.background = background;
                _slotStyle.active.background = background;
            }

            _quantityStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _quantityStyle.normal.textColor = Color.white;
            _placeholderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                wordWrap = true
            };
            _placeholderStyle.normal.textColor = new Color(.8f, .86f, .9f);
        }
    }
}
