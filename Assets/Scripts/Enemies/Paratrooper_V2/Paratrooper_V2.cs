using iStick2War;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
/*
 * <summary>
     * PARATROOPER ARCHITECTURE (COMPOSITION ROOT)
     *
     * Paratrooper (ROOT / BRAIN)
     * ├── ParatrooperModel
     * ├── ParatrooperController (AI + State Machine)
     * ├── ParatrooperDamageReceiver
     * ├── ParatrooperStateMachine
     * ├── ParatrooperView (Spine + VFX)
     * ├── BodyParts (multiple)
     * │     └── ParatrooperBodyPart
     * └── ParatrooperDeathHandler
</summary>
<remarks>
     *
     * NOTES:
     * - This class should NOT contain gameplay logic.
     * - It only coordinates sub-components.
     * - Prefer dependency references via inspector or GetComponent in Awake().
     * - Binds everything together
     * - Owns lifecycle
     * - References all sub systems

--------------------------------

# SHOOT FLOW

Player Weapon
   ↓
Raycast Hit
   ↓
ParatrooperBodyPart
   ↓
ParatrooperDamageReceiver
   ↓
ParatrooperModel (HP reduces)
   ↓
ParatrooperStateMachine (maybe change state)
   ↓
ParatrooperView (hit animation)
   ↓
ParatrooperDeathHandler (if HP <= 0)

--------------------------------

# Spine Event Flow

Spine Animation Event
        ↓
ParatrooperView (ONLY forwards event)
        ↓
ParatrooperController (interprets event)
        ↓
WeaponSystem / AI / StateMachine

--------------------------------

PARATROOPER (ROOT)
│
├── ParatrooperController  ← receives animation events
├── ParatrooperModel
├── ParatrooperStateMachine
├── ParatrooperDamageReceiver
│
├── ParatrooperView (Spine bridge ONLY)
│       └── SpineEventForwarder
│
├── WeaponSystem
├── BodyParts
│
└── ParatrooperDeathHandler

--------------------------------

# AI FLOW

ParatrooperController (Update tick)
   ↓
StateMachine decides state
   ↓
Model updates data
   ↓
View reacts visually

</remarks>
     */
namespace iStick2War_V2
{
public class Paratrooper : MonoBehaviour
{
    [Header("Core Systems")]
    private ParatrooperModel_V2 _model;
    [SerializeField] private ParatrooperController_V2 _controller;
    [SerializeField] private ParatrooperStateMachine_V2 _stateMachine;
    [SerializeField] private ParatrooperDamageReceiver_V2 _damageReceiver;
    [SerializeField] private ParatrooperDeathHandler_V2 _deathHandler;
    [SerializeField] private ParatrooperWeaponSystem_V2 _weaponSystem;
    [SerializeField] private ParatrooperSpineEventForwarder_V2 _spineEventForwarder;
    [SerializeField] private SkeletonAnimation _skeletonAnimation;

    [Header("View")]
    [SerializeField] private ParatrooperView_V2 _view;
    [Tooltip(
        "If true, Awake() zeros SkeletonAnimation.localPosition X/Y when its planar offset exceeds the max, and " +
        "moves the paratrooper root + Rigidbody2D by the same world delta so Spine mesh/hitboxes stay where the prefab " +
        "placed them (avoids invisible mesh off-screen while shot LineRenderer still uses root/fallback origin).")]
    [SerializeField] private bool _autoFixVisualRootOffsetOnSpawn = true;
    [Tooltip("Only used when auto-fix is on. Offsets larger than this (planar magnitude) trigger flatten + root compensation.")]
    [SerializeField] private float _maxAllowedVisualRootLocalOffset = 2.5f;
    [SerializeField] private bool _debugVisualRootOffsetFixLogs;

    [Header("Body Parts")]
    [SerializeField] private ParatrooperBodyPart_V2[] _bodyParts;

    [Header("Air Movement")]
    [SerializeField] private Rigidbody2D _rigidbody2D;
    [SerializeField] private Collider2D _mainCollider2D;
    [SerializeField] private float _glideMaxFallSpeed = 1.35f;
    [SerializeField] private float _glideMinFallSpeed = 0.5f;
    [SerializeField] private float _glideDeathMaxFallSpeed = 4.75f;
    [SerializeField] private LayerMask _groundMask = 0;
    [SerializeField] private float _groundCheckDistance = 0.08f;
    [Tooltip("How far below the feet we look for Ground when switching Glide → Land (larger = earlier landing).")]
    [SerializeField] private float _nearGroundRayDistance = 3f;
    [Tooltip(
        "Optional empty child at the visual feet. When set, Ground ray origins use this transform (recommended) " +
        "instead of collider bounds — avoids Spine mesh vs BoundingBox mismatch without adding extra physics colliders.")]
    [SerializeField] private Transform _groundProbeAnchor;
    [Tooltip(
        "If the anchor is not parented under this paratrooper, or its X drifts farther than this from the Rigidbody2D " +
        "(e.g. wrong scene reference / stale bone), it is ignored so probes use collider or rigidbody fallbacks.")]
    [SerializeField] private float _groundProbeAnchorMaxHorizontalDriftFromRb = 4f;
    [Tooltip(
        "Ground probes use the lowest enabled non-trigger collider on this Rigidbody2D (Spine BoundingBoxFollower " +
        "polygons), not only the root collider. Negative bias moves the probe down if feet still look above Ground.")]
    [SerializeField] private float _groundProbeFootBiasWorld;
    [Tooltip("Buffer size for Rigidbody2D.GetAttachedColliders (raise if you add many hitboxes).")]
    [SerializeField] [Min(8)] private int _groundAttachedColliderProbeCapacity = 48;

