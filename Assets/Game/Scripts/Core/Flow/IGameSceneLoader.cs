using System;

namespace PlanetSurvival.Core.Flow
{
    public interface IGameSceneLoader
    {
        void Load(string sceneName, Action completed);
    }
}
