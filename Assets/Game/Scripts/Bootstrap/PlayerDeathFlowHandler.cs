using PlanetSurvival.Core.Flow;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class PlayerDeathFlowHandler : MonoBehaviour
    {
        private PlayerSurvival _survival;
        private GameFlowController _flowController;

        public void Bind(PlayerSurvival survival, GameFlowController flowController)
        {
            Unsubscribe();
            _survival = survival;
            _flowController = flowController;

            if (_survival == null || _flowController == null)
            {
                Debug.LogError($"{nameof(PlayerDeathFlowHandler)} requires survival and game flow references.", this);
                enabled = false;
                return;
            }

            _survival.Died += HandleDied;
            if (_survival.IsDead)
            {
                HandleDied();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void HandleDied()
        {
            _flowController.EndGame();
        }

        private void Unsubscribe()
        {
            if (_survival != null)
            {
                _survival.Died -= HandleDied;
            }
        }
    }
}
