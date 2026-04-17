using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Place on bunker cover (e.g. bunkerFront) with a <see cref="Collider2D"/> so enemy shots can hit the bunker first.
    /// Put these objects on the <b>Bunker</b> physics layer when possible so hero/paratrooper rigidbodies can exclude them
    /// from resting collisions while still receiving raycast hits for combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BunkerHitbox_V2 : MonoBehaviour
    {
    }
}
