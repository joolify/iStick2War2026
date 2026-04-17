using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Optional trigger collider on bunker interior. When assigned or found, <see cref="WaveManager_V2"/>
    /// treats the hero inside as protected from enemy fire (HP damage blocked; bunker can still be hit).
    /// </summary>
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
