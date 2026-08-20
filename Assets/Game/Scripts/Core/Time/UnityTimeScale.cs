using PlanetSurvival.Core.Flow;
using UnityEngine;

namespace PlanetSurvival.Core.Time
{
    public sealed class UnityTimeScale : IGamePauseService
    {
        public bool IsPaused => Mathf.Approximately(UnityEngine.Time.timeScale, 0f);

        public void SetPaused(bool isPaused)
        {
            UnityEngine.Time.timeScale = isPaused ? 0f : 1f;
        }
    }
}
