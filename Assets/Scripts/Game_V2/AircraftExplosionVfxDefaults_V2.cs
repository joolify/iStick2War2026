using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Typed aircraft death / bazooka-blast VFX prefabs loaded from Resources (build-safe).
     * Matches HeroRocketProjectile_V2 explosion prefab selection when typed slots are unset.
     */
    [CreateAssetMenu(
        fileName = "AircraftExplosionVfxDefaults_V2",
        menuName = "iStick2War/Aircraft Explosion VFX Defaults V2")]
    public sealed class AircraftExplosionVfxDefaults_V2 : ScriptableObject
    {
        public const string ResourcesPath = "iStick2War/AircraftExplosionVfxDefaults_V2";

        [Header("Typed aircraft (optional; null falls back to genericFallback)")]
        public GameObject bombPlane;
        public GameObject kamikazeDrone;
        public GameObject bombDrone;
        [Tooltip("Helicopter and other AircraftHealth_V2 not matched above.")]
        public GameObject helicopterOrGenericAircraft;

        [Header("Fallback")]
        [Tooltip("Used when no typed prefab is set for the damaged aircraft family (bazooka _explosionEffectPrefab).")]
        public GameObject genericFallback;

        public float effectLifetime = 1.5f;
    }
}
