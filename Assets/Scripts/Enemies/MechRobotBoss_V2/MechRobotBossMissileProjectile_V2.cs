using System;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>Slow boss missile; homes on <see cref="Hero_V2"/> root and applies bunker/hero damage like mech hitscan.</summary>
    [DisallowMultipleComponent]
    public sealed class MechRobotBossMissileProjectile_V2 : MonoBehaviour
    {
        [SerializeField] private float _radius = 0.22f;
        [SerializeField] private bool _debugLogs;

        private int _damage;
        private float _speed;
        private float _spawnTime;
        private float _maxLifetime = 12f;
        private bool _respectBunkerCover;
        private Transform _heroFollow;
        private bool _didHit;

        public void Launch(
            int damage,
            float speed,
            float maxLifetime,
            bool respectBunkerCover,
            Transform heroFollow)
        {
            _damage = Mathf.Max(1, damage);
            _speed = Mathf.Max(0.5f, speed);
            _maxLifetime = Mathf.Max(0.5f, maxLifetime);
            _respectBunkerCover = respectBunkerCover;
            _heroFollow = heroFollow;
            _spawnTime = Time.time;
            _didHit = false;

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
            Vector2 dir;
            if (_heroFollow != null)
            {
                dir = ((Vector2)_heroFollow.position - pos);
            }
            else
            {
                dir = transform.right;
            }

            if (dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            dir.Normalize();
            transform.position = pos + dir * (_speed * Time.deltaTime);
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_didHit || other == null)
            {
                return;
            }

            if (TryApplyHit(other))
            {
                _didHit = true;
                Destroy(gameObject);
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
