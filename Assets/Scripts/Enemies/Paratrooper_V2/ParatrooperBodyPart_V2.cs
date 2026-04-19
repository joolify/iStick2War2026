using Assets.Scripts.Components;
using iStick2War;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// ParatrooperBodyPart (Hitbox Layer)
/// </summary>
/// <remarks>
/// Represents a single hitbox (body part) of the Paratrooper entity.
/// This component is intentionally minimal and acts only as a relay between
/// hit detection and the damage system.
///
/// Hit Flow:
/// Raycast → BodyPart → DamageReceiver
///
/// Responsibilities:
/// - Identifies which body part was hit
/// - Forwards hit data to the ParatrooperDamageReceiver
///
/// Constraints:
/// - MUST NOT contain any damage calculation logic
/// - MUST NOT modify Model data directly
/// - MUST NOT contain gameplay logic
/// - MUST remain lightweight and efficient (can exist in large numbers)
///
/// Notes:
/// - Typically attached to child GameObjects with colliders
/// - Works with raycasts or collision-based hit detection
/// </remarks>
namespace iStick2War_V2
{
public class ParatrooperBodyPart_V2 : MonoBehaviour
{

    public BodyPartType bodyPart;

    [Header("Spine hit reaction")]
    [SerializeField] private SkeletonDataAsset _skeletonDataAsset;
    [SpineBone(dataField: nameof(_skeletonDataAsset))]
    [SerializeField] private string _targetBoneName;
    [SerializeField] private float _impulseScale = 0.035f;
    [SerializeField] private float _minImpulse = 0.1f;
    [SerializeField] private float _maxImpulse = 0.34f;
    [SerializeField] private float _maxOffset = 0.12f;
    [SerializeField] private float _instantKickFactor = 0.09f;
    [SerializeField] private float _maxVelocityFromOffset = 4.1f;
    [SerializeField] private float _springStrength = 180f;
    [SerializeField] private float _damping = 42f;
    [SerializeField] private bool _debugHitReaction = false;
    [SerializeField] private bool _freezeBoundingBoxFollowerAtStartup = true;
    [SerializeField] private bool _enableColliderWatchdog = true;
    [SerializeField] private float _watchdogIntervalSeconds = 1f;

    private ParatrooperDamageReceiver_V2 _damageReceiver;
    private SkeletonAnimation _skeletonAnimation;
    private BoundingBoxFollower _boundingBoxFollower;
    private Bone _targetBone;

    private ParatrooperModel_V2 _model;
    private Vector2 _boneOffset;
    private Vector2 _boneVelocity;
    private Vector2 _lastAppliedOffset;
    private bool _isSubscribedToUpdateComplete;
    private float _nextWatchdogTime;
    private bool _watchdogMissingLogged;

    void Awake()
    {
        _damageReceiver = GetComponentInParent<ParatrooperDamageReceiver_V2>();
        EnsureSkeletonAnimationReference();
        SyncSkeletonDataAsset();
        StabilizeBoundingBoxFollowerCollider();

        gameObject.layer = LayerMask.NameToLayer("EnemyBodyPart");

        var ownCollider = GetComponent<Collider2D>();
        if (ownCollider == null)
        {
            Debug.LogWarning($"[ParatrooperBodyPart_V2] No Collider2D on '{gameObject.name}'. This body part cannot be hit by raycast.");
        }

        // Cache while still parented: death visuals may reparent hitboxes (e.g. SetParent(null)), which breaks
        // GetComponentInParent for later queries (AutoHero overlap / aim filtering).
        if (_model == null)
        {
            _model = GetComponentInParent<ParatrooperModel_V2>();
        }
    }

    /// <summary>
    /// True if this hitbox belongs to a living paratrooper. Uses Awake-cached model so it stays valid after
    /// reparent-at-death; callers must not treat missing model as alive.
    /// </summary>
    public bool IsLivingCharacterForTargeting()
    {
        return _model != null && !_model.IsDead();
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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (_damageReceiver == null)
        {
            Debug.LogWarning($"BodyPart has no {nameof(_damageReceiver)} assigned.");
            return;
        }

        ResolveTargetBone();
    }

    private void OnValidate()
    {
        EnsureSkeletonAnimationReference();
        SyncSkeletonDataAsset();
    }

    // Update is called once per frame
    void Update()
    {
        if (_enableColliderWatchdog && Time.time >= _nextWatchdogTime)
        {
            _nextWatchdogTime = Time.time + Mathf.Max(0.1f, _watchdogIntervalSeconds);
            RunColliderWatchdog();
        }
    }

