using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Player.Interaction
{
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject actor, PlayerSurvival survival, PlayerInventory inventory)
        {
            Actor = actor;
            Survival = survival;
            Inventory = inventory;
        }

        public GameObject Actor { get; }
        public PlayerSurvival Survival { get; }
        public PlayerInventory Inventory { get; }
    }
}
