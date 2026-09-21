using System;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Transport.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Session-owned state for one transfer post. The small buffer makes every hand-off transactional:
    /// an item is only removed from a source after this post has proved it has room for it.
    /// </summary>
    public sealed class ItemTransferPost
    {
        public const int BufferCapacity = 8;
        public const float ItemsPerSecond = 2f;

        // Item size affects a backpack's carrying capacity, but this machine has eight physical item slots.
        private readonly InventoryModel _buffer = new(int.MaxValue, 1, BufferCapacity);

        public ItemTransferPost(int postNumber, Color groupColor)
        {
            PostNumber = Mathf.Max(1, postNumber);
            GroupNumber = PostNumber;
            GroupColor = Opaque(groupColor);
            _buffer.Changed += NotifyChanged;
        }

        public InventoryModel Buffer => _buffer;
        public string InputEndpointId { get; private set; } = string.Empty;
        public string OutputEndpointId { get; private set; } = string.Empty;
        public int PostNumber { get; }
        public int GroupNumber { get; private set; }
        public Color GroupColor { get; private set; }
        public long TotalItemsMoved { get; private set; }
        public ItemDefinition LastMovedItem { get; private set; }
        public int LastMovedQuantity { get; private set; }
        public event Action Changed;

        public ItemDefinition BufferedItem => _buffer.Stacks.Count > 0 ? _buffer.Stacks[0].Definition : null;
        public int BufferedQuantity => _buffer.Stacks.Count > 0 ? _buffer.Stacks[0].Quantity : 0;

        public void ConfigureInput(string endpointId)
        {
            string normalized = endpointId ?? string.Empty;
            if (InputEndpointId == normalized)
            {
                return;
            }

            InputEndpointId = normalized;
            Changed?.Invoke();
        }

        public void ConfigureOutput(string endpointId)
        {
            string normalized = endpointId ?? string.Empty;
            if (OutputEndpointId == normalized)
            {
                return;
            }

            OutputEndpointId = normalized;
            Changed?.Invoke();
        }

        public void ConfigureGroup(int number, Color color)
        {
            int safeNumber = Mathf.Max(1, number);
            Color safeColor = Opaque(color);
            if (GroupNumber == safeNumber && GroupColor == safeColor)
            {
                return;
            }

            GroupNumber = safeNumber;
            GroupColor = safeColor;
            Changed?.Invoke();
        }

        public int AcceptableQuantity(ItemDefinition item, int maximumQuantity)
        {
            if (item == null || maximumQuantity <= 0)
            {
                return 0;
            }

            ItemDefinition buffered = BufferedItem;
            if (buffered != null && buffered.ItemId != item.ItemId)
            {
                return 0;
            }

            int accepted = Mathf.Min(maximumQuantity, BufferCapacity - BufferedQuantity);
            while (accepted > 0 && !_buffer.CanAdd(item, accepted).Succeeded)
            {
                accepted--;
            }

            return accepted;
        }

        public int Insert(ItemDefinition item, int quantity)
        {
            int accepted = AcceptableQuantity(item, quantity);
            return accepted > 0 && _buffer.Add(item, accepted).Succeeded ? accepted : 0;
        }

        public int Extract(int maximumQuantity)
        {
            if (maximumQuantity <= 0 || _buffer.Stacks.Count == 0)
            {
                return 0;
            }

            ItemStack stack = _buffer.Stacks[0];
            int extracted = Mathf.Min(maximumQuantity, stack.Quantity);
            return _buffer.Remove(stack.StackId, extracted).Succeeded ? extracted : 0;
        }

        public void RecordTransfer(ItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
            {
                return;
            }

            LastMovedItem = item;
            LastMovedQuantity = quantity;
            TotalItemsMoved += quantity;
            Changed?.Invoke();
        }

        public void ClearLinks()
        {
            bool changed = InputEndpointId.Length > 0 || OutputEndpointId.Length > 0;
            InputEndpointId = string.Empty;
            OutputEndpointId = string.Empty;
            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static Color Opaque(Color color)
        {
            color.a = 1f;
            return color;
        }
    }
}
