using System;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Player.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanetSurvival.Core.SceneManagement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ScenePortal : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _targetScene;
        [SerializeField] private string _prompt = "travel";

        private bool _isLoading;

        public string Prompt => _prompt;
        public string TargetScene => _targetScene;

        public void Configure(string targetScene, string prompt)
        {
            _targetScene = targetScene;
            _prompt = string.IsNullOrWhiteSpace(prompt) ? "travel" : prompt;
        }

        public bool CanInteract(in InteractionContext context)
        {
            return !_isLoading && context.Actor != null && !string.IsNullOrWhiteSpace(_targetScene);
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(_targetScene))
            {
                Debug.LogError($"Portal '{name}' cannot load scene '{_targetScene}'. Ensure it is included in Build Settings.", this);
                return;
            }

            _isLoading = true;
            AsyncOperation operation = SceneManager.LoadSceneAsync(_targetScene, LoadSceneMode.Single);
            if (operation == null)
            {
                _isLoading = false;
                throw new InvalidOperationException($"Unity could not start loading portal scene '{_targetScene}'.");
            }
        }
    }
}
