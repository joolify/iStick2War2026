using UnityEngine;

namespace iStick2War_V2
{
    /*
 * EnemyLootCleanup_V2 (Ground loot despawn helper)
 *
 * PURPOSE:
 * Removes dropped mp40 / naziHelmet props when a wave ends or a life-loss retry needs a clean field.
 *
 * NAVIGATION: WaveManager_V2.CompleteWave, life retry, EnemySpawner_V2.ClearActiveWaveCombatForLifeRetry.
 */
    public static class EnemyLootCleanup_V2
    {
        public static void DespawnAllActiveGroundLoot()
        {
            EnemyLootSettle_V2[] loot = Object.FindObjectsByType<EnemyLootSettle_V2>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < loot.Length; i++)
            {
                EnemyLootSettle_V2 piece = loot[i];
                if (piece == null || !piece.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Object.Destroy(piece.gameObject);
            }
        }
    }
}
