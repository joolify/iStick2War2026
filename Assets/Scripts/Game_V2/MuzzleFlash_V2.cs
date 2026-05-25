using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MuzzleFlash_V2 (Shared weapon muzzle VFX)
 *
 * PURPOSE:
 * Scene-level VFX service for short muzzle-flash prefab bursts. Weapon systems call the static Play
 * method only after a shot is committed (ammo/cooldown passed), while this component owns prefab,
 * rotation offset, lifetime, and optional random scale.
 *
 * ---------------------------------------------------------
 * INPUT SOURCES
 *
 * - HeroController_V2 for hero hitscan / projectile launch flashes.
 * - ParatrooperWeaponSystem_V2 for MP40 fire.
 * - MechRobotBossWeaponSystem_V2 for machine-gun / cannon fire.
 *
 * ---------------------------------------------------------
 * MUST NOT
 *
 * - Decide whether a weapon can shoot.
 * - Apply damage, recoil, shell casings, or audio.
 */
    public sealed class MuzzleFlash_V2 : MonoBehaviour
    {
        private static MuzzleFlash_V2 s_instance;
        private static bool s_warnedNoInstance;
        private static bool s_warnedNoPrefab;

        [Header("Prefab")]
        [SerializeField] private GameObject _muzzleFlashPrefab;
        [Tooltip(
            "Added to atan2(direction). Use 0 if prefab points along +X/right at rotation 0. " +
            "Use -90 if prefab art points up at rotation 0.")]
        [SerializeField] private float _prefabRotationOffsetDegrees;
        [SerializeField] private float _worldZ = 0f;
        [SerializeField] private float _lifetimeSeconds = 0.08f;
        [SerializeField] private Transform _optionalParent;

        [Header("Scale")]
        [SerializeField] private Vector2 _randomScaleRange = new Vector2(0.9f, 1.15f);

        [Header("Debug")]
        [SerializeField] private bool _logSpawn;

        public static void Play(Vector2 worldPosition, Vector2 direction)
        {
            if (s_instance == null)
            {
                if (!s_warnedNoInstance)
                {
                    s_warnedNoInstance = true;
                    Debug.LogWarning("[MuzzleFlash_V2] No MuzzleFlash_V2 instance in scene; muzzle flash ignored.");
                }

                return;
            }

            s_instance.Spawn(worldPosition, direction);
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Debug.LogWarning("[MuzzleFlash_V2] Multiple instances found; latest instance will receive static Play calls.");
            }

            s_instance = this;
            s_warnedNoInstance = false;
            s_warnedNoPrefab = false;
        }

        private void OnDisable()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Spawn(Vector2 worldPosition, Vector2 direction)
        {
            if (_muzzleFlashPrefab == null)
            {
                if (!s_warnedNoPrefab)
                {
                    s_warnedNoPrefab = true;
                    Debug.LogWarning("[MuzzleFlash_V2] Muzzle Flash Prefab is not assigned.");
                }

                return;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + _prefabRotationOffsetDegrees;
            Vector3 pos = new Vector3(worldPosition.x, worldPosition.y, _worldZ);
            GameObject go = Instantiate(
                _muzzleFlashPrefab,
                pos,
                Quaternion.Euler(0f, 0f, angle),
                _optionalParent);

            float minScale = Mathf.Min(_randomScaleRange.x, _randomScaleRange.y);
            float maxScale = Mathf.Max(_randomScaleRange.x, _randomScaleRange.y);
            float scale = Random.Range(Mathf.Max(0.01f, minScale), Mathf.Max(0.01f, maxScale));
            go.transform.localScale = go.transform.localScale * scale;

            float lifetime = Mathf.Max(0.01f, _lifetimeSeconds);
            Destroy(go, lifetime);

            if (_logSpawn)
            {
                Debug.Log($"[MuzzleFlash_V2] Spawn pos={pos}, angle={angle:0.##}, lifetime={lifetime:0.###}");
            }
        }

        [ContextMenu("Test Muzzle Flash Right")]
        private void TestMuzzleFlashRight()
        {
            Vector3 p = transform.position;
            Spawn(new Vector2(p.x, p.y), Vector2.right);
        }
    }
}
