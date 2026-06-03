using UnityEngine;

namespace iStick2War_V2
{
    /*
 * CombatProjectileCleanup_V2 (Run-time projectile despawn helper)
 *
 * PURPOSE:
 * Removes active hero/enemy projectiles when a wave retry or life-loss reset needs a clean combat field.
 *
 * NAVIGATION: called from EnemySpawner_V2.ClearActiveWaveCombatForLifeRetry and WaveManager_V2 life retry.
 */
    public static class CombatProjectileCleanup_V2
    {
        public static void DespawnAllActiveProjectiles()
        {
            DespawnAll<HeroRocketProjectile_V2>();
            DespawnAll<PotatomasherProjectile_V2>();
            DespawnAll<BombProjectile_V2>();
            DespawnAll<MechRobotBossMissileProjectile_V2>();
        }

        private static void DespawnAll<T>() where T : MonoBehaviour
        {
            T[] projectiles = Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                T projectile = projectiles[i];
                if (projectile == null || !projectile.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Object.Destroy(projectile.gameObject);
            }
        }
    }
}
