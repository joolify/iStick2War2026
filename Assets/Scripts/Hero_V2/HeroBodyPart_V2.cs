using Assets.Scripts.Components;
using iStick2War;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * HeroBodyPart_V2 (Hitbox + hit presentation hooks)
 *
 * PURPOSE:
 * One Spine bounding-box collider on the hero (same Infantry skeleton as paratrooper). Forwards hits to
 * Hero_V2.ReceiveDamage and optional lightweight bone hit-reaction on the hero SkeletonAnimation.
 *
 * ---------------------------------------------------------
 * FLOW
 *
 * Enemy raycast / collision → HeroBodyPart_V2.OnHit → Hero_V2.ReceiveDamage
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Own final HP rules (HeroDamageReceiver_V2 / HeroModel_V2)
 * - Play death animations or weapon logic
 */
    public class HeroBodyPart_V2 : MonoBehaviour
    {
        public BodyPartType bodyPart;

        [Header("Spine hit reaction")]
        [SerializeField] private SkeletonDataAsset _skeletonDataAsset;
        [SpineBone(dataField: nameof(_skeletonDataAsset))]
        [SerializeField] private string _targetBoneName;
        [SerializeField] private float _impulseScale = 0.025f;
        [SerializeField] private float _minImpulse = 0.1f;
        [SerializeField] private float _maxImpulse = 0.34f;
        [SerializeField] private float _maxOffset = 1.2f;
        [SerializeField] private float _instantKickFactor = 0.09f;
        [SerializeField] private float _maxVelocityFromOffset = 4.1f;
        [SerializeField] private float _springStrength = 90f;
        [SerializeField] private float _damping = 16f;
        [SerializeField] private bool _debugHitReaction = false;
        [SerializeField] private bool _freezeBoundingBoxFollowerAtStartup = true;
        [SerializeField] private bool _enableColliderWatchdog = true;
        [SerializeField] private float _watchdogIntervalSeconds = 1f;

        private Hero_V2 _heroRoot;
        private HeroModel_V2 _model;
        private SkeletonAnimation _skeletonAnimation;
        private BoundingBoxFollower _boundingBoxFollower;
        private Bone _targetBone;

        private Vector2 _boneOffset;
        private Vector2 _boneVelocity;
        private Vector2 _lastAppliedOffset;
        private bool _isSubscribedToUpdateComplete;
        private float _nextWatchdogTime;
        private bool _watchdogMissingLogged;

        void Awake()
        {
            _heroRoot = GetComponentInParent<Hero_V2>();
            _model = GetComponentInParent<HeroModel_V2>();
            EnsureSkeletonAnimationReference();
            SyncSkeletonDataAsset();
            StabilizeBoundingBoxFollowerCollider();

            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                gameObject.layer = playerLayer;
            }

            if (GetComponent<Collider2D>() == null)
            {
                Debug.LogWarning($"[HeroBodyPart_V2] No Collider2D on '{gameObject.name}'. Raycasts cannot hit this part.");
            }
        }

        public bool IsLivingCharacterForTargeting()
        {
            if (_model == null)
            {
                _model = GetComponentInParent<HeroModel_V2>();
            }

            return _model != null && !_model.isDead;
        }

        private void OnEnable()
        {
            EnsureSkeletonAnimationReference();
            SubscribeToSkeletonUpdateComplete();
            ResolveTargetBone();
        }

        private void OnDisable()
        {
            RestoreLastAppliedOffset();
            UnsubscribeFromSkeletonUpdateComplete();
        }

        void Start()
        {
            if (_heroRoot == null)
            {
                _heroRoot = GetComponentInParent<Hero_V2>();
            }

            ResolveTargetBone();
        }

        private void OnValidate()
        {
            EnsureSkeletonAnimationReference();
            SyncSkeletonDataAsset();
        }

        void Update()
        {
            if (_enableColliderWatchdog && Time.time >= _nextWatchdogTime)
            {
                _nextWatchdogTime = Time.time + Mathf.Max(0.1f, _watchdogIntervalSeconds);
                RunColliderWatchdog();
            }
        }

        public void OnHit(DamageInfo info)
        {
            info.BodyPart = bodyPart;
            AddHitImpulse(info);

            if (_heroRoot == null)
            {
                _heroRoot = GetComponentInParent<Hero_V2>();
            }

            if (_heroRoot == null)
            {
                Debug.LogWarning($"[HeroBodyPart_V2] OnHit on '{name}' but no {nameof(Hero_V2)} parent.");
                return;
            }

            int damage = Mathf.Max(1, Mathf.RoundToInt(info.BaseDamage));
            Vector2? shotDir = info.ShotDirection.sqrMagnitude > 0.0001f ? info.ShotDirection : (Vector2?)null;
            _heroRoot.ReceiveDamage(damage, incomingShotWorldDirection: shotDir);
        }

        /// <summary>Collider used for enemy aim (torso preferred, else any enabled part).</summary>
        public static Collider2D ResolvePrimaryCombatCollider(Hero_V2 hero)
        {
            if (hero == null)
            {
                return null;
            }

            HeroBodyPart_V2[] parts = hero.GetComponentsInChildren<HeroBodyPart_V2>(true);
            Collider2D torsoCollider = null;
            Collider2D anyCollider = null;

            for (int i = 0; i < parts.Length; i++)
            {
                HeroBodyPart_V2 part = parts[i];
                if (part == null)
                {
                    continue;
                }

                Collider2D col = part.GetComponent<Collider2D>();
                if (col == null || !col.enabled)
                {
                    continue;
                }

                if (part.bodyPart == BodyPartType.Torso)
                {
                    torsoCollider = col;
                }

                if (anyCollider == null)
                {
                    anyCollider = col;
                }
            }

            if (torsoCollider != null)
            {
                return torsoCollider;
            }

            if (anyCollider != null)
            {
                return anyCollider;
            }

            return hero.GetComponent<Collider2D>();
        }

        private void HandleSkeletonUpdateComplete(ISkeletonAnimation _)
        {
            if (_targetBone == null)
            {
                return;
            }

            RestoreLastAppliedOffset();

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _boneVelocity += (-_springStrength * _boneOffset - _damping * _boneVelocity) * dt;
            _boneOffset += _boneVelocity * dt;

            if (_boneOffset.sqrMagnitude > _maxOffset * _maxOffset)
            {
                _boneOffset = _boneOffset.normalized * _maxOffset;
                _boneVelocity *= 0.5f;
            }

            if (_boneOffset.sqrMagnitude < 0.000001f && _boneVelocity.sqrMagnitude < 0.000001f)
            {
                _boneOffset = Vector2.zero;
                _boneVelocity = Vector2.zero;
                return;
            }

            _targetBone.X += _boneOffset.x;
            _targetBone.Y += _boneOffset.y;
            _lastAppliedOffset = _boneOffset;
        }

        private void ResolveTargetBone()
        {
            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_targetBoneName))
            {
                _targetBoneName = gameObject.name;
            }

            _targetBone = _skeletonAnimation.Skeleton.FindBone(_targetBoneName);
            if (_targetBone == null)
            {
                Debug.LogWarning($"[HeroBodyPart_V2] Bone '{_targetBoneName}' not found for '{gameObject.name}'.");
            }
        }

        private void SyncSkeletonDataAsset()
        {
            if (_skeletonDataAsset != null)
            {
                return;
            }

            EnsureSkeletonAnimationReference();
            if (_skeletonAnimation != null)
            {
                _skeletonDataAsset = _skeletonAnimation.skeletonDataAsset;
            }
        }

        private void AddHitImpulse(DamageInfo info)
        {
            if (_targetBone == null || _skeletonAnimation == null)
            {
                EnsureSkeletonAnimationReference();
                ResolveTargetBone();
            }

            if (_targetBone == null || _skeletonAnimation == null)
            {
                return;
            }

            Vector3 boneWorld = _skeletonAnimation.transform.TransformPoint(new Vector3(_targetBone.WorldX, _targetBone.WorldY, 0f));
            Vector2 awayFromHit = (Vector2)boneWorld - info.HitPoint;
            if (awayFromHit.sqrMagnitude < 0.0001f)
            {
                awayFromHit = Vector2.right;
            }

            awayFromHit.Normalize();
            Vector3 localDirection3 = _skeletonAnimation.transform.InverseTransformVector(new Vector3(awayFromHit.x, awayFromHit.y, 0f));
            Vector2 localDirection = new Vector2(localDirection3.x, localDirection3.y).normalized;

            float bodyPartMultiplier = GetReactionMultiplier(info.BodyPart);
            float bodyPartMaxOffsetMultiplier = GetMaxOffsetMultiplier(info.BodyPart);
            float bodyPartMaxOffset = _maxOffset * bodyPartMaxOffsetMultiplier;
            float rawImpulse = info.BaseDamage * _impulseScale * bodyPartMultiplier;
            float impulse = Mathf.Clamp(rawImpulse, _minImpulse, _maxImpulse);

            _boneOffset += localDirection * (impulse * _instantKickFactor);
            _boneVelocity += localDirection * impulse;

            if (_boneOffset.sqrMagnitude > bodyPartMaxOffset * bodyPartMaxOffset)
            {
                _boneOffset = _boneOffset.normalized * bodyPartMaxOffset;
            }

            float maxVelocity = bodyPartMaxOffset * Mathf.Max(1f, _maxVelocityFromOffset);
            if (_boneVelocity.sqrMagnitude > maxVelocity * maxVelocity)
            {
                _boneVelocity = _boneVelocity.normalized * maxVelocity;
            }
        }

        private void EnsureSkeletonAnimationReference()
        {
            if (_skeletonAnimation != null)
            {
                return;
            }

            _skeletonAnimation = GetComponentInParent<SkeletonAnimation>();
            if (_skeletonAnimation != null)
            {
                return;
            }

            Hero_V2 hero = GetComponentInParent<Hero_V2>();
            if (hero != null)
            {
                _skeletonAnimation = hero.GetComponentInChildren<SkeletonAnimation>(true);
            }
        }

        internal void StabilizeBoundingBoxFollowerCollider()
        {
            if (!_freezeBoundingBoxFollowerAtStartup)
            {
                return;
            }

            _boundingBoxFollower = GetComponent<BoundingBoxFollower>();
            if (_boundingBoxFollower == null)
            {
                return;
            }

            _boundingBoxFollower.Initialize(true);
            _boundingBoxFollower.clearStateOnDisable = false;

            PolygonCollider2D[] polygonColliders = GetComponents<PolygonCollider2D>();
            for (int i = 0; i < polygonColliders.Length; i++)
            {
                if (polygonColliders[i] != null)
                {
                    polygonColliders[i].enabled = true;
                }
            }

            _boundingBoxFollower.enabled = false;
        }

        private void RunColliderWatchdog()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            bool anyEnabledCollider = false;
            PolygonCollider2D[] polygonColliders = GetComponents<PolygonCollider2D>();
            for (int i = 0; i < polygonColliders.Length; i++)
            {
                if (polygonColliders[i] != null && polygonColliders[i].enabled)
                {
                    anyEnabledCollider = true;
                    break;
                }
            }

            if (anyEnabledCollider)
            {
                _watchdogMissingLogged = false;
                return;
            }

            if (!_watchdogMissingLogged)
            {
                _watchdogMissingLogged = true;
                Debug.LogWarning($"[HeroBodyPart_V2] Collider watchdog: '{name}' has no enabled PolygonCollider2D. Trying recovery.");
            }

            TryRecoverColliderFromBoundingBoxFollower();
        }

        private void TryRecoverColliderFromBoundingBoxFollower()
        {
            if (_boundingBoxFollower == null)
            {
                _boundingBoxFollower = GetComponent<BoundingBoxFollower>();
            }

            if (_boundingBoxFollower == null)
            {
                return;
            }

            bool wasEnabled = _boundingBoxFollower.enabled;
            _boundingBoxFollower.enabled = true;
            _boundingBoxFollower.Initialize(true);

            PolygonCollider2D[] polygonColliders = GetComponents<PolygonCollider2D>();
            for (int i = 0; i < polygonColliders.Length; i++)
            {
                if (polygonColliders[i] != null)
                {
                    polygonColliders[i].enabled = true;
                }
            }

            if (_freezeBoundingBoxFollowerAtStartup)
            {
                _boundingBoxFollower.clearStateOnDisable = false;
                _boundingBoxFollower.enabled = false;
            }
            else
            {
                _boundingBoxFollower.enabled = wasEnabled;
            }
        }

        private void SubscribeToSkeletonUpdateComplete()
        {
            if (_skeletonAnimation == null || _isSubscribedToUpdateComplete)
            {
                return;
            }

            _skeletonAnimation.UpdateComplete -= HandleSkeletonUpdateComplete;
            _skeletonAnimation.UpdateComplete += HandleSkeletonUpdateComplete;
            _isSubscribedToUpdateComplete = true;
        }

        private void UnsubscribeFromSkeletonUpdateComplete()
        {
            if (_skeletonAnimation == null || !_isSubscribedToUpdateComplete)
            {
                return;
            }

            _skeletonAnimation.UpdateComplete -= HandleSkeletonUpdateComplete;
            _isSubscribedToUpdateComplete = false;
        }

        private void RestoreLastAppliedOffset()
        {
            if (_targetBone == null)
            {
                _lastAppliedOffset = Vector2.zero;
                return;
            }

            if (_lastAppliedOffset.sqrMagnitude <= 0f)
            {
                return;
            }

            _targetBone.X -= _lastAppliedOffset.x;
            _targetBone.Y -= _lastAppliedOffset.y;
            _lastAppliedOffset = Vector2.zero;
        }

        private static float GetReactionMultiplier(BodyPartType part)
        {
            switch (part)
            {
                case BodyPartType.Head:
                    return 0.5f;
                case BodyPartType.Torso:
                    return 0.9f;
                case BodyPartType.ArmUpperFront:
                case BodyPartType.ArmUpperBack:
                    return 0.78f;
                case BodyPartType.ArmLowerBack:
                case BodyPartType.ArmLowerFront:
                    return 0.72f;
                case BodyPartType.LegUpperBack:
                case BodyPartType.LegUpperFront:
                    return 0.75f;
                case BodyPartType.LegLowerBack:
                case BodyPartType.LegLowerFront:
                    return 0.7f;
                case BodyPartType.FootBack:
                case BodyPartType.FootFront:
                    return 0.66f;
                default:
                    return 0.7f;
            }
        }

        private static float GetMaxOffsetMultiplier(BodyPartType part)
        {
            switch (part)
            {
                case BodyPartType.Head:
                    return 0.5f;
                case BodyPartType.Torso:
                    return 0.75f;
                default:
                    return 0.8f;
            }
        }
    }
}
