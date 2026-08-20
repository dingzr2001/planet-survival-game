using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using UnityEngine;

namespace PlanetSurvival.UI.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryView : MonoBehaviour
    {
        private const float SlotSize = 52f;
        private const float SlotSpacing = 4f;

        private PlayerInventory _playerInventory;
        private bool _isPanelOpen;

        public void Bind(PlayerInventory playerInventory)
        {
            _playerInventory = playerInventory;
            if (_playerInventory == null)
            {
                Debug.LogError($"{nameof(InventoryView)} requires a player inventory.", this);
                enabled = false;
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

            DrawQuickBar();
            if (_isPanelOpen)
            {
                DrawInventoryPanel();
            }
        }

        private void DrawQuickBar()
        {
            float width = QuickBarConfiguration.SlotCount * (SlotSize + SlotSpacing) + SlotSpacing;
            var area = new Rect((Screen.width - width) * 0.5f, Screen.height - SlotSize - 42f, width, SlotSize + 36f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < QuickBarConfiguration.SlotCount; i++)
            {
                ItemStack stack = GetQuickStack(i);
                string label = stack == null ? $"{(i + 1) % 10}" : $"{(i + 1) % 10}\n{stack.Definition.DisplayName}\n×{stack.Quantity}";
                if (GUILayout.Button(label, GUILayout.Width(SlotSize), GUILayout.Height(SlotSize)) && stack != null)
                {
                    _playerInventory.Use(stack.StackId);
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"{_playerInventory.Inventory.UsedCapacity} / {_playerInventory.Inventory.TotalCapacity}    [I] Inventory");
            GUILayout.EndArea();
        }

        private void DrawInventoryPanel()
        {
            float width = Mathf.Min(480f, Screen.width - 32f);
            float height = Mathf.Min(420f, Screen.height - 140f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height), GUI.skin.window);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Inventory    {_playerInventory.Inventory.UsedCapacity} / {_playerInventory.Inventory.TotalCapacity}");
            if (GUILayout.Button("Close", GUILayout.Width(70f)))
            {
                _isPanelOpen = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(8f);

            for (int i = 0; i < _playerInventory.Inventory.Stacks.Count; i++)
            {
                ItemStack stack = _playerInventory.Inventory.Stacks[i];
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{stack.Definition.DisplayName}  ×{stack.Quantity}");
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Quick", GUILayout.Width(64f)))
                {
                    AssignFirstAvailableSlot(stack.StackId);
                }

                GUI.enabled = stack.Definition.CanUse;
                if (GUILayout.Button("Use", GUILayout.Width(54f)))
                {
                    _playerInventory.Use(stack.StackId);
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
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
            for (int i = 0; i < QuickBarConfiguration.SlotCount; i++)
            {
                if (_playerInventory.QuickBar.StackIds[i] == null)
                {
                    _playerInventory.QuickBar.Assign(i, stackId);
                    return;
                }
            }

            _playerInventory.QuickBar.Assign(0, stackId);
        }
    }
}
