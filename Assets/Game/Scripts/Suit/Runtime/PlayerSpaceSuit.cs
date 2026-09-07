using System;
using PlanetSurvival.Suit.Domain;
using UnityEngine;

namespace PlanetSurvival.Suit.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpaceSuit : MonoBehaviour
    {
        public SpaceSuitResources Resources { get; private set; }
        public bool IsEquipped { get; private set; }

        public event Action<bool> EquippedChanged;

        public void Bind(SpaceSuitResources resources, bool isEquipped)
        {
            Resources = resources;
            if (Resources == null)
            {
                Debug.LogError($"{nameof(PlayerSpaceSuit)} on '{name}' requires suit resources.", this);
                enabled = false;
                return;
            }

            enabled = true;
            SetEquipped(isEquipped);
        }

        public void SetEquipped(bool isEquipped)
        {
            if (IsEquipped == isEquipped)
            {
                return;
            }

            IsEquipped = isEquipped;
            EquippedChanged?.Invoke(IsEquipped);
        }
    }
}
