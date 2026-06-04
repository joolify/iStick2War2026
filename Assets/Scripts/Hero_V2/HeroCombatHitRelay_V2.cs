using Assets.Scripts.Components;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * HeroCombatHitRelay_V2
 *
 * Shared entry for enemy weapons that raycast or collide with the hero. Prefers HeroBodyPart_V2 (Spine
 * bounding boxes) and falls back to Hero_V2 / HeroModel_V2 when only a legacy collider exists.
 */
    public static class HeroCombatHitRelay_V2
    {
        public static bool TryApplyRaycastWeaponHit(
            RaycastHit2D hit,
            int baseDamage,
            Vector2 shotDirection,
            WaveManager_V2 waveManager,
            Hero_V2 heroRootFallback,
            bool debugLogs,
            out bool appliedDamage)
        {
            appliedDamage = false;
            if (hit.collider == null)
            {
                return false;
            }

            return TryApplyColliderWeaponHit(
                hit.collider,
                hit.point,
                baseDamage,
                shotDirection,
                waveManager,
                heroRootFallback,
                debugLogs,
                out appliedDamage);
        }

        public static bool TryApplyColliderWeaponHit(
            Collider2D collider,
            Vector2 hitPoint,
            int baseDamage,
            Vector2 shotDirection,
            WaveManager_V2 waveManager,
            Hero_V2 heroRootFallback,
            bool debugLogs,
            out bool appliedDamage)
        {
            appliedDamage = false;
            if (collider == null)
            {
                return false;
            }

            HeroBodyPart_V2 bodyPart = collider.GetComponent<HeroBodyPart_V2>();
            Hero_V2 heroRoot = collider.GetComponentInParent<Hero_V2>();
            HeroModel_V2 heroModel = collider.GetComponentInParent<HeroModel_V2>();

            if (bodyPart == null && heroRoot == null && heroModel == null)
            {
                return false;
            }

            if (heroRoot == null && heroModel != null)
            {
                heroRoot = heroModel.GetComponentInParent<Hero_V2>();
            }

            if (heroRoot == null)
            {
                heroRoot = heroRootFallback;
            }

            if (waveManager != null && heroRoot != null && waveManager.IsHeroInsideBunker(heroRoot))
            {
                if (debugLogs)
                {
                    Debug.Log("[HeroCombatHitRelay_V2] Hero inside bunker — no HP damage.");
                }

                return true;
            }

            if (bodyPart != null)
            {
                DamageInfo info = new DamageInfo
                {
                    BaseDamage = baseDamage,
                    HitPoint = hitPoint,
                    ShotDirection = shotDirection,
                    SourceWeapon = WeaponType.MP40,
                };
                bodyPart.OnHit(info);
                appliedDamage = true;
                return true;
            }

            if (heroRoot != null)
            {
                if (debugLogs)
                {
                    Debug.Log($"[HeroCombatHitRelay_V2] Hit Hero_V2 for {baseDamage} damage.");
                }

                heroRoot.ReceiveDamage(baseDamage, incomingShotWorldDirection: shotDirection);
                appliedDamage = true;
                return true;
            }

            if (heroModel != null)
            {
                if (debugLogs)
                {
                    Debug.Log($"[HeroCombatHitRelay_V2] Hit HeroModel_V2 for {baseDamage} damage.");
                }

                if (heroRootFallback != null)
                {
                    heroRootFallback.ReceiveDamage(baseDamage, incomingShotWorldDirection: shotDirection);
                }
                else
                {
                    heroModel.TakeDamage(baseDamage);
                }

                appliedDamage = true;
                return true;
            }

            return false;
        }
    }
}
