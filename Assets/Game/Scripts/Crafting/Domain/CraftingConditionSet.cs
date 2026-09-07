using System;
using System.Collections.Generic;

namespace PlanetSurvival.Crafting.Domain
{
    public sealed class CraftingConditionSet : ICraftingConditionProvider
    {
        private readonly HashSet<string> _conditionIds;

        public CraftingConditionSet(IEnumerable<string> conditionIds)
        {
            _conditionIds = conditionIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(conditionIds, StringComparer.Ordinal);
        }

        public bool IsConditionMet(string conditionId)
        {
            return !string.IsNullOrWhiteSpace(conditionId) && _conditionIds.Contains(conditionId);
        }
    }
}
