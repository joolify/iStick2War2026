using Assets.Scripts.Components;
using UnityEngine;

namespace iStick2War_V2
{
    public struct HeroShotContext_V2
    {
        public Vector2 Origin;
        public Vector2 Direction;
        public float Range;
        public LayerMask WhatToHit;
        public float BaseDamage;
        /// <summary>Hit-scan damage to aircraft (AircraftHealth_V2), per weapon.</summary>
        public float AircraftDamage;
        public bool DebugDrawShotRay;
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
        private const float FallbackHitRadius = 0.12f;
        private static readonly bool DebugShotLogs = false;

        public HeroShotResult_V2 ResolveShot(HeroShotContext_V2 context)
        {
            var normalizedDirection = context.Direction.normalized;
            var range = context.Range > 0f ? context.Range : 100f;

            // Keep this to avoid stale collider/bone positions before raycast checks.
            Physics2D.SyncTransforms();

            RaycastHit2D hit = Physics2D.Raycast(context.Origin, normalizedDirection, range, context.WhatToHit);
            bool usedFallbackCast = false;
            if (hit.collider == null)
            {
                // Small forgiving cast to reduce visual "through body" misses on thin/animated hitboxes.
                hit = Physics2D.CircleCast(context.Origin, FallbackHitRadius, normalizedDirection, range, context.WhatToHit);
                usedFallbackCast = hit.collider != null;
            }
            if (context.DebugDrawShotRay)
            {
                Debug.DrawRay(context.Origin, normalizedDirection * range, Color.green, 0.75f);
            }
            LogShot($"[HeroShotResolver_V2] Raycast origin={context.Origin}, dir={normalizedDirection}, range={range}, mask={context.WhatToHit.value}");

            if (hit.collider != null)
            {
                if (usedFallbackCast)
                {
                    LogShot($"[HeroShotResolver_V2] Hit by fallback CircleCast radius={FallbackHitRadius:0.###}, collider={hit.collider.name}");
                }
                else
                {
                    LogShot($"[HeroShotResolver_V2] Hit collider={hit.collider.name} layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                }
                ApplyDamage(hit, context);
            }
            else
            {
                LogShot("[HeroShotResolver_V2] Raycast miss.");
                RaycastHit2D unmaskedHit = Physics2D.Raycast(context.Origin, normalizedDirection, range);
                if (unmaskedHit.collider != null)
                {
                    LogShotWarning(
                        $"[HeroShotResolver_V2] Unmasked hit detected on layer '{LayerMask.LayerToName(unmaskedHit.collider.gameObject.layer)}' " +
                        $"(collider={unmaskedHit.collider.name}). Check layer assignment/mask for missed shot.");
                }
            }

            return new HeroShotResult_V2
            {
                DidHit = hit.collider != null,
                Hit = hit,
                FinalPos = hit.collider != null ? hit.point : context.Origin + normalizedDirection * range
            };
        }

        private static void ApplyDamage(RaycastHit2D hit, HeroShotContext_V2 context)
        {
            ParatrooperBodyPart_V2 bodyPart = hit.collider.GetComponent<ParatrooperBodyPart_V2>();
            if (bodyPart != null)
            {
                var damageInfo = new DamageInfo
                {
                    BaseDamage = context.BaseDamage,
                    HitPoint = hit.point,
                };

                try
                {
                    bodyPart.OnHit(damageInfo);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[HeroShotResolver_V2] ApplyDamage failed on collider '{hit.collider.name}': {ex.Message}");
                }

                return;
            }

            AircraftHealth_V2 aircraft =
                hit.collider.GetComponent<AircraftHealth_V2>() ??
                hit.collider.GetComponentInParent<AircraftHealth_V2>();
            if (aircraft != null)
            {
                float damage = context.AircraftDamage > 0f ? context.AircraftDamage : context.BaseDamage;
                aircraft.ApplyDamage(damage);
            }
        }

        private static void LogShot(string message)
        {
            if (DebugShotLogs)
            {
                Debug.Log(message);
            }
        }

        private static void LogShotWarning(string message)
        {
            if (DebugShotLogs)
            {
                Debug.LogWarning(message);
            }
        }
    }
}
