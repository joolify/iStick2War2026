using Assets.Scripts.Components;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    public struct HeroShotContext_V2
    {
        public Vector2 Origin;
        public Vector2 Direction;
        public float Range;
        public LayerMask WhatToHit;
        public float BaseDamage;
    }

    public struct HeroShotResult_V2
    {
        public bool DidHit;
        public RaycastHit2D Hit;
        public Vector2 FinalPos;
    }

    /// <summary>
    /// Centralized hit-scan resolution for Hero_V2.
    /// Extracted from legacy GunBase.StartShoot() so combat logic lives in the V2 domain.
    /// </summary>
    public sealed class HeroShotResolver_V2
    {
        public HeroShotResult_V2 ResolveShot(HeroShotContext_V2 context)
        {
            var normalizedDirection = context.Direction.normalized;
            var range = context.Range > 0f ? context.Range : 100f;

            // Keep this to avoid stale collider/bone positions before raycast checks.
            Physics2D.SyncTransforms();

            RaycastHit2D hit = Physics2D.Raycast(context.Origin, normalizedDirection, range, context.WhatToHit);
            // Optional debug ray
            //Debug.DrawRay(context.Origin, normalizedDirection * range, Color.green, 1f);
            Debug.Log($"[HeroShotResolver_V2] Raycast origin={context.Origin}, dir={normalizedDirection}, range={range}, mask={context.WhatToHit.value}");

            if (hit.collider != null)
            {
                Debug.Log($"[HeroShotResolver_V2] Hit collider={hit.collider.name} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                ApplyDamage(hit, context.BaseDamage);
            }
            else
            {
                Debug.Log("[HeroShotResolver_V2] Raycast miss.");
            }

            return new HeroShotResult_V2
            {
                DidHit = hit.collider != null,
                Hit = hit,
                FinalPos = hit.collider != null ? hit.point : context.Origin + normalizedDirection * range
            };
        }

        private static void ApplyDamage(RaycastHit2D hit, float baseDamage)
        {
            var bodyPart = hit.collider.GetComponent<ParatrooperBodyPart_V2>();
            if (bodyPart == null)
            {
                return;
            }

            var damageInfo = new DamageInfo
            {
                BaseDamage = baseDamage,
                HitPoint = hit.point,
            };

            bodyPart.OnHit(damageInfo);
        }
    }
}
