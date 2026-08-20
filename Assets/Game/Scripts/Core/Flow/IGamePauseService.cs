namespace PlanetSurvival.Core.Flow
{
    public interface IGamePauseService
    {
        bool IsPaused { get; }
        void SetPaused(bool isPaused);
    }
}
