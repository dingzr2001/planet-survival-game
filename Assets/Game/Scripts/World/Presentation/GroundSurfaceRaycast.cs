using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// Finds where a camera ray meets the drawn ground, so a click lands on the spot drawn under the cursor
    /// even where the ground is drawn below or above the gameplay plane.
    /// </summary>
    public static class GroundSurfaceRaycast
    {
        // The steep camera moves a ray's ground point only a quarter metre per metre of height, so a few
        // steps settle it to well under a centimetre on any drawn slope.
        private const int Iterations = 6;

        /// <summary>
        /// The gameplay point (on the plane at <paramref name="planeHeight"/>) whose drawn ground the ray hits.
        /// A null <paramref name="surface"/> is flat ground.
        /// </summary>
        /// <returns>False when the ray runs parallel to the ground.</returns>
        public static bool TryIntersect(IGroundSurface surface, Ray ray, float planeHeight, out Vector3 point)
        {
            point = default;
            float drawnHeight = planeHeight;
            for (int i = 0; i < Iterations; i++)
            {
                var plane = new Plane(Vector3.up, new Vector3(0f, drawnHeight, 0f));
                if (!plane.Raycast(ray, out float distance))
                {
                    return false;
                }

                point = ray.GetPoint(distance);
                if (surface == null)
                {
                    break;
                }

                drawnHeight = planeHeight + surface.HeightAt(point.x, point.z);
            }

            point.y = planeHeight;
            return true;
        }

        /// <summary>
        /// The ray shifted so it hits gameplay colliders where their objects are drawn, taking the drawn
        /// height of the ground the ray meets. Objects stand on the ground they are drawn on, so this is exact
        /// at their feet and close enough over their height.
        /// </summary>
        public static Ray ToGameplaySpace(IGroundSurface surface, Ray ray, float planeHeight)
        {
            if (surface == null || !TryIntersect(surface, ray, planeHeight, out Vector3 point))
            {
                return ray;
            }

            return new Ray(ray.origin - Vector3.up * surface.HeightAt(point.x, point.z), ray.direction);
        }
    }
}