    [Header("Debug — Ground probe")]
    [Tooltip("Throttled logs for ray origin, mask, hits, and rigidbody vs probe (feet vs colliders vs anchor).")]
    [SerializeField] private bool _debugGroundProbeLogs;
    [SerializeField] private float _debugGroundProbeLogIntervalSeconds = 0.35f;
    [Tooltip("If on, only logs while state is Glide, GlideDie, or Land (reduces noise from Die/Shoot checks).")]
    [SerializeField] private bool _debugGroundProbeLogOnlyWhenGlideOrLand;
    [Tooltip(
        "While deploying / gliding, ignore physics contacts with the Bunker layer so drops are not blocked mid-air " +
        "(Unity 6 Rigidbody2D.excludeLayers). Cleared when landing or fighting on the ground.")]
    [SerializeField] private bool _excludeBunkerLayerWhileAirborne = true;
    [Tooltip(
        "OR'ed into Rigidbody2D.excludeLayers while Deploy/Glide/GlideDie (in addition to Bunker when enabled). " +
        "Assign if bunker sandbags / props still use Default or another layer and block falling.")]
    [SerializeField] private LayerMask _airborneRigidbodyExtraExcludeLayers;

    [Header("Collider Debug")]
    [SerializeField] private bool _enableColliderSummaryLog = true;
    [SerializeField] private float _colliderSummaryIntervalSeconds = 2f;
    [SerializeField] private bool _onlyLogWhenSummaryChanges = true;
    [SerializeField] private bool _warnWhenColliderSummaryIsNotFull = true;
    [SerializeField] private bool _autoFixBodyPartLayerToEnemyBodyPart = true;

    private float _nextColliderSummaryTime;
    private string _lastColliderSummary;
    private int _groundLayer = -1;
    private int _landingPointLayer = -1;
    private int _trackedAirborneRigidbodyExcludeBits;
    private Collider2D[] _attachedCollidersScratch;
    private bool _warnedAttachedColliderBufferTruncation;
    private float _lastGroundProbeDebugUnscaledTime = float.NegativeInfinity;
    private bool _warnedGroundProbeAnchorRejected;
    private float _airborneGravityScaleCached;
    private bool _airborneGravityScaleCachedValid;

    /// <summary>
    /// Spine root world position at end of <see cref="Awake"/> (before spawner flips spawn facing on root scale X).
    /// Mirroring after that pass would shift the mesh/hitboxes unless we compensate — see <see cref="ReconcileRootPositionAfterSpawnFacing"/>.
    /// </summary>
    private Vector3 _spinePivotWorldAfterVisualRootSanitize;
    private bool _pendingSpineWorldReconcileAfterSpawnFacing;

    private enum GroundProbeOriginSource
    {
        Failed,
        Anchor,
        AttachedCollidersLowest,
        MainCollider,
        RigidbodyPosition,
    }

    private readonly struct GroundProbeBuild
    {
        public readonly bool Ok;
        public readonly Vector2 Origin;
        public readonly GroundProbeOriginSource Source;
        public readonly float DebugLowestSolidMinYWorld;
        public readonly int DebugSolidColliderCount;

        private GroundProbeBuild(
            bool ok,
            Vector2 origin,
            GroundProbeOriginSource source,
            float debugLowestSolidMinYWorld,
            int debugSolidColliderCount)
        {
            Ok = ok;
            Origin = origin;
            Source = source;
            DebugLowestSolidMinYWorld = debugLowestSolidMinYWorld;
            DebugSolidColliderCount = debugSolidColliderCount;
        }

        public static GroundProbeBuild Failed => new GroundProbeBuild(false, default, GroundProbeOriginSource.Failed, float.NaN, 0);

        public static GroundProbeBuild FromAnchor(Vector2 origin)
        {
            return new GroundProbeBuild(true, origin, GroundProbeOriginSource.Anchor, float.NaN, 0);
        }

        public static GroundProbeBuild FromAttachedLowest(Vector2 origin, float lowestMinYWorld, int solidCount)
        {
            return new GroundProbeBuild(true, origin, GroundProbeOriginSource.AttachedCollidersLowest, lowestMinYWorld, solidCount);
        }

        public static GroundProbeBuild FromMainCollider(Vector2 origin, float lowestMinYWorld)
        {
            return new GroundProbeBuild(true, origin, GroundProbeOriginSource.MainCollider, lowestMinYWorld, 1);
        }

        public static GroundProbeBuild FromRigidbodyPosition(Vector2 origin)
        {
            return new GroundProbeBuild(true, origin, GroundProbeOriginSource.RigidbodyPosition, float.NaN, 0);
        }
    }


    /*
     * Paratrooper.cs
     *  ↓
     * Initialize systems
     *    ↓
     * Controller starts AI
     */
    private void Awake()
    {
        InitializeDependencies();
        SanitizeVisualRootAlignment();
        _groundLayer = LayerMask.NameToLayer("Ground");
        _landingPointLayer = LayerMask.NameToLayer("LandingPoint");
        int groundProbeMask = LayerMask.GetMask("Ground", "LandingPoint");
        if (groundProbeMask != 0)
        {
            // Prefabs often leave this at Default/-1 (Everything); force a sane mask so probes match IsGroundLayer.
            _groundMask = groundProbeMask;
        }
        else if (_groundMask.value == 0 && _groundLayer >= 0)
        {
            _groundMask = 1 << _groundLayer;
        }

        ValidateBodyPartSetup();

        WireSystems();

        _controller.StartGame();
        CaptureSpineWorldAnchorForPostSpawnFacingReconcile();
    }