    private void HandleSkeletonUpdateComplete(ISkeletonAnimation _)
    {
        if (_targetBone == null)
        {
            return;
        }

        // Remove previous frame's additive offset first, so we never accumulate permanent drift.
        RestoreLastAppliedOffset();

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        // Spring-damper: pushes the hit bone then quickly returns it to animation pose.
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

        if (_debugHitReaction)
        {
            Debug.Log($"[ParatrooperBodyPart_V2] ApplyReaction bone={_targetBoneName} offset={_boneOffset} velocity={_boneVelocity}");
        }
    }

    /// <summary>
    /// Called when this body part is hit by a raycast or collision.
    /// Forwards the damage information to the DamageReceiver.
    /// </summary>
    public void OnHit(DamageInfo info)
    {
        info.BodyPart = bodyPart;
        Debug.Log($"[ParatrooperBodyPart_V2] OnHit part={info.BodyPart}, base={info.BaseDamage}, collider={gameObject.name}");
        AddHitImpulse(info);
        _damageReceiver.TakeDamage(info);
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
            Debug.LogWarning($"[ParatrooperBodyPart_V2] Bone '{_targetBoneName}' not found for '{gameObject.name}'.");
        }
    }

    private void SyncSkeletonDataAsset()
    {
        if (_skeletonDataAsset != null)
        {
            return;
        }

        if (_skeletonAnimation == null)
        {
            _skeletonAnimation = GetComponentInParent<SkeletonAnimation>();
        }

        if (_skeletonAnimation == null)
        {
            var paratrooperRoot = GetComponentInParent<Paratrooper>();
            if (paratrooperRoot != null)
            {
                _skeletonAnimation = paratrooperRoot.GetComponentInChildren<SkeletonAnimation>(true);
            }
        }

        if (_skeletonAnimation == null)
        {
            var sceneRoot = transform.root;
            if (sceneRoot != null)
            {
                _skeletonAnimation = sceneRoot.GetComponentInChildren<SkeletonAnimation>(true);
            }
        }

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
        Vector2 awayFromHit = ((Vector2)boneWorld - info.HitPoint);

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

        // Immediate visual kick so each hit is clearly visible this frame.
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

        if (_debugHitReaction)
        {
            Debug.Log($"[ParatrooperBodyPart_V2] AddImpulse part={info.BodyPart} bone={_targetBoneName} impulse={impulse:F3} localDir={localDirection}");
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

        var paratrooperRoot = GetComponentInParent<Paratrooper>();
        if (paratrooperRoot != null)
        {
            _skeletonAnimation = paratrooperRoot.GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (_skeletonAnimation != null)
        {
            return;
        }

        var sceneRoot = transform.root;
        if (sceneRoot != null)
        {
            _skeletonAnimation = sceneRoot.GetComponentInChildren<SkeletonAnimation>(true);
        }
    }

    private void StabilizeBoundingBoxFollowerCollider()
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

        // BoundingBoxFollower can disable/destroy colliders when slot attachments change.
        // We initialize once and then freeze its state to keep body-part hitboxes stable.
        _boundingBoxFollower.Initialize(true);
        _boundingBoxFollower.clearStateOnDisable = false;

        var polygonColliders = GetComponents<PolygonCollider2D>();
        for (int i = 0; i < polygonColliders.Length; i++)
        {
            if (polygonColliders[i] != null)
            {
                polygonColliders[i].enabled = true;
            }
        }

        _boundingBoxFollower.enabled = false;

        if (_debugHitReaction)
        {
            Debug.Log($"[ParatrooperBodyPart_V2] Frozen BoundingBoxFollower on '{name}', colliders={polygonColliders.Length}.");
        }
    }

    private void RunColliderWatchdog()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        var anyEnabledCollider = false;
        var polygonColliders = GetComponents<PolygonCollider2D>();
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
            Debug.LogWarning($"[ParatrooperBodyPart_V2] Collider watchdog: '{name}' has no enabled PolygonCollider2D. Trying recovery.");
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
            Debug.LogWarning($"[ParatrooperBodyPart_V2] Collider watchdog: no BoundingBoxFollower on '{name}', cannot rebuild collider.");
            return;
        }

        bool wasEnabled = _boundingBoxFollower.enabled;
        _boundingBoxFollower.enabled = true;
        _boundingBoxFollower.Initialize(true);

        var polygonColliders = GetComponents<PolygonCollider2D>();
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
