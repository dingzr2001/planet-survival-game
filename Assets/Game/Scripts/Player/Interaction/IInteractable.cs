namespace PlanetSurvival.Player.Interaction
{
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract(in InteractionContext context);
        void Interact(in InteractionContext context);
    }
}
