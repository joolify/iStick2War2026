using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BunkerHitbox_V2 (Bunker cover collider marker)
 *
 * PURPOSE:
 * Marker MonoBehaviour on bunker cover colliders (e.g. bunkerFront) so enemy weapons and kamikaze drones can resolve
 * bunker geometry. Prefer Bunker physics layer so hero/paratrooper rigidbodies can ignore resting collisions while rays still hit.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Apply bunker HP damage itself (WaveManager_V2.ApplyBunkerDamage and weapon paths own numbers).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2 + enemies)
 *
 * Bunker damage numbers → WaveManager_V2.cs | Aim / approach consumers → EnemySpawner_V2.cs, KamikazeDroneDriver_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Zero-logic tag component; EnemySpawner_V2 and KamikazeDroneDriver_V2 locate it via FindAnyObjectByType for aim cues.
 */
    [DisallowMultipleComponent]
    public sealed class BunkerHitbox_V2 : MonoBehaviour
    {
    }
}
