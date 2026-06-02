using System;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBossMissileProjectile_V2 (Boss missile projectile)
 *
 * PURPOSE:
 * Kinematic boss missile projectile. It follows a high arcing curve toward the hero root transform; on impact it
 * applies boss missile damage with the same bunker-cover and hero damage rules as other mech boss weapon paths,
 * then Destroy(gameObject) on resolve or timeout.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own attack pattern cadence (MechRobotBossWeaponSystem_V2 spawns and configures each shot)
 * - Despawn or drive lifecycle of the boss composition root (MechRobotBossDeathHandler_V2)
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Self-contained projectile behaviour launched by the weapon system; keeps Update logic local to the missile object.
 */
    [DisallowMultipleComponent]
    public sealed class MechRobotBossMissileProjectile_V2 : MonoBehaviour
    {
        [SerializeField] private float _radius = 0.22f;
        [SerializeField] private bool _debugLogs;
        [SerializeField] private GameObject _explosionEffectPrefab;
        [SerializeField] private float _explosionEffectLifetime = 1.5f;
        [SerializeField] private bool _overrideExplosionSorting = true;
        [SerializeField] private string _explosionSortingLayerName = "MechRobot";
        [SerializeField] private int _explosionSortingOrder = 200;

        private int _damage;
        private float _speed;
        private float _spawnTime;
        private float _maxLifetime = 12f;
        private bool _respectBunkerCover;
        private Transform _heroFollow;
        private bool _didHit;
        private Vector2 _arcStartPosition;
        private float _arcProgress;
        private float _arcDurationSeconds = 1.15f;
        private float _arcHeightWorld = 3.2f;
        private Vector2 _targetWorldPoint;
        private bool _hasTargetWorldPoint;
        private bool _targetIsBunker;

        public void Launch(
            int damage,
            float speed,
            float maxLifetime,
            bool respectBunkerCover,
            Transform heroFollow,
            float arcDurationSeconds = 1.15f,
            float arcHeightWorld = 3.2f,
            Vector2 targetWorldPoint = default,
            bool targetIsBunker = false)
        {
            _damage = Mathf.Max(1, damage);
            _speed = Mathf.Max(0.5f, speed);
            _maxLifetime = Mathf.Max(0.5f, maxLifetime);
            _respectBunkerCover = respectBunkerCover;
            _heroFollow = heroFollow;
            _spawnTime = Time.time;
            _didHit = false;
            _arcStartPosition = transform.position;
            _arcProgress = 0f;
            _arcDurationSeconds = Mathf.Max(0.1f, arcDurationSeconds);
            _arcHeightWorld = Mathf.Max(0.1f, arcHeightWorld);
            _targetWorldPoint = targetWorldPoint;
            _hasTargetWorldPoint = targetIsBunker || targetWorldPoint.sqrMagnitude > 0.0001f;
            _targetIsBunker = targetIsBunker;

            if (TryGetComponent(out Rigidbody2D rb))
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            if (TryGetComponent(out CircleCollider2D c))
            {
                c.isTrigger = true;
                c.radius = _radius;
            }
        }

        private void Update()
        {
            if (_didHit)
            {
                return;
            }

            if (Time.time - _spawnTime > _maxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 pos = transform.position;
            if (HasTarget() && _arcProgress < 1f)
            {
                Vector2 nextPos = GetArcPosition(_arcProgress + Time.deltaTime / _arcDurationSeconds);
                Vector2 tangent = nextPos - pos;
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    MoveTo(nextPos, tangent);
                    TryApplyTargetArrivalDamage();
                }

                return;
            }

            Vector2 targetPoint = GetTargetWorldPoint();
            Vector2 dir = targetPoint - pos;
            if (dir.sqrMagnitude < 0.0001f)
            {
                TryApplyTargetArrivalDamage();
                return;
            }

            dir.Normalize();
            MoveTo(pos + dir * (_speed * Time.deltaTime), dir);
            TryApplyTargetArrivalDamage();
        }

        private Vector2 GetArcPosition(float progress)
        {
            _arcProgress = Mathf.Clamp01(progress);

            Vector2 end = GetTargetWorldPoint();
            Vector2 middle = (_arcStartPosition + end) * 0.5f;
            Vector2 control = middle + Vector2.up * _arcHeightWorld;
            float t = Mathf.SmoothStep(0f, 1f, _arcProgress);
            float inv = 1f - t;
            return inv * inv * _arcStartPosition + 2f * inv * t * control + t * t * end;
        }

        private void MoveTo(Vector2 position, Vector2 direction)
        {
            transform.position = position;
            float ang = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        private bool HasTarget()
        {
            return _hasTargetWorldPoint || _heroFollow != null;
        }

        private Vector2 GetTargetWorldPoint()
        {
            if (_hasTargetWorldPoint)
            {
                return _targetWorldPoint;
            }

            return _heroFollow != null ? (Vector2)_heroFollow.position : (Vector2)transform.position + (Vector2)transform.right;
        }

        private void TryApplyTargetArrivalDamage()
        {
            if (_didHit || !_targetIsBunker)
            {
                return;
            }

            Vector2 delta = _targetWorldPoint - (Vector2)transform.position;
            if (delta.sqrMagnitude > _radius * _radius)
            {
                return;
            }

            WaveManager_V2 waveManager = FindAnyObjectByType<WaveManager_V2>();
            if (waveManager != null && waveManager.BunkerHealth > 0)
            {
                waveManager.ApplyBunkerDamage(_damage);
            }

            CompleteImpact();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_didHit || other == null)
            {
                return;
            }

            if (TryApplyHit(other))
            {
                CompleteImpact();
            }
        }

        private bool TryApplyHit(Collider2D collider)
        {
            WaveManager_V2 waveManager = FindAnyObjectByType<WaveManager_V2>();
            Vector2 hitDir = transform.right;

            if (_respectBunkerCover && IsBunkerCoverHit(collider))
            {
                if (waveManager != null && waveManager.BunkerHealth > 0)
                {
                    waveManager.ApplyBunkerDamage(_damage);
                    return true;
                }

                return false;
            }

            Hero_V2 heroRoot = collider.GetComponentInParent<Hero_V2>();
            if (heroRoot != null)
            {
                if (waveManager != null && waveManager.IsHeroInsideBunker(heroRoot))
                {
                    return false;
                }

                heroRoot.ReceiveDamage(_damage, incomingShotWorldDirection: hitDir);
                return true;
            }

            HeroModel_V2 heroModelHit = collider.GetComponentInParent<HeroModel_V2>();
            if (heroModelHit != null)
            {
                Hero_V2 heroForZone = heroModelHit.GetComponentInParent<Hero_V2>();
                bool heroProtected = waveManager != null &&
                    (heroForZone != null ? waveManager.IsHeroInsideBunker(heroForZone) : waveManager.IsHeroInsideBunker());
                if (heroProtected)
                {
                    return false;
                }

                if (heroForZone != null)
                {
                    heroForZone.ReceiveDamage(_damage, incomingShotWorldDirection: hitDir);
                }
                else
                {
                    heroModelHit.TakeDamage(_damage);
                }

                return true;
            }

            if (_debugLogs)
            {
                Debug.Log($"[MechMissile] ignored trigger: {collider.name}");
            }

            return false;
        }

        private void CompleteImpact()
        {
            if (_didHit)
            {
                return;
            }

            _didHit = true;
            AudioManager_V2.PlayMissileExplosion();
            HitStop_V2.Request(HitStopKind_V2.LargeExplosion);
            SpawnExplosionEffect();
            Destroy(gameObject);
        }

        private void SpawnExplosionEffect()
        {
            if (_explosionEffectPrefab == null)
            {
                return;
            }

            GameObject effect = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
            ApplyExplosionSorting(effect);
            Destroy(effect, Mathf.Max(0.05f, _explosionEffectLifetime));
        }

        private void ApplyExplosionSorting(GameObject effect)
        {
            if (!_overrideExplosionSorting || effect == null)
            {
                return;
            }

            bool hasSortingLayer = TryResolveSortingLayerId(_explosionSortingLayerName, out int sortingLayerId);
            Renderer[] renderers = effect.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (hasSortingLayer)
                {
                    renderer.sortingLayerID = sortingLayerId;
                }

                renderer.sortingOrder = _explosionSortingOrder;
            }
        }

        private static bool TryResolveSortingLayerId(string layerName, out int id)
        {
            id = 0;
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return false;
            }

            SortingLayer[] layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == layerName)
                {
                    id = layers[i].id;
                    return true;
                }
            }

            return false;
        }

        private static bool IsBunkerCoverHit(Collider2D collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.GetComponentInParent<BunkerHitbox_V2>() != null)
            {
                return true;
            }

            Transform t = collider.transform;
            while (t != null)
            {
                string n = t.name;
                if (!string.IsNullOrWhiteSpace(n) &&
                    n.IndexOf("bunker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                t = t.parent;
            }

            return false;
        }
    }
}
