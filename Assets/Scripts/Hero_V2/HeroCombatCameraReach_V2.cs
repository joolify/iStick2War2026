using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Orthographic camera reach along an aim ray (used by flamethrower range + aim clamp).
     */
    internal static class HeroCombatCameraReach_V2
    {
        public const float DefaultFlamethrowerViewReachFraction = 0.7f;

        public static bool TryGetReachPoint(
            Camera cam,
            Vector2 origin,
            Vector2 direction,
            float viewReachFraction,
            out Vector2 reachPoint,
            out float reachDistance)
        {
            reachPoint = origin;
            reachDistance = 0f;

            if (cam == null || !cam.orthographic)
            {
                return false;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
            if (dir == Vector2.zero)
            {
                return false;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            float minX = camPos.x - halfWidth;
            float maxX = camPos.x + halfWidth;
            float minY = camPos.y - halfHeight;
            float maxY = camPos.y + halfHeight;

            const float epsilon = 0.0001f;
            float bestT = float.PositiveInfinity;
            bool found = false;

            if (Mathf.Abs(dir.x) > epsilon)
            {
                float tx = ((dir.x > 0f ? maxX : minX) - origin.x) / dir.x;
                if (tx > epsilon)
                {
                    float yAtTx = origin.y + dir.y * tx;
                    if (yAtTx >= minY - epsilon && yAtTx <= maxY + epsilon)
                    {
                        bestT = tx;
                        found = true;
                    }
                }
            }

            if (Mathf.Abs(dir.y) > epsilon)
            {
                float ty = ((dir.y > 0f ? maxY : minY) - origin.y) / dir.y;
                if (ty > epsilon)
                {
                    float xAtTy = origin.x + dir.x * ty;
                    if (xAtTy >= minX - epsilon && xAtTy <= maxX + epsilon && ty < bestT)
                    {
                        bestT = ty;
                        found = true;
                    }
                }
            }

            if (!found || float.IsInfinity(bestT))
            {
                return false;
            }

            float fraction = Mathf.Clamp01(viewReachFraction);
            reachDistance = bestT * fraction;
            reachPoint = origin + dir * reachDistance;
            return true;
        }
    }
}
