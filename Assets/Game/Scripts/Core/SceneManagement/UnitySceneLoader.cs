using System;
using PlanetSurvival.Core.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanetSurvival.Core.SceneManagement
{
    public sealed class UnitySceneLoader : IGameSceneLoader
    {
        public void Load(string sceneName, Action completed)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name cannot be empty.", nameof(sceneName));
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                throw new InvalidOperationException($"Unity could not start loading scene '{sceneName}'. Ensure it is included in Build Settings.");
            }

            operation.completed += _ => completed?.Invoke();
        }
    }
}
