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
public class ParatrooperBodyPart_V2 : MonoBehaviour
{

    public BodyPartType bodyPart;

    [Header("Spine hit reaction")]
    [SerializeField] private SkeletonDataAsset _skeletonDataAsset;
    [SpineBone(dataField: nameof(_skeletonDataAsset))]
    [SerializeField] private string _targetBoneName;
    [SerializeField] private float _impulseScale = 0.125f;
    [SerializeField] private float _maxOffset = 2f;
    [SerializeField] private float _springStrength = 55f;
    [SerializeField] private float _damping = 10f;
    [SerializeField] private bool _debugHitReaction = false;

    private ParatrooperDamageReceiver_V2 _damageReceiver;
    private SkeletonAnimation _skeletonAnimation;
    private Bone _targetBone;

    private ParatrooperModel_V2 _model;
    private Vector2 _boneOffset;
    private Vector2 _boneVelocity;
    private bool _isSubscribedToUpdateComplete;

    void Awake()
    {
        _damageReceiver = GetComponentInParent<ParatrooperDamageReceiver_V2>();
        EnsureSkeletonAnimationReference();
        SyncSkeletonDataAsset();

        gameObject.layer = LayerMask.NameToLayer("EnemyBodyPart");

        var ownCollider = GetComponent<Collider2D>();
        if (ownCollider == null)
        {
            Debug.LogWarning($"[ParatrooperBodyPart_V2] No Collider2D on '{gameObject.name}'. This body part cannot be hit by raycast.");
        }
    }

    private void OnEnable()
    {
        EnsureSkeletonAnimationReference();
        SubscribeToSkeletonUpdateComplete();
        ResolveTargetBone();
    }

    private void OnDisable()
    {
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

    }

    private void HandleSkeletonUpdateComplete(ISkeletonAnimation _)
    {
        if (_targetBone == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        // Spring-damper: pushes the hit bone then quickly returns it to animation pose.
        _boneVelocity += (-_springStrength * _boneOffset - _damping * _boneVelocity) * dt;
        _boneOffset += _boneVelocity * dt;

        if (_boneOffset.sqrMagnitude < 0.000001f && _boneVelocity.sqrMagnitude < 0.000001f)
        {
            _boneOffset = Vector2.zero;
            _boneVelocity = Vector2.zero;
            return;
        }

        _targetBone.X += _boneOffset.x;
        _targetBone.Y += _boneOffset.y;

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
        float impulse = info.BaseDamage * _impulseScale * bodyPartMultiplier;

        // Immediate visual kick so each hit is clearly visible this frame.
        _boneOffset += localDirection * (impulse * 0.45f);
        _boneVelocity += localDirection * impulse;

        if (_boneOffset.sqrMagnitude > _maxOffset * _maxOffset)
        {
            _boneOffset = _boneOffset.normalized * _maxOffset;
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

    private static float GetReactionMultiplier(BodyPartType part)
    {
        switch (part)
        {
            case BodyPartType.Head:
                return 1.8f;
            case BodyPartType.Torso:
                return 1.25f;
            case BodyPartType.LegUpperBack:
            case BodyPartType.LegUpperFront:
            case BodyPartType.LegLowerBack:
            case BodyPartType.LegLowerFront:
                return 1.1f;
            default:
                return 1f;
        }
    }
}