    private void CaptureSpineWorldAnchorForPostSpawnFacingReconcile()
    {
        if (_skeletonAnimation == null)
        {
            return;
        }

        Transform skeletonRoot = _skeletonAnimation.transform;
        if (skeletonRoot == transform || !skeletonRoot.IsChildOf(transform))
        {
            return;
        }

        _spinePivotWorldAfterVisualRootSanitize = skeletonRoot.position;
        _pendingSpineWorldReconcileAfterSpawnFacing = true;
    }

    private void InitializeDependencies()
    {
        // Ensure references exist (safe setup pattern)
        if (_model == null) _model = GetComponent<ParatrooperModel_V2>();
        if (_controller == null) _controller = GetComponent<ParatrooperController_V2>();
        if (_view == null) _view = GetComponent<ParatrooperView_V2>();
        if (_damageReceiver == null) _damageReceiver = GetComponent<ParatrooperDamageReceiver_V2>();
        if (_stateMachine == null) _stateMachine = GetComponent<ParatrooperStateMachine_V2>();
        if (_deathHandler == null) _deathHandler = GetComponent<ParatrooperDeathHandler_V2>();
        if (_spineEventForwarder == null) _spineEventForwarder = GetComponent<ParatrooperSpineEventForwarder_V2>();
        if (_weaponSystem == null)
        {
            _weaponSystem = GetComponent<ParatrooperWeaponSystem_V2>();
            if (_weaponSystem == null)
            {
                _weaponSystem = gameObject.AddComponent<ParatrooperWeaponSystem_V2>();
                Debug.LogWarning("[Paratrooper_V2] ParatrooperWeaponSystem_V2 was missing and has been auto-added to root.");
            }
        }
        if (_rigidbody2D == null) _rigidbody2D = GetComponent<Rigidbody2D>();
        if (_mainCollider2D == null) _mainCollider2D = GetComponent<Collider2D>();
        EnsureAttachedColliderScratch();
    }

    private void EnsureAttachedColliderScratch()
    {
        int cap = Mathf.Clamp(_groundAttachedColliderProbeCapacity, 8, 128);
        if (_attachedCollidersScratch == null || _attachedCollidersScratch.Length != cap)
        {
            _attachedCollidersScratch = new Collider2D[cap];
        }
    }

    private void SanitizeVisualRootAlignment()
    {
        if (!_autoFixVisualRootOffsetOnSpawn || _skeletonAnimation == null)
        {
            return;
        }

        Transform skeletonRoot = _skeletonAnimation.transform;
        if (skeletonRoot == transform || !skeletonRoot.IsChildOf(transform))
        {
            return;
        }

        Vector3 local = skeletonRoot.localPosition;
        float planarOffset = new Vector2(local.x, local.y).magnitude;
        if (planarOffset <= Mathf.Max(0.05f, _maxAllowedVisualRootLocalOffset))
        {
            return;
        }

        Vector3 worldBefore = skeletonRoot.position;
        skeletonRoot.localPosition = new Vector3(0f, 0f, local.z);
        Vector3 worldAfter = skeletonRoot.position;
        Vector3 worldDelta = worldBefore - worldAfter;
        transform.position += worldDelta;
        if (_rigidbody2D != null)
        {
            _rigidbody2D.position = (Vector2)transform.position;
        }

        if (_debugVisualRootOffsetFixLogs)
        {
            Debug.LogWarning(
                $"[Paratrooper_V2] Flattened SkeletonAnimation local XY from {local} to (0,0,{local.z:0.###}); " +
                $"compensated root+rb by worldDelta={worldDelta} so Spine world pose is preserved.");
        }
    }

    /// <summary>
    /// Call after any code that changes the paratrooper root's lossy scale (e.g. spawn facing flip). Awake-time
    /// visual-root flattening preserves Spine world position for the scale at that moment; flipping X afterward
    /// mirrors children and would otherwise shift the mesh/hitboxes in world space.
    /// </summary>
    public void ReconcileRootPositionAfterSpawnFacing()
    {
        if (!_pendingSpineWorldReconcileAfterSpawnFacing || _skeletonAnimation == null)
        {
            return;
        }

        Transform skeletonRoot = _skeletonAnimation.transform;
        if (skeletonRoot == transform || !skeletonRoot.IsChildOf(transform))
        {
            _pendingSpineWorldReconcileAfterSpawnFacing = false;
            return;
        }

        Vector3 delta = _spinePivotWorldAfterVisualRootSanitize - skeletonRoot.position;
        if (delta.sqrMagnitude < 1e-10f)
        {
            _pendingSpineWorldReconcileAfterSpawnFacing = false;
            return;
        }

        transform.position += delta;
        if (_rigidbody2D != null)
        {
            _rigidbody2D.position = (Vector2)transform.position;
        }

        if (_debugVisualRootOffsetFixLogs)
        {
            Debug.LogWarning(
                $"[Paratrooper_V2] Reconciled root+rb after spawn facing by worldDelta={delta} " +
                $"(preserved spine pivot {_spinePivotWorldAfterVisualRootSanitize}).");
        }

        _pendingSpineWorldReconcileAfterSpawnFacing = false;
    }

