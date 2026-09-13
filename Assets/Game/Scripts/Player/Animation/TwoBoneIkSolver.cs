using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>
    /// Standalone geometry helper retained for non-character uses. The player cutout rig deliberately
    /// uses authored symbol transforms instead, because presentation-time IK exposes sprite seams.
    /// </summary>
    public static class TwoBoneIkSolver
    {
        public static TwoBoneIkSolution Solve(Vector2 root, Vector2 target,
            float firstLength, float secondLength, float bendDirection)
        {
            firstLength = Mathf.Max(.0001f, firstLength);
            secondLength = Mathf.Max(.0001f, secondLength);
            Vector2 delta = target - root;
            float rawDistance = delta.magnitude;
            float minimumDistance = Mathf.Abs(firstLength - secondLength) + .0001f;
            float maximumDistance = firstLength + secondLength - .0001f;
            float distance = Mathf.Clamp(rawDistance, minimumDistance, maximumDistance);
            Vector2 direction = rawDistance > .0001f ? delta / rawDistance : Vector2.down;
            Vector2 reachableTarget = root + direction * distance;

            float baseAngle = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
            float shoulderOffset = Mathf.Acos(Mathf.Clamp(
                (firstLength * firstLength + distance * distance - secondLength * secondLength)
                / (2f * firstLength * distance), -1f, 1f)) * Mathf.Rad2Deg;
            float firstAngle = baseAngle + Mathf.Sign(Mathf.Approximately(bendDirection, 0f) ? 1f : bendDirection)
                * shoulderOffset;
            Vector2 joint = root + DownAt(firstAngle) * firstLength;
            Vector2 secondDelta = reachableTarget - joint;
            float secondAngle = Mathf.Atan2(secondDelta.x, -secondDelta.y) * Mathf.Rad2Deg;
            return new TwoBoneIkSolution(firstAngle, secondAngle, joint, reachableTarget);
        }

        private static Vector2 DownAt(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
        }
    }

    public readonly struct TwoBoneIkSolution
    {
        public TwoBoneIkSolution(float firstAngle, float secondAngle, Vector2 joint, Vector2 target)
        {
            FirstAngle = firstAngle;
            SecondAngle = secondAngle;
            Joint = joint;
            Target = target;
        }

        public float FirstAngle { get; }
        public float SecondAngle { get; }
        public Vector2 Joint { get; }
        public Vector2 Target { get; }
    }
}
