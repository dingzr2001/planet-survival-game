using PlanetSurvival.Core.Flow;
using UnityEngine;

namespace PlanetSurvival.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameFlowController))]
    public sealed class BootstrapSceneEntry : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<GameFlowController>().EnterInitialScene();
            Destroy(this);
        }
    }
}