    /// <summary>
    /// Spawner calls this after <see cref="Awake"/> and optional spawn-facing. Instantiate uses a world drop point, but
    /// <see cref="SanitizeVisualRootAlignment"/> may shift the root to preserve Spine pose, and scale flips mirror children.
    /// This moves root + <see cref="Rigidbody2D"/> so the Spine transform (or the root if SkeletonAnimation is on the root)
    /// matches <paramref name="requestedWorldPosition"/> in XY.
    /// </summary>
    public void SnapSpawnAlignmentToRequestedWorld(Vector3 requestedWorldPosition)
    {
        Vector3 referenceWorld = transform.position;
        if (_skeletonAnimation != null)
        {
            referenceWorld = _skeletonAnimation.transform.position;
        }

        Vector3 delta = requestedWorldPosition - referenceWorld;
        delta.z = 0f;
        if (delta.sqrMagnitude < 1e-10f)
        {
            return;
        }

        transform.position += delta;
        if (_rigidbody2D != null)
        {
            _rigidbody2D.position = (Vector2)transform.position;
        }
    }

    /// <summary>
    /// Applied by <see cref="EnemySpawner_V2"/> after spawn so <see cref="WaveConfig_V2"/> multipliers affect this unit.
    /// </summary>
    public void ApplyWaveDifficultyMultipliers(float healthMultiplier, float damageMultiplier)
    {
        if (_model == null)
        {
            _model = GetComponent<ParatrooperModel_V2>();
        }

        if (_model != null)
        {
            _model.ApplyWaveHealthMultiplier(healthMultiplier);
        }

        if (_weaponSystem == null)
        {
            _weaponSystem = GetComponent<ParatrooperWeaponSystem_V2>();
        }

        if (_weaponSystem == null)
        {
            _weaponSystem = GetComponentInChildren<ParatrooperWeaponSystem_V2>(true);
        }

        if (_weaponSystem != null)
        {
            _weaponSystem.ApplyWaveDamageMultiplier(damageMultiplier);
        }
    }

    private void WireSystems()
    {
        // Inject dependencies manually (clean + fast in Unity)
        if (_model == null)
        {
            Debug.LogError("[Paratrooper_V2] Missing ParatrooperModel_V2 component.");
            return;
        }

        // 2. Init StateMachine
        _stateMachine.Initialize(_model);

        // 3. Init DamageReceiver
        //_damageReceiver.Initialize(_model, _stateMachine);
        //FIXME

        // 4. Init Controller (brain)
        _controller.Initialize(_model, _stateMachine, _damageReceiver, _weaponSystem);

        // 5. Init View
        _view.Initialize(_stateMachine);

        // 6. Init DeathHandler
        _deathHandler.Initialize(_stateMachine);

        // 7. Wire BodyParts → DamageReceiver
        //foreach (var part in _bodyParts)
        //{
        //    part.Initialize(_damageReceiver);
        //}
        //FIXME

        // 8. Wire Spine Events → Controller
        _spineEventForwarder.Init(_controller, _skeletonAnimation);

        // Init WeaponSystem
        if (_weaponSystem != null)
        {
            _weaponSystem.Initialize(_model);
        }

        // 9. Hook events (StateMachine → View & Death)
        _stateMachine.OnStateChanged += HandleStateChanged;
        if (_damageReceiver != null && _view != null)
        {
            _damageReceiver.OnExploded += _view.ExplodeIntoPieces;
        }
    }

    private void OnDestroy()
    {
        if (_stateMachine != null)
            _stateMachine.OnStateChanged -= HandleStateChanged;
        if (_damageReceiver != null && _view != null)
        {
            _damageReceiver.OnExploded -= _view.ExplodeIntoPieces;
        }

        ClearAirborneBunkerCollisionExclusion();
    }

    private void OnDisable()
    {
        ClearAirborneBunkerCollisionExclusion();
    }

    /*
     * Controller → ChangeState()
     *             ↓
     * StateMachine updates state
     *             ↓
     * OnStateChanged EVENT fires
     *             ↓
     * View / DeathHandler / others react
    */
    private void HandleStateChanged(StickmanBodyState from, StickmanBodyState to)
    {
        Debug.Log($"HandleStateChanged: State changed: {from} → {to}");

        // Safety: if death is requested while still in the air, force airborne death state first.
        // This guarantees GlideDeath animation before ground impact animation.
        // Important: when coming from ground-combat states, keep ground death even if IsGrounded()
        // has a one-frame false negative.
        bool wasGroundCombatState =
            from == StickmanBodyState.Shoot ||
            from == StickmanBodyState.Land ||
            from == StickmanBodyState.Idle ||
            from == StickmanBodyState.Run;
        if (to == StickmanBodyState.Die && wasGroundCombatState)
        {
            Debug.Log($"[Paratrooper_V2] Keeping ground death from {from} (skip GlideDie conversion).");
        }
        if (to == StickmanBodyState.Die && !wasGroundCombatState && !IsGrounded())
        {
            Debug.Log("[Paratrooper_V2] Converted Die -> GlideDie (airborne).");
            _stateMachine.ChangeState(StickmanBodyState.GlideDie);
            return;
        }

        UpdateAirborneBunkerCollisionExclusion(to);

        if (to == StickmanBodyState.Land && (from == StickmanBodyState.Glide || from == StickmanBodyState.GlideDie))
        {
            SnapRigidbodyToGroundUnderProbe();
        }

        // Death handling
        if (to == StickmanBodyState.Die || to == StickmanBodyState.GlideDie)
        {
            _deathHandler.Die();
        }
    }

