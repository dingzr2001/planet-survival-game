using UnityEngine;

namespace PlanetSurvival.Building.Runtime
{
    /// <summary>
    /// Resolves which placed structure the pointer is over. Every building panel opens through this, so a
    /// click can only ever reach the structure actually under the cursor: there is no nearest-on-screen
    /// fallback, because guessing is exactly what made a click open the neighbouring machine.
    /// </summary>
    public static class BuildingPointer
    {
        /// <summary>
        /// The building under <paramref name="screenPosition"/>, or false when the ray hits none, when the
        /// nearest hit is not a building, or when that building is out of the player's reach.
        /// </summary>
        public static bool TryPick(Camera camera, Vector2 screenPosition, Transform player, float reach,
            out BuildSiteView building)
        {
            building = null;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, camera.farClipPlane, ~0, QueryTriggerInteraction.Collide);
            float nearest = float.MaxValue;
            Collider nearestCollider = null;
            for (int i = 0; i < hits.Length; i++)
            {
                BuildSiteView candidate = hits[i].collider.GetComponentInParent<BuildSiteView>();
                if (candidate == null || hits[i].distance >= nearest)
                {
                    continue;
                }

                nearest = hits[i].distance;
                nearestCollider = hits[i].collider;
                building = candidate;
            }

            if (building == null)
            {
                return false;
            }

            if (player != null && !IsWithinReach(nearestCollider, player.position, reach))
            {
                building = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Reach is measured to the nearest point of the structure's own volume, the same way the proximity
        /// focus measures it, so clicking a building costs exactly the walking that pressing E would.
        /// </summary>
        private static bool IsWithinReach(Collider collider, Vector3 playerPosition, float reach)
        {
            if (collider == null)
            {
                return false;
            }

            Vector3 closest = collider.ClosestPoint(playerPosition);
            return (closest - playerPosition).sqrMagnitude <= reach * reach;
        }
    }
}
