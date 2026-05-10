using UnityEngine;

namespace iStick2War_V2
{
    /*
 * AircraftHorizontalFlightControllerBase_V2
 *
 * Shared helpers for orthographic camera horizontal bounds and initial X direction toward bunker or sprite facing.
 */
    public abstract class AircraftHorizontalFlightControllerBase_V2 : MonoBehaviour
    {
        protected void DespawnSelfViaPool(GameObject self)
        {
            SimplePrefabPool_V2.Despawn(self);
        }

        protected bool TryGetOrthographicCameraHorizontalBounds(
            Camera cam,
            float marginWorld,
            out float leftBound,
            out float rightBound)
        {
            leftBound = 0f;
            rightBound = 0f;
            if (cam == null || !cam.orthographic)
            {
                return false;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float camX = cam.transform.position.x;
            float margin = Mathf.Max(0.5f, marginWorld);
            leftBound = camX - halfWidth - margin;
            rightBound = camX + halfWidth + margin;
            return true;
        }

        protected bool IsPastHorizontalFlyBounds(float worldX, float directionX, float leftBound, float rightBound)
        {
            return (directionX > 0f && worldX > rightBound) || (directionX < 0f && worldX < leftBound);
        }

        protected float ResolveInitialDirectionXTowardBunkerOrFacing(
            BunkerHitbox_V2 bunkerHitbox,
            Transform transform,
            bool spriteFacesRightWhenScaleXPositive)
        {
            if (bunkerHitbox != null)
            {
                float dx = bunkerHitbox.transform.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.05f)
                {
                    return Mathf.Sign(dx);
                }
            }

            bool positiveScaleMeansFacingRight = spriteFacesRightWhenScaleXPositive;
            bool facingRight = transform.lossyScale.x >= 0f
                ? positiveScaleMeansFacingRight
                : !positiveScaleMeansFacingRight;
            return facingRight ? 1f : -1f;
        }
    }
}