    private void UpdateAirborneBunkerCollisionExclusion(StickmanBodyState to)
    {
        if (_rigidbody2D == null)
        {
            return;
        }

        bool airborne =
            to == StickmanBodyState.Deploy ||
            to == StickmanBodyState.Glide ||
            to == StickmanBodyState.GlideDie;

        if (!airborne)
        {
            if (_trackedAirborneRigidbodyExcludeBits != 0)
            {
                _rigidbody2D.excludeLayers &= ~_trackedAirborneRigidbodyExcludeBits;
                _trackedAirborneRigidbodyExcludeBits = 0;
            }

            return;
        }

        int add = BuildAirborneRigidbodyExcludeLayerBits();
        if (_trackedAirborneRigidbodyExcludeBits != 0)
        {
            _rigidbody2D.excludeLayers &= ~_trackedAirborneRigidbodyExcludeBits;
        }

        _trackedAirborneRigidbodyExcludeBits = add;
        if (add != 0)
        {
            _rigidbody2D.excludeLayers |= add;
        }
    }

    private int BuildAirborneRigidbodyExcludeLayerBits()
    {
        int bits = 0;
        if (_excludeBunkerLayerWhileAirborne)
        {
            int bunkerLayer = LayerMask.NameToLayer("Bunker");
            if (bunkerLayer >= 0)
            {
                bits |= 1 << bunkerLayer;
            }
        }

        bits |= _airborneRigidbodyExtraExcludeLayers.value;
        return bits;
    }

    private void ClearAirborneBunkerCollisionExclusion()
    {
        if (_rigidbody2D == null || _trackedAirborneRigidbodyExcludeBits == 0)
        {
            return;
        }

        _rigidbody2D.excludeLayers &= ~_trackedAirborneRigidbodyExcludeBits;
        _trackedAirborneRigidbodyExcludeBits = 0;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }


    /* Update is called once per frame
     * Paratrooper.Update()
     * ↓
     * Controller.Tick()
     * ↓
     * StateMachine decides
     * ↓
     * View updates animation
     * */
    void Update()
    {
        _controller.Tick(Time.deltaTime);
        ApplyGlideAirMovement();
        HandleNearGroundLandingTransition();
        HandleGlideDeathLandingTransition();

        if (_enableColliderSummaryLog && Time.time >= _nextColliderSummaryTime)
        {
            _nextColliderSummaryTime = Time.time + Mathf.Max(0.2f, _colliderSummaryIntervalSeconds);
            LogColliderSummary();
        }
    }

    private void LateUpdate()
    {
        ApplyGroundedCombatPhysicsSuppression();
    }

    /// <summary>
    /// Spine hitboxes are often triggers; without a solid floor collider the RB keeps accelerating.
    /// While landing / shooting on the ground, cancel vertical motion and gravity (X stays physics-driven if any).
    /// </summary>
    private void ApplyGroundedCombatPhysicsSuppression()
    {
        if (_rigidbody2D == null || _stateMachine == null)
        {
            return;
        }

        StickmanBodyState s = _stateMachine.CurrentState;
        if (s == StickmanBodyState.Land || s == StickmanBodyState.Shoot)
        {
            if (!_airborneGravityScaleCachedValid)
            {
                _airborneGravityScaleCached = _rigidbody2D.gravityScale;
                _airborneGravityScaleCachedValid = true;
            }

            _rigidbody2D.gravityScale = 0f;
            Vector2 v = _rigidbody2D.linearVelocity;
            if (!Mathf.Approximately(v.y, 0f))
            {
                v.y = 0f;
                _rigidbody2D.linearVelocity = v;
            }

            return;
        }

        if (_airborneGravityScaleCachedValid &&
            (s == StickmanBodyState.Deploy || s == StickmanBodyState.Glide || s == StickmanBodyState.GlideDie))
        {
            _rigidbody2D.gravityScale = _airborneGravityScaleCached;
        }
    }

    private void ApplyGlideAirMovement()
    {
        if (_rigidbody2D == null || _stateMachine == null)
        {
            return;
        }

        var currentState = _stateMachine.CurrentState;
        bool isGlide = currentState == StickmanBodyState.Glide || currentState == StickmanBodyState.Deploy;
        bool isGlideDeath = currentState == StickmanBodyState.GlideDie;
        if (!isGlide && !isGlideDeath)
        {
            return;
        }

        Vector2 velocity = _rigidbody2D.linearVelocity;
        float maxFallSpeed = isGlideDeath ? _glideDeathMaxFallSpeed : _glideMaxFallSpeed;
        if (velocity.y < -maxFallSpeed)
        {
            velocity.y = -maxFallSpeed;
        }
        else if (!isGlideDeath)
        {
            // Prevent deploy/glide from stalling at y=0 when gravity is neutralized by animation/state changes.
            float minFall = Mathf.Min(Mathf.Max(0f, _glideMinFallSpeed), maxFallSpeed);
            if (velocity.y > -minFall)
            {
                velocity.y = -minFall;
            }
        }

        if (isGlide)
        {
            // Design decision: paratroopers should drop straight down only.
            velocity.x = 0f;
        }

        _rigidbody2D.linearVelocity = velocity;
    }

    private void HandleGlideDeathLandingTransition()
    {
        if (_stateMachine == null || _stateMachine.CurrentState != StickmanBodyState.GlideDie)
        {
            return;
        }

        if (!IsGrounded())
        {
            return;
        }

        _stateMachine.ChangeState(StickmanBodyState.Die);
    }

    private void HandleNearGroundLandingTransition()
    {
        if (_stateMachine == null || _stateMachine.CurrentState != StickmanBodyState.Glide)
        {
            return;
        }

        if (!IsNearGround())
        {
            return;
        }

        Debug.Log("[Paratrooper_V2] Near Ground detected by raycast -> switching Glide to Land.");
        _stateMachine.ChangeState(StickmanBodyState.Land);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TryTransitionGlideDeathOnGroundContact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTransitionGlideDeathOnGroundContact(other);
    }

