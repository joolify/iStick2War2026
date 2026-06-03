using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Orthographic combat camera helpers: aim-ray reach (flamethrower clamp, shot range cap)
     * and visibility tests so off-screen enemies are not damaged by hero hit-scan.
     */
    internal static class HeroCombatCameraReach_V2
    {
        public const float DefaultFlamethrowerViewReachFraction = 0.7f;

        // Caps weapon range to the distance from origin to the combat view edge along the shot ray.
        public static float ClampShotRangeToCombatView(
            Camera cam,
            Vector2 origin,
            Vector2 direction,
            float weaponRange,
            float viewReachFraction = 1f)
        {
            weaponRange = weaponRange > 0f ? weaponRange : 100f;
            if (!TryGetReachPoint(cam, origin, direction, viewReachFraction, out _, out float viewReach))
            {
                return weaponRange;
            }

            return Mathf.Min(weaponRange, viewReach);
        }

        // True when collider bounds overlap the orthographic combat view (margin expands the rect).
        public static bool IsDamageTargetVisibleInCombatView(
            Camera cam,
            Collider2D collider,
            float orthographicMarginWorld = 0f)
        {
            if (collider == null)
            {
                return false;
            }

            if (cam == null || !cam.isActiveAndEnabled)
            {
                return true;
            }

            Bounds bounds = collider.bounds;
            if (cam.orthographic)
            {
                TryGetOrthographicViewRect(cam, orthographicMarginWorld, out float minX, out float maxX, out float minY, out float maxY);
                return bounds.max.x >= minX && bounds.min.x <= maxX &&
                       bounds.max.y >= minY && bounds.min.y <= maxY;
            }

            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        private static void TryGetOrthographicViewRect(
            Camera cam,
            float marginWorld,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;
            float margin = Mathf.Max(0f, marginWorld);
            minX = camPos.x - halfWidth - margin;
            maxX = camPos.x + halfWidth + margin;
            minY = camPos.y - halfHeight - margin;
            maxY = camPos.y + halfHeight + margin;
        }

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
