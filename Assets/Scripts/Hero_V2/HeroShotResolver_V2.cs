using Assets.Scripts.Components;
using iStick2War;
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
        public WeaponType WeaponType;
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
        private static float _nextFlamethrowerDebugLogAt;

        public HeroShotResult_V2 ResolveShot(HeroShotContext_V2 context)
        {
            var normalizedDirection = context.Direction.normalized;
            var range = context.Range > 0f ? context.Range : 100f;

            // Keep this to avoid stale collider/bone positions before raycast checks.
            Physics2D.SyncTransforms();

            RaycastHit2D hit = FindPrimaryHit(context, normalizedDirection, range, useCircleCast: false);
            bool usedFallbackCast = false;
            if (hit.collider == null)
            {
                // Small forgiving cast to reduce visual "through body" misses on thin/animated hitboxes.
                hit = FindPrimaryHit(context, normalizedDirection, range, useCircleCast: true);
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

            LogFlamethrowerShotTrace(context, normalizedDirection, range, hit);

            return new HeroShotResult_V2
            {
                DidHit = hit.collider != null,
                Hit = hit,
                FinalPos = hit.collider != null ? hit.point : context.Origin + normalizedDirection * range
            };
        }

        private static RaycastHit2D FindPrimaryHit(
            HeroShotContext_V2 context,
            Vector2 normalizedDirection,
            float range,
            bool useCircleCast)
        {
            RaycastHit2D[] hits = useCircleCast
                ? Physics2D.CircleCastAll(context.Origin, FallbackHitRadius, normalizedDirection, range, context.WhatToHit)
                : Physics2D.RaycastAll(context.Origin, normalizedDirection, range, context.WhatToHit);

            if (hits == null || hits.Length == 0)
            {
                return default;
            }

            RaycastHit2D bestHit = default;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D candidate = hits[i];
                if (!IsValidHitForContext(candidate, context))
                {
                    continue;
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    bestHit = candidate;
                }
            }

            return bestHit;
        }

        private static bool IsValidHitForContext(RaycastHit2D hit, HeroShotContext_V2 context)
        {
            if (hit.collider == null)
            {
                return false;
            }

            // Hero bullets should not be blocked by his own bunker / terrain helper colliders.
            // Ground dirt VFX is handled separately by HeroController_V2 after a gameplay miss.
            if (IsHeroIgnoredSurfaceCollider(hit.collider))
            {
                return false;
            }

            // Any hero hitscan weapon should pass through already-dead paratrooper hitboxes.
            ParatrooperBodyPart_V2 bodyPart = hit.collider.GetComponent<ParatrooperBodyPart_V2>();
            if (bodyPart != null)
            {
                if (!bodyPart.IsLivingCharacterForTargeting())
                {
                    return false;
                }

                if (!IsEnemyDamageTargetVisibleInCombatView(hit.collider))
                {
                    return false;
                }
            }

            MechRobotBossBodyPart_V2 mechPart = hit.collider.GetComponent<MechRobotBossBodyPart_V2>();
            if (mechPart != null)
            {
                if (!mechPart.IsLivingCharacterForTargeting())
                {
                    return false;
                }

                if (!IsEnemyDamageTargetVisibleInCombatView(hit.collider))
                {
                    return false;
                }
            }

            AircraftHealth_V2 aircraft =
                hit.collider.GetComponent<AircraftHealth_V2>() ??
                hit.collider.GetComponentInParent<AircraftHealth_V2>();
            if (aircraft != null && !IsEnemyDamageTargetVisibleInCombatView(hit.collider))
            {
                return false;
            }

            return true;
        }

        private static bool IsEnemyDamageTargetVisibleInCombatView(Collider2D collider)
        {
            return HeroCombatCameraReach_V2.IsDamageTargetVisibleInCombatView(Camera.main, collider);
        }

        private static bool IsHeroIgnoredSurfaceCollider(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.GetComponentInParent<BunkerHitbox_V2>() != null)
            {
                return true;
            }

            int layer = collider.gameObject.layer;
            int bunker = LayerMask.NameToLayer("Bunker");
            int ground = LayerMask.NameToLayer("Ground");
            return (bunker >= 0 && layer == bunker) ||
                   (ground >= 0 && layer == ground);
        }

        private static void ApplyDamage(RaycastHit2D hit, HeroShotContext_V2 context)
        {
            Vector2 shotDirection = context.Direction.sqrMagnitude > 0.0001f
                ? context.Direction.normalized
                : Vector2.right;

            float baseDamage = context.BaseDamage;
            float aircraftDamage = context.AircraftDamage > 0f ? context.AircraftDamage : context.BaseDamage;
            if (context.WeaponType == WeaponType.Ithaca)
            {
                float distanceMultiplier = HeroWeaponSystem_V2.GetIthacaPelletDamageMultiplierByDistance(
                    hit.distance,
                    context.Range);
                baseDamage *= distanceMultiplier;
                aircraftDamage *= distanceMultiplier;
            }

            ParatrooperBodyPart_V2 bodyPart = hit.collider.GetComponent<ParatrooperBodyPart_V2>();
            if (bodyPart != null)
            {
                bool ithacaCloseRangeGib = context.WeaponType == WeaponType.Ithaca &&
                    HeroWeaponSystem_V2.ShouldIthacaCauseExplosiveGib(hit.distance, context.Range);
                var damageInfo = new DamageInfo
                {
                    BaseDamage = baseDamage,
                    HitPoint = hit.point,
                    ShotDirection = shotDirection,
                    IsExplosive = ithacaCloseRangeGib,
                    ExplosionForce = ithacaCloseRangeGib
                        ? HeroWeaponSystem_V2.GetIthacaExplosionForceForHit(hit.distance, context.Range)
                        : 0f,
                    SourceWeapon = context.WeaponType,
                };

                try
                {
                    bodyPart.OnHit(damageInfo);
                    if (context.WeaponType == WeaponType.Flamethrower)
                    {
                        Debug.Log(
                            $"[HeroShotResolver_V2] Flamethrower damage applied to body part '{bodyPart.bodyPart}' " +
                            $"on collider '{hit.collider.name}' at {hit.point}.");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[HeroShotResolver_V2] ApplyDamage failed on collider '{hit.collider.name}': {ex.Message}");
                }

                return;
            }

            MechRobotBossBodyPart_V2 mechPart = hit.collider.GetComponent<MechRobotBossBodyPart_V2>();
            if (mechPart != null)
            {
                var damageInfo = new DamageInfo
                {
                    BaseDamage = baseDamage,
                    HitPoint = hit.point,
                    ShotDirection = shotDirection,
                    SourceWeapon = context.WeaponType,
                };

                try
                {
                    mechPart.OnHit(damageInfo);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[HeroShotResolver_V2] ApplyDamage failed on mech collider '{hit.collider.name}': {ex.Message}");
                }

                return;
            }

            AircraftHealth_V2 aircraft =
                hit.collider.GetComponent<AircraftHealth_V2>() ??
                hit.collider.GetComponentInParent<AircraftHealth_V2>();
            if (aircraft != null)
            {
                aircraft.ApplyDamage(aircraftDamage);
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

        private static void LogFlamethrowerShotTrace(
            HeroShotContext_V2 context,
            Vector2 normalizedDirection,
            float range,
            RaycastHit2D hit)
        {
            if (context.WeaponType != WeaponType.Flamethrower)
            {
                return;
            }

            if (Time.time < _nextFlamethrowerDebugLogAt)
            {
                return;
            }

            _nextFlamethrowerDebugLogAt = Time.time + 0.2f;
            string colliderName = hit.collider != null ? hit.collider.name : "none";
            string colliderLayer = hit.collider != null ? LayerMask.LayerToName(hit.collider.gameObject.layer) : "none";
            Debug.Log(
                $"[HeroShotResolver_V2] Flamethrower trace: origin={context.Origin}, dir={normalizedDirection}, " +
                $"range={range:0.##}, mask={context.WhatToHit.value}, hit={hit.collider != null}, " +
                $"collider={colliderName}, layer={colliderLayer}");
        }
    }
}