    private void TryTransitionGlideDeathOnGroundContact(Collider2D collider)
    {
        if (collider == null || _stateMachine == null)
        {
            return;
        }

        if (_stateMachine.CurrentState != StickmanBodyState.GlideDie)
        {
            return;
        }

        if (!IsGroundLayer(collider.gameObject.layer))
        {
            return;
        }

        Debug.Log("[Paratrooper_V2] GlideDie hit Ground layer -> switching to Die.");
        _stateMachine.ChangeState(StickmanBodyState.Die);
    }

    private bool IsGrounded()
    {
        GroundProbeBuild probe = BuildGroundProbeOrigin();
        if (!probe.Ok)
        {
            MaybeLogGroundProbeFailure("IsGrounded", probe);
            return false;
        }

        float rayLength = Mathf.Max(0.01f, _groundCheckDistance);
        RaycastHit2D[] hits = Physics2D.RaycastAll(probe.Origin, Vector2.down, rayLength, _groundMask);
        bool grounded = EvaluateGroundHits(hits);

        MaybeLogGroundProbe("IsGrounded", in probe, rayLength, hits, grounded);
        return grounded;
    }

    private bool IsNearGround()
    {
        GroundProbeBuild probe = BuildGroundProbeOrigin();
        if (!probe.Ok)
        {
            MaybeLogGroundProbeFailure("IsNearGround", probe);
            return false;
        }

        float rayLength = Mathf.Max(_groundCheckDistance, _nearGroundRayDistance);
        RaycastHit2D[] hits = Physics2D.RaycastAll(probe.Origin, Vector2.down, rayLength, _groundMask);
        bool near = EvaluateGroundHits(hits);

        MaybeLogGroundProbe("IsNearGround", in probe, rayLength, hits, near);
        return near;
    }

    /// <summary>
    /// When hitboxes are triggers only (no solid collider on the RB), physics never rests on Ground.
    /// Snap once on Land so the entity stays on the surface the glide probe already trusted.
    /// </summary>
    private void SnapRigidbodyToGroundUnderProbe()
    {
        if (_rigidbody2D == null)
        {
            return;
        }

        GroundProbeBuild probe = BuildGroundProbeOrigin();
        if (!probe.Ok)
        {
            return;
        }

        float rayLen = Mathf.Max(_nearGroundRayDistance, 4f);
        RaycastHit2D hit = Physics2D.Raycast(probe.Origin, Vector2.down, rayLen, _groundMask);
        if (hit.collider == null)
        {
            return;
        }

        if (hit.collider.transform.IsChildOf(transform))
        {
            return;
        }

        if (!IsGroundLayer(hit.collider.gameObject.layer))
        {
            return;
        }

        float targetFeetY = hit.point.y + 0.02f + _groundProbeFootBiasWorld;
        float deltaY = targetFeetY - probe.Origin.y;
        Vector2 p = _rigidbody2D.position;
        p.y += deltaY;
        _rigidbody2D.position = p;
        Vector2 v = _rigidbody2D.linearVelocity;
        v.y = 0f;
        _rigidbody2D.linearVelocity = v;
    }

    private bool EvaluateGroundHits(RaycastHit2D[] hits)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!IsGroundLayer(hitCollider.gameObject.layer))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// World point for downward Ground probes: optional feet anchor, else lowest solid collider on the rigidbody.
    /// </summary>
    private bool TryGetGroundProbeWorldOrigin(out Vector2 origin)
    {
        GroundProbeBuild b = BuildGroundProbeOrigin();
        origin = b.Origin;
        return b.Ok;
    }

    private bool ShouldUseGroundProbeAnchor()
    {
        if (_groundProbeAnchor == null)
        {
            return false;
        }

        if (_groundProbeAnchor == transform)
        {
            return true;
        }

        if (!_groundProbeAnchor.IsChildOf(transform))
        {
            return false;
        }

        if (_rigidbody2D == null)
        {
            return true;
        }

        float dx = Mathf.Abs(_groundProbeAnchor.position.x - _rigidbody2D.position.x);
        return dx <= Mathf.Max(0.01f, _groundProbeAnchorMaxHorizontalDriftFromRb);
    }

