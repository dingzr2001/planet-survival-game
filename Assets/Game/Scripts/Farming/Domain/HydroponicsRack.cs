using System;
using System.Collections.Generic;

namespace PlanetSurvival.Farming.Domain
{
    /// <summary>
    /// A fixed set of growing trays. The rack owns no rules of its own; it exists so the session can hand
    /// one object to the fixture and so the number of trays is decided in one place.
    /// </summary>
    public sealed class HydroponicsRack
    {
        private readonly HydroponicsSlot[] _slots;

        public HydroponicsRack(int slotCount)
        {
            if (slotCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "A rack needs at least one tray.");
            }

            _slots = new HydroponicsSlot[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                _slots[i] = new HydroponicsSlot(i);
            }
        }

        public IReadOnlyList<HydroponicsSlot> Slots => _slots;

        /// <summary>Trays that can be harvested right now.</summary>
        public int RipeCount(double nowDays)
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsRipe(nowDays))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The first free tray, or null when every tray is planted.</summary>
        public HydroponicsSlot FirstEmptySlot()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].IsPlanted)
                {
                    return _slots[i];
                }
            }

            return null;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].Clear();
            }
        }
    }
}
