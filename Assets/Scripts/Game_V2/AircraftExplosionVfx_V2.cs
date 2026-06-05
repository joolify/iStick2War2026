using UnityEngine;

namespace iStick2War_V2
{
    /*
     * Shared aircraft explosion VFX: same typed prefabs / fallback as HeroRocketProjectile_V2 bazooka blasts.
     * AircraftHealth_V2 uses this on fatal hit-scan damage; rockets suppress here and spawn at detonation center.
     */
    public enum AircraftExplosionVfxKind_V2
    {
        BombPlane,
        KamikazeDrone,
        BombDrone,
        HelicopterOrGenericAircraft
    }

    public static class AircraftExplosionVfx_V2
    {
        private static AircraftExplosionVfxDefaults_V2 _cachedDefaults;

        public static AircraftExplosionVfxKind_V2 Classify(AircraftHealth_V2 aircraft)
        {
            if (aircraft == null)
            {
                return AircraftExplosionVfxKind_V2.HelicopterOrGenericAircraft;
            }

            if (aircraft.GetComponentInParent<KamikazeDroneDriver_V2>() != null)
            {
                return AircraftExplosionVfxKind_V2.KamikazeDrone;
            }

            if (aircraft.GetComponentInParent<BombDrone_V2>() != null)
            {
                return AircraftExplosionVfxKind_V2.BombDrone;
            }

            if (aircraft.GetComponentInParent<Bombplane_V2>() != null)
            {
                return AircraftExplosionVfxKind_V2.BombPlane;
            }

            return AircraftExplosionVfxKind_V2.HelicopterOrGenericAircraft;
        }

        public static GameObject ResolvePrefab(AircraftExplosionVfxKind_V2 kind, AircraftExplosionVfxDefaults_V2 defaults)
        {
            if (defaults == null)
            {
                return null;
            }

            GameObject typed = null;
            switch (kind)
            {
                case AircraftExplosionVfxKind_V2.BombPlane:
                    typed = defaults.bombPlane;
                    break;
                case AircraftExplosionVfxKind_V2.KamikazeDrone:
                    typed = defaults.kamikazeDrone;
                    break;
                case AircraftExplosionVfxKind_V2.BombDrone:
                    typed = defaults.bombDrone;
                    break;
                case AircraftExplosionVfxKind_V2.HelicopterOrGenericAircraft:
                    typed = defaults.helicopterOrGenericAircraft;
                    break;
            }

            return typed != null ? typed : defaults.genericFallback;
        }

        // Aircraft roots often sit far from Spine/colliders (e.g. Fa_223 view +11m on X); use hitbox center, not root pivot.
        public static Vector3 ResolveDeathWorldPoint(AircraftHealth_V2 aircraft, Vector3? preferredWorldPoint = null)
        {
            if (preferredWorldPoint.HasValue)
            {
                return preferredWorldPoint.Value;
            }

            if (aircraft == null)
            {
                return Vector3.zero;
            }

            Bounds? merged = null;
            Collider2D[] colliders = aircraft.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D col = colliders[i];
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!merged.HasValue)
                {
                    merged = col.bounds;
                }
                else
                {
                    Bounds combined = merged.Value;
                    combined.Encapsulate(col.bounds);
                    merged = combined;
                }
            }

            if (merged.HasValue)
            {
                return merged.Value.center;
            }

            return aircraft.transform.position;
        }

        public static bool TrySpawnForAircraftDeath(AircraftHealth_V2 aircraft, Vector3? worldPointOverride = null)
        {
            if (aircraft == null)
            {
                return false;
            }

            AircraftExplosionVfxDefaults_V2 defaults = LoadDefaults();
            if (defaults == null)
            {
                return false;
            }

            GameObject prefab = ResolvePrefab(Classify(aircraft), defaults);
            if (prefab == null)
            {
                return false;
            }

            Vector3 spawnPoint = ResolveDeathWorldPoint(aircraft, worldPointOverride);
            SpawnPooled(prefab, spawnPoint, defaults.effectLifetime);
            return true;
        }

        public static bool TrySpawnKind(Vector3 worldPosition, AircraftExplosionVfxKind_V2 kind)
        {
            AircraftExplosionVfxDefaults_V2 defaults = LoadDefaults();
            if (defaults == null)
            {
                return false;
            }

            GameObject prefab = ResolvePrefab(kind, defaults);
            if (prefab == null)
            {
                return false;
            }

            SpawnPooled(prefab, worldPosition, defaults.effectLifetime);
            return true;
        }

        public static AircraftExplosionVfxDefaults_V2 LoadDefaults()
        {
            if (_cachedDefaults != null)
            {
                return _cachedDefaults;
            }

            _cachedDefaults = Resources.Load<AircraftExplosionVfxDefaults_V2>(AircraftExplosionVfxDefaults_V2.ResourcesPath);
            return _cachedDefaults;
        }

        private static void SpawnPooled(GameObject prefab, Vector3 worldPosition, float lifetimeSeconds)
        {
            GameObject fx = SimplePrefabPool_V2.Spawn(prefab, worldPosition, Quaternion.identity);
            if (fx == null)
            {
                return;
            }

            PooledAutoDespawn_V2 timer = fx.GetComponent<PooledAutoDespawn_V2>();
            if (timer == null)
            {
                timer = fx.AddComponent<PooledAutoDespawn_V2>();
            }

            timer.Arm(Mathf.Max(0.05f, lifetimeSeconds));
        }
    }
}