    private GroundProbeBuild BuildGroundProbeOrigin()
    {
        if (ShouldUseGroundProbeAnchor())
        {
            Vector3 p = _groundProbeAnchor.position;
            Vector2 origin = new Vector2(p.x, p.y + 0.01f + _groundProbeFootBiasWorld);
            return GroundProbeBuild.FromAnchor(origin);
        }

        if (_groundProbeAnchor != null && !_warnedGroundProbeAnchorRejected)
        {
            _warnedGroundProbeAnchorRejected = true;
            Debug.LogWarning(
                "[Paratrooper_V2] Ground probe anchor is set but ignored (not a child of this paratrooper, or X drift " +
                $"from Rigidbody2D exceeds {_groundProbeAnchorMaxHorizontalDriftFromRb:F2}). Using collider / rigidbody fallbacks.");
        }

        float probeX = _rigidbody2D != null ? _rigidbody2D.position.x : transform.position.x;
        float lowestMinY = float.PositiveInfinity;
        int solidCount = 0;
        int written = 0;

        if (_rigidbody2D != null && _attachedCollidersScratch != null)
        {
            written = _rigidbody2D.GetAttachedColliders(_attachedCollidersScratch);
            int totalAttached = _rigidbody2D.attachedColliderCount;
            if ((written < 0 || totalAttached > _attachedCollidersScratch.Length) && !_warnedAttachedColliderBufferTruncation)
            {
                _warnedAttachedColliderBufferTruncation = true;
                Debug.LogWarning(
                    $"[Paratrooper_V2] Rigidbody2D has {totalAttached} attached colliders but scratch length is {_attachedCollidersScratch.Length} " +
                    $"(GetAttachedColliders returned {written}). Raise '{nameof(_groundAttachedColliderProbeCapacity)}' so Ground probes use every hitbox.");
            }

            if (written > 0)
            {
                for (int i = 0; i < written; i++)
                {
                    Collider2D c = _attachedCollidersScratch[i];
                    if (c == null || !c.enabled || c.isTrigger)
                    {
                        continue;
                    }

                    solidCount++;
                    lowestMinY = Mathf.Min(lowestMinY, c.bounds.min.y);
                }
            }
        }

        if (float.IsPositiveInfinity(lowestMinY) && _mainCollider2D != null)
        {
            lowestMinY = _mainCollider2D.bounds.min.y;
            solidCount = Mathf.Max(1, solidCount);
            float y = lowestMinY + 0.01f + _groundProbeFootBiasWorld;
            return GroundProbeBuild.FromMainCollider(new Vector2(probeX, y), lowestMinY);
        }

        if (float.IsPositiveInfinity(lowestMinY) && written > 0 && _attachedCollidersScratch != null)
        {
            for (int i = 0; i < written; i++)
            {
                Collider2D c = _attachedCollidersScratch[i];
                if (c == null || !c.enabled)
                {
                    continue;
                }

                lowestMinY = Mathf.Min(lowestMinY, c.bounds.min.y);
            }
        }

        if (!float.IsPositiveInfinity(lowestMinY))
        {
            float y = lowestMinY + 0.01f + _groundProbeFootBiasWorld;
            return GroundProbeBuild.FromAttachedLowest(new Vector2(probeX, y), lowestMinY, solidCount);
        }

        if (_rigidbody2D != null)
        {
            Vector2 fallback = new Vector2(probeX, _rigidbody2D.position.y + 0.01f + _groundProbeFootBiasWorld);
            return GroundProbeBuild.FromRigidbodyPosition(fallback);
        }

        return GroundProbeBuild.Failed;
    }

    private bool ShouldEmitGroundProbeDebug()
    {
        if (!_debugGroundProbeLogs)
        {
            return false;
        }

        if (_debugGroundProbeLogOnlyWhenGlideOrLand && _stateMachine != null)
        {
            StickmanBodyState s = _stateMachine.CurrentState;
            if (s != StickmanBodyState.Glide && s != StickmanBodyState.GlideDie && s != StickmanBodyState.Land)
            {
                return false;
            }
        }

        float t = Time.unscaledTime;
        if (t - _lastGroundProbeDebugUnscaledTime < Mathf.Max(0.05f, _debugGroundProbeLogIntervalSeconds))
        {
            return false;
        }

        _lastGroundProbeDebugUnscaledTime = t;
        return true;
    }

    private void MaybeLogGroundProbeFailure(string probeName, GroundProbeBuild probe)
    {
        if (!ShouldEmitGroundProbeDebug())
        {
            return;
        }

        string state = _stateMachine != null ? _stateMachine.CurrentState.ToString() : "?";
        Debug.Log(
            $"[Paratrooper_V2 GroundDbg] {name} probe={probeName} state={state} " +
            $"origin=FAILED mask={_groundMask.value} ({FormatLayerMaskLayers(_groundMask)}) " +
            $"groundLayer={_groundLayer} landingPointLayer={_landingPointLayer} " +
            $"rbPos={FormatVec(_rigidbody2D != null ? (Vector2)_rigidbody2D.position : default)} " +
            $"lossyScaleY={transform.lossyScale.y:F3}");
    }

    private void MaybeLogGroundProbe(string probeName, in GroundProbeBuild probe, float rayLength, RaycastHit2D[] hits, bool passes)
    {
        if (!ShouldEmitGroundProbeDebug())
        {
            return;
        }

        string state = _stateMachine != null ? _stateMachine.CurrentState.ToString() : "?";
        float vy = _rigidbody2D != null ? _rigidbody2D.linearVelocity.y : float.NaN;
        string src = probe.Source.ToString();
        string lowest =
            float.IsNaN(probe.DebugLowestSolidMinYWorld) ? "n/a" : $"{probe.DebugLowestSolidMinYWorld:F4}";
        string anchorPos =
            _groundProbeAnchor != null ? FormatVec(_groundProbeAnchor.position) : "null";

        StringBuilder hitSummary = new StringBuilder();
        int listed = 0;
        const int maxList = 6;
        for (int i = 0; i < hits.Length && listed < maxList; i++)
        {
            Collider2D hc = hits[i].collider;
            if (hc == null)
            {
                continue;
            }

            bool self = hc.transform.IsChildOf(transform);
            bool ground = IsGroundLayer(hc.gameObject.layer);
            string why =
                self ? "self" :
                !ground ? $"layer={hc.gameObject.layer}({LayerMask.LayerToName(hc.gameObject.layer)})" :
                "GROUND_OK";
            hitSummary.Append(
                $" | hit[{listed}] {hc.name} dist={hits[i].distance:F3} {why}");
            listed++;
        }

        if (hits.Length == 0)
        {
            hitSummary.Append(" | no hits (mask empty or clear line)");
        }
        else if (hits.Length > maxList)
        {
            hitSummary.Append($" | …+{hits.Length - maxList} more");
        }

        Debug.Log(
            $"[Paratrooper_V2 GroundDbg] {name} probe={probeName} state={state} pass={passes} " +
            $"originSrc={src} origin={FormatVec(probe.Origin)} rayLen={rayLength:F3} bias={_groundProbeFootBiasWorld:F3} " +
            $"lowestSolidMinY={lowest} solidCount={probe.DebugSolidColliderCount} anchorPos={anchorPos} " +
            $"transform.pos={FormatVec(transform.position)} rb.pos={FormatVec(_rigidbody2D != null ? (Vector2)_rigidbody2D.position : default)} " +
            $"rb.vy={vy:F3} mask={_groundMask.value} ({FormatLayerMaskLayers(_groundMask)}){hitSummary}");
    }

