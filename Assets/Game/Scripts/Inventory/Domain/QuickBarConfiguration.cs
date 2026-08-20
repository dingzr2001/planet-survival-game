using System;
using System.Collections.Generic;

namespace PlanetSurvival.Inventory.Domain
{
    public sealed class QuickBarConfiguration : IDisposable
    {
        public const int SlotCount = 10;

        private readonly Inventory _inventory;
        private readonly string[] _stackIds = new string[SlotCount];

        public QuickBarConfiguration(Inventory inventory)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventory.StackRemoved += ClearStack;
        }

        public IReadOnlyList<string> StackIds => _stackIds;
        public event Action Changed;

        public bool Assign(int slotIndex, string stackId)
        {
            if (!IsValidSlot(slotIndex) || _inventory.FindStack(stackId) == null)
            {
                return false;
            }

            for (int i = 0; i < _stackIds.Length; i++)
            {
                if (_stackIds[i] == stackId)
                {
                    _stackIds[i] = null;
                }
            }

            _stackIds[slotIndex] = stackId;
            Changed?.Invoke();
            return true;
        }

        public bool Swap(int firstSlot, int secondSlot)
        {
            if (!IsValidSlot(firstSlot) || !IsValidSlot(secondSlot))
            {
                return false;
            }

            (_stackIds[firstSlot], _stackIds[secondSlot]) = (_stackIds[secondSlot], _stackIds[firstSlot]);
            Changed?.Invoke();
            return true;
        }

        public bool Clear(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || _stackIds[slotIndex] == null)
            {
                return false;
            }

            _stackIds[slotIndex] = null;
            Changed?.Invoke();
            return true;
        }

        public void Dispose()
        {
            _inventory.StackRemoved -= ClearStack;
        }

        private void ClearStack(string stackId)
        {
            bool changed = false;
            for (int i = 0; i < _stackIds.Length; i++)
            {
                if (_stackIds[i] != stackId)
                {
                    continue;
                }

                _stackIds[i] = null;
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < SlotCount;
        }
    }
}
