using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Place on bunker cover (e.g. bunkerFront) with a <see cref="Collider2D"/> so enemy shots can hit the bunker first.
    /// Layer should be included in ParatrooperWeaponSystem_V2 bunker shot mask.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BunkerHitbox_V2 : MonoBehaviour
    {
    }
}