    private static string FormatVec(Vector2 v)
    {
        return $"({v.x:F3},{v.y:F3})";
    }

    private static string FormatVec(Vector3 v)
    {
        return $"({v.x:F3},{v.y:F3},{v.z:F3})";
    }

    private static string FormatLayerMaskLayers(LayerMask mask)
    {
        if (mask.value == 0)
        {
            return "none";
        }

        var sb = new StringBuilder();
        for (int i = 0; i < 32; i++)
        {
            if ((mask.value & (1 << i)) == 0)
            {
                continue;
            }

            string layerName = LayerMask.LayerToName(i);
            if (sb.Length > 0)
            {
                sb.Append(',');
            }

            sb.Append(string.IsNullOrEmpty(layerName) ? $"#{i}" : layerName);
        }

        return sb.Length == 0 ? "none" : sb.ToString();
    }

    private bool IsGroundLayer(int layer)
    {
        if (_groundLayer < 0)
        {
            _groundLayer = LayerMask.NameToLayer("Ground");
        }

        if (_landingPointLayer < 0)
        {
            _landingPointLayer = LayerMask.NameToLayer("LandingPoint");
        }

        if (_groundLayer >= 0 && layer == _groundLayer)
        {
            return true;
        }

        return _landingPointLayer >= 0 && layer == _landingPointLayer;
    }

    private void ValidateBodyPartSetup()
    {
        if (_bodyParts == null || _bodyParts.Length == 0)
        {
            _bodyParts = GetComponentsInChildren<ParatrooperBodyPart_V2>(true);
        }

        int total = _bodyParts != null ? _bodyParts.Length : 0;
        int missingCollider = 0;
        int missingReceiver = 0;
        int wrongLayer = 0;
        int autoFixedLayer = 0;
        int expectedLayer = LayerMask.NameToLayer("EnemyBodyPart");

        for (int i = 0; i < total; i++)
        {
            var part = _bodyParts[i];
            if (part == null)
            {
                continue;
            }

            if (part.GetComponent<Collider2D>() == null)
            {
                missingCollider++;
                Debug.LogWarning($"[Paratrooper_V2] Body part '{part.name}' has no Collider2D.");
            }

            if (part.GetComponentInParent<ParatrooperDamageReceiver_V2>() == null)
            {
                missingReceiver++;
                Debug.LogWarning($"[Paratrooper_V2] Body part '{part.name}' cannot find ParatrooperDamageReceiver_V2 in parents.");
            }

            if (expectedLayer >= 0 && part.gameObject.layer != expectedLayer)
            {
                wrongLayer++;
                if (_autoFixBodyPartLayerToEnemyBodyPart)
                {
                    part.gameObject.layer = expectedLayer;
                    autoFixedLayer++;
                }
                else
                {
                    Debug.LogWarning($"[Paratrooper_V2] Body part '{part.name}' layer is '{LayerMask.LayerToName(part.gameObject.layer)}', expected 'EnemyBodyPart'.");
                }
            }
        }

        string autoFixSuffix = _autoFixBodyPartLayerToEnemyBodyPart ? $", autoFixedLayer={autoFixedLayer}" : string.Empty;
        Debug.Log($"[Paratrooper_V2] BodyPart setup: total={total}, missingCollider={missingCollider}, missingReceiver={missingReceiver}, wrongLayer={wrongLayer}{autoFixSuffix}");
    }

    private void LogColliderSummary()
    {
        if (_bodyParts == null || _bodyParts.Length == 0)
        {
            _bodyParts = GetComponentsInChildren<ParatrooperBodyPart_V2>(true);
        }

        int totalBodyParts = _bodyParts != null ? _bodyParts.Length : 0;
        int activeBodyPartColliders = 0;
        int totalPolygonColliders = 0;
        int enabledPolygonColliders = 0;

        for (int i = 0; i < totalBodyParts; i++)
        {
            var part = _bodyParts[i];
            if (part == null)
            {
                continue;
            }

            var polygonColliders = part.GetComponents<PolygonCollider2D>();
            bool bodyPartHasEnabledCollider = false;

            for (int c = 0; c < polygonColliders.Length; c++)
            {
                var polygonCollider = polygonColliders[c];
                if (polygonCollider == null)
                {
                    continue;
                }

                totalPolygonColliders++;
                if (polygonCollider.enabled)
                {
                    enabledPolygonColliders++;
                    bodyPartHasEnabledCollider = true;
                }
            }

            if (bodyPartHasEnabledCollider)
            {
                activeBodyPartColliders++;
            }
        }

        string summary =
            $"[Paratrooper_V2] Collider summary: active bodypart colliders={activeBodyPartColliders}/{totalBodyParts}, " +
            $"enabled polygon colliders={enabledPolygonColliders}/{totalPolygonColliders}";

        if (_onlyLogWhenSummaryChanges && summary == _lastColliderSummary)
        {
            return;
        }

        _lastColliderSummary = summary;
        Debug.Log(summary);

        if (_warnWhenColliderSummaryIsNotFull && activeBodyPartColliders < totalBodyParts)
        {
            Debug.LogWarning(
                $"[Paratrooper_V2] Collider warning: active bodypart colliders dropped to {activeBodyPartColliders}/{totalBodyParts}.");
        }
    }
}
}
