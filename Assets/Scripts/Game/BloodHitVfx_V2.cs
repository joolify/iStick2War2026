using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Spawns a one-shot blood / hit-chunk prefab at a world point, rotated to match shot direction.
    /// Particles are pinned to the emitter (no added drift) so the splat stays on the hit location.
    /// </summary>
    public static class BloodHitVfx_V2
    {
        /// <summary>Draw on top of typical Spine / sprite layers so hits on the body are not fully occluded.</summary>
        private const int ForegroundSortOrder = 8000;

        /// <param name="shotTravelDirectionWorld">World-space direction the projectile traveled (into the target).</param>
        /// <param name="damageAmount">Scales instance size vs <paramref name="referenceDamage"/>.</param>
        /// <param name="towardShooterSurfaceBiasMeters">
        /// Nudge spawn opposite to shot (toward the gun) so the splat sits on the visible surface when hit points sit
        /// slightly inside / behind Spine vs collider.
        /// </param>
        public static void Spawn(
            GameObject prefab,
            Vector2 worldPosition,
            Vector2 shotTravelDirectionWorld,
            float damageAmount,
            float referenceDamage = 24f,
            float destroyAfterSeconds = 0f,
            float towardShooterSurfaceBiasMeters = 0.022f)
        {
            if (prefab == null)
            {
                return;
            }

            Vector2 d = shotTravelDirectionWorld.sqrMagnitude > 0.0001f
                ? shotTravelDirectionWorld.normalized
                : Vector2.right;

            Vector2 biasedWorld = worldPosition;
            if (towardShooterSurfaceBiasMeters > 0f)
            {
                biasedWorld += -d * towardShooterSurfaceBiasMeters;
            }

            Quaternion rot = Rotation2DAlignRightWith(d);
            Vector3 spawn3 = new Vector3(biasedWorld.x, biasedWorld.y, 0f);
            GameObject instance = Object.Instantiate(prefab, spawn3, rot);

            float refD = Mathf.Max(0.01f, referenceDamage);
            float mul = Mathf.Clamp(damageAmount / refD, 0.35f, 2.4f);
            instance.transform.localScale *= mul;

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            PrimeParticleSystemsForControlledPlay(systems);
            PinParticleSystemsToHitLocation(systems);

            BringParticleRenderersToForeground(instance);

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Play(true);
                }
            }

            float lifetime = destroyAfterSeconds > 0.05f
                ? destroyAfterSeconds
                : ComputeDestroyDelaySeconds(systems);
            Object.Destroy(instance, lifetime);
        }

        /// <summary>Cancels prefab Play On Awake emission so we can configure modules, then Play once.</summary>
        private static void PrimeParticleSystemsForControlledPlay(ParticleSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                main.playOnAwake = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>
        /// Removes motion so the burst stays anchored at the hit point (prefab may have speed / noise / forces).
        /// </summary>
        private static void PinParticleSystemsToHitLocation(ParticleSystem[] systems)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
                main.gravityModifier = 0f;

                ParticleSystem.VelocityOverLifetimeModule vol = ps.velocityOverLifetime;
                vol.enabled = false;

                ParticleSystem.ForceOverLifetimeModule fo = ps.forceOverLifetime;
                fo.enabled = false;

                ParticleSystem.InheritVelocityModule iv = ps.inheritVelocity;
                iv.enabled = false;

                ParticleSystem.NoiseModule noise = ps.noise;
                noise.enabled = false;
            }
        }

        private static float ComputeDestroyDelaySeconds(ParticleSystem[] systems, float minSeconds = 2.2f, float maxSeconds = 10f)
        {
            float need = minSeconds;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                float sim = Mathf.Max(0.01f, main.simulationSpeed);
                float startLife = Mathf.Max(
                    main.startLifetime.constant,
                    main.startLifetime.constantMax);
                float dur = main.duration;
                float t = (dur + startLife) / sim + 0.35f;
                if (t > need)
                {
                    need = t;
                }
            }

            return Mathf.Clamp(need, minSeconds, maxSeconds);
        }

        /// <summary>Aligns local +X with <paramref name="dir"/> in the XY plane (2D shooter convention).</summary>
        private static Quaternion Rotation2DAlignRightWith(Vector2 dir)
        {
            Vector2 n = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            Vector3 dv = new Vector3(n.x, n.y, 0f);
            if (Vector3.Dot(dv.normalized, Vector3.right) < -0.999f)
            {
                return Quaternion.Euler(0f, 0f, 180f);
            }

            return Quaternion.FromToRotation(Vector3.right, dv);
        }

        private static void BringParticleRenderersToForeground(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            int layerId = GetTopSortingLayerId();
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                {
                    continue;
                }

                r.sortingLayerID = layerId;
                r.sortingOrder = ForegroundSortOrder;
            }
        }

        private static int GetTopSortingLayerId()
        {
            SortingLayer[] layers = SortingLayer.layers;
            if (layers == null || layers.Length == 0)
            {
                return SortingLayer.NameToID("Default");
            }

            int topId = layers[0].id;
            int topValue = layers[0].value;
            for (int i = 1; i < layers.Length; i++)
            {
                if (layers[i].value > topValue)
                {
                    topValue = layers[i].value;
                    topId = layers[i].id;
                }
            }

            return topId;
        }
    }
}
