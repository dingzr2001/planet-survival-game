using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Player.Interaction
{
    [DisallowMultipleComponent]
    public sealed class VitalConsumable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _prompt = "Consume";
        [SerializeField] private VitalType _vitalType = VitalType.Hunger;
        [SerializeField] private float _amount = 20f;

        public string Prompt => _prompt;

        public bool CanInteract(in InteractionContext context)
        {
            return context.Survival != null;
        }

        public void Interact(in InteractionContext context)
        {
            context.Survival.Apply(_vitalType, _amount);
            Destroy(gameObject);
        }
    }
}
