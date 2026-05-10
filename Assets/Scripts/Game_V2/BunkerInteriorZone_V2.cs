using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BunkerInteriorZone_V2 (Hero safe-zone trigger)
 *
 * PURPOSE:
 * Optional trigger Collider2D describing bunker interior volume. WaveManager_V2 queries ContainsWorldPoint to suppress
 * hero HP damage from enemy fire while still allowing bunker colliders to receive hits.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Heal the hero or repair bunker (WaveManager shop + damage paths).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * Safe-zone queries → WaveManager_V2.cs | Hero damage entry → Hero_V2.cs (ReceiveDamage path, Hero_V2 folder)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Geometry-only helper; keeps safe-zone logic out of hero movement code.
 */
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class BunkerInteriorZone_V2 : MonoBehaviour
    {
        private Collider2D _collider2D;

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
        }

        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            return _collider2D != null && _collider2D.OverlapPoint(worldPoint);
        }
    }
}
