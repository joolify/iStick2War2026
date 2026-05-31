using System;
using System.Reflection;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using iStick2War;

namespace iStick2War_V2
{
    /*
 * HeroView_V2 (Presentation Layer)
 *
 * PURPOSE:
 * HeroView_V2 is responsible for all visual representation of the Hero.
 * It reacts to gameplay state changes and translates them into visuals.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * The View layer MUST NOT contain any gameplay logic.
 * It is strictly responsible for presentation only.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Make gameplay decisions
 * - Read input
 * - Know or enforce gameplay rules
 * - Contain state machine logic
 * - Decide when actions like shooting, reloading, or moving happen
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Play animations (Spine / sprites)
 * - Render visual effects (VFX)
 * - Handle visual flipping
 * - React to events from gameplay systems
 *
 * ---------------------------------------------------------
 * ARCHITECTURE NOTE:
 *
 * HeroView_V2:
 * - Only plays animations
 * - Only handles visual flipping
 * - Contains no gameplay logic
  */
    public class HeroView_V2 : MonoBehaviour
    {
        private struct WeaponAnimationSet
        {
            public AnimationReferenceAsset Idle;
            public AnimationReferenceAsset Aim;
            public AnimationReferenceAsset Shoot;
            public AnimationReferenceAsset Run;
            public AnimationReferenceAsset Jump;
            public AnimationReferenceAsset Reload;
            public AnimationReferenceAsset DryFire;
        }

        public SkeletonAnimation _skeletonAnimation;

        private HeroStateMachine_V2 _stateMachine;
        private HeroModel_V2 _model;
        private HeroDamageReceiver_V2 _damageReceiver;
        private HeroDeathHandler_V2 _deathHandler;
        public Bone _crossHairBone;
        public Bone _aimPointBone;
        public Bone _flamethrowerAimBone;
        private bool _facingRight;

        [SpineBone(dataField: "skeletonAnimation")] public string aimPointBoneName;

        [SpineBone(dataField: "skeletonAnimation")] public string crossHairBoneName;
        [SpineBone(dataField: "skeletonAnimation")] public string flamethrowerAimBoneName = "flamegun-aim";

        private Vector2 _touchPos;

        /// <summary>When set, replaces mouse/touch aim for crosshair and facing (used by <see cref="AutoHero_V2"/>).</summary>
        private Vector2? _overrideAimWorld;

        [SerializeField] private Camera _cam;

        private bool _isInitialized;
        private bool _shootLocomotionIsMoving;
        private bool _shootLocomotionInitialized;
        private bool _jumpCombatIsShooting;
        private bool _jumpCombatInitialized;

        [Header("Animations")]
        [SerializeField] private HeroWeaponDefinition_V2 _fallbackWeaponDefinition;
        public AnimationReferenceAsset _landFallDownBackAnim;
        [Header("Flamethrower animation override (optional)")]
        public AnimationReferenceAsset aimFlamethrowerAnim;
        public AnimationReferenceAsset grenadeFlamethrowerAnim;
        public AnimationReferenceAsset idleFlamethrowerAnim;
        public AnimationReferenceAsset jumpFlamethrowerAnim;
        public AnimationReferenceAsset runFlamethrowerAnim;
        public AnimationReferenceAsset shootingFlamethrowerAnim;
        [Header("Flamethrower VFX (optional)")]
        [SerializeField] private ParticleSystem _flamethrowerTestPs;
        [SerializeField] private float _flamethrowerVfxAngleOffsetDegrees;
        [Header("Flamethrower spray tuning")]
        [Tooltip("Max flamethrower reach as a fraction of orthographic view edge distance along the aim ray (0.7 = ~70% of game view).")]
        [SerializeField] private float _flamethrowerViewReachFraction = HeroCombatCameraReach_V2.DefaultFlamethrowerViewReachFraction;
        [SerializeField] private float _flamethrowerSprayDistanceMeters = 10f;
        [SerializeField] private float _flamethrowerSprayTravelSeconds = 0.22f;
        [SerializeField] private bool _overrideFlamethrowerParticleSettings = false;
        [SerializeField] private bool _autoTuneFlamethrowerParticleSpeed = true;
        [SerializeField] private bool _autoConfigureFlamethrowerAsBeam = true;
        [SerializeField] private float _flamethrowerBeamSpreadAngle = 1.2f;
        [SerializeField] private float _flamethrowerBeamStartSize = 0.06f;
        [SerializeField] private float _flamethrowerBeamEmissionRate = 420f;
        [SerializeField] private bool _autoConfigureFlamethrowerBeamRenderer = true;
        [SerializeField] private float _flamethrowerBeamLengthScale = 12f;
        [SerializeField] private float _flamethrowerBeamSpeedScale = 1.6f;
        [SerializeField] private bool _lockFlamethrowerDirectionWhileShooting = false;
        [SerializeField] private bool _driveFlamethrowerForceFromAim = true;
        [SerializeField] private float _flamethrowerAimForceStrength = 20f;
        private Vector2 _lockedFlamethrowerDirection = Vector2.right;
        private bool _hasLockedFlamethrowerDirection;

        internal float FlamethrowerViewReachFraction =>
            Mathf.Clamp01(_flamethrowerViewReachFraction);

        [Header("Tesla animation overrides (optional)")]
        [Tooltip("When the active weapon is Tesla, non-null entries replace the weapon-definition set (grenade: reserved for future grenade playback).")]
        public AnimationReferenceAsset aimTeslaAnim;
        public AnimationReferenceAsset grenadeTeslaAnim;
        public AnimationReferenceAsset idleTeslaAnim;
        public AnimationReferenceAsset jumpTeslaAnim;
        public AnimationReferenceAsset runTeslaAnim;
        public AnimationReferenceAsset shootingTeslaAnim;

        [Space(10)]
        [Header("Tesla blixt (LightningBolt2D)")]
        [Tooltip("Dra hit LightningBolt2D-komponenten (objektet med blixteffekten).")]
        [SerializeField] private MonoBehaviour teslaLightningBolt;
        [SerializeField] private bool _syncTeslaLightningSortingWithTrail = true;
        [System.NonSerialized] private TeslaLightningBoltCache _teslaLightningCache;

        [Header("VFX")]
        [SerializeField] private Transform _trailPrefab;
        [SerializeField] private float _trailWidth = 0.06f;
        [SerializeField] private float _trailVisibleDuration = 0.12f;
        [SerializeField] private bool _overrideTrailColor = false;
        [SerializeField] private Color _trailColor = Color.white;
        [SerializeField] private int _trailSortingOrder = 5000;
        [Tooltip("If set, trail uses this sorting layer. Empty = highest sorting layer in project.")]
        [SerializeField] private string _trailSortingLayerName = "";
        [SerializeField] private bool _debugTrailLogs = false;
        [SerializeField] private bool _debugViewLogs = false;

        [Header("Hero muzzle flash (optional)")]
        [Tooltip("Hero-local muzzle flash prefab. If empty, HeroController falls back to scene-level MuzzleFlash_V2.")]
        [SerializeField] private GameObject _muzzleFlashPrefab;
        [Tooltip(
            "Added to atan2(direction). Use 0 if prefab points along +X/right at rotation 0. " +
            "Use -90 if prefab art points up at rotation 0.")]
        [SerializeField] private float _muzzleFlashRotationOffsetDegrees;
        [SerializeField] private float _muzzleFlashWorldZ = 0f;
        [SerializeField] private float _muzzleFlashLifetimeSeconds = 0.08f;
        [SerializeField] private Vector2 _muzzleFlashRandomScaleRange = new Vector2(0.9f, 1.15f);
        [SerializeField] private Transform _muzzleFlashParent;

        [Header("Hero shell casings (optional)")]
        [Tooltip("Prefab for Colt45/Thompson shell casings. Spawned through SimplePrefabPool_V2.")]
        [SerializeField] private GameObject _shellCasingPrefab;
        [Tooltip(
            "Added to atan2(eject direction). Use 0 if prefab points along +X/right at rotation 0. " +
            "If your casing art lies sideways but points 90 degrees to the right, try -90 or 90.")]
        [SerializeField] private float _shellCasingRotationOffsetDegrees;
        [SerializeField] private float _shellCasingWorldZ = 0f;
        [SerializeField] private float _shellCasingLifetimeSeconds = 2.4f;
        [SerializeField] private Transform _shellCasingParent;
        [SerializeField] private Vector2 _shellCasingLocalSpawnOffset = new Vector2(-0.08f, 0.03f);
        [SerializeField] private Vector2 _shellCasingBackwardSpeedRange = new Vector2(1.2f, 2.1f);
        [SerializeField] private Vector2 _shellCasingUpwardSpeedRange = new Vector2(1.3f, 2.4f);
        [SerializeField] private Vector2 _shellCasingAngularVelocityRange = new Vector2(420f, 820f);

        [Header("Visual recoil (presentation only)")]
        [SerializeField] private float _recoilRecoverySpeed = 18f;
        [SerializeField] private float _colt45RecoilBack = 0.045f;
        [SerializeField] private float _colt45RecoilUp = 0.015f;
        [SerializeField] private float _colt45RecoilAngleZ = 1.8f;
        [SerializeField] private float _thompsonRecoilBack = 0.06f;
        [SerializeField] private float _thompsonRecoilUp = 0.03f;
        [SerializeField] private float _thompsonRecoilAngleZ = 2.2f;
        [SerializeField] private float _bazookaRecoilBack = 0.14f;
        [SerializeField] private float _bazookaRecoilUp = 0.02f;
        [SerializeField] private float _bazookaRecoilAngleZ = 5.5f;
        [SerializeField] private float _teslaVibrateAmplitude = 0.018f;
        [SerializeField] private float _teslaVibrateAngleZ = 0.9f;
        [SerializeField] private float _flamethrowerRecoilBack = 0.025f;
        [SerializeField] private float _flamethrowerRecoilUp = 0.008f;
        [SerializeField] private float _flamethrowerRecoilAngleZ = 0.8f;

        [Header("Hero damage VFX (optional)")]
        [Tooltip("Small blood or impact burst; leave empty to skip.")]
        [SerializeField] private GameObject _heroBloodHitPrefab;
        [SerializeField] private Vector2 _heroBloodWorldOffset = new Vector2(0f, 0.35f);
        [SerializeField] private float _heroBloodReferenceDamage = 14f;

        private Vector3 _recoilOffsetLocal;
        private float _recoilAngleLocalZ;
        private Vector3 _recoilBaseLocalPosition;
        private Quaternion _recoilBaseLocalRotation;
        private bool _recoilBaseCached;

        // -------------------------
        // INIT
        // -------------------------
        public void Init(
            HeroModel_V2 model,
            HeroStateMachine_V2 stateMachine,
            HeroDamageReceiver_V2 damageReceiver,
            HeroDeathHandler_V2 deathHandler,
            SkeletonAnimation skeletonAnimation)
        {
            _model = model;
            _stateMachine = stateMachine;
            _damageReceiver = damageReceiver;
            _deathHandler = deathHandler;
            _skeletonAnimation = skeletonAnimation;

            _crossHairBone = _skeletonAnimation.Skeleton.FindBone("crosshair");
            _facingRight = _skeletonAnimation.Skeleton.ScaleX >= 0f;
            _recoilBaseCached = false;
            _recoilOffsetLocal = Vector3.zero;
            _recoilAngleLocalZ = 0f;

            if (_crossHairBone == null)
                Debug.LogError("Crosshair bone not found in skeleton!");

            // Subscribe to events
            stateMachine.OnStateChanged += HandleStateChanged;
            damageReceiver.OnDamageTaken += HandleDamageTaken;
            deathHandler.OnDeathHandled += HandleDeath;

            _isInitialized = true;
            StopFlamethrowerVfxIfActive();

            if (_debugViewLogs)
            {
                Debug.Log("HeroView_V2 initialized successfully");
            }
        }

        void Start()
        {
            if (_cam == null)
                Debug.LogError("Cam not found!");

            if (!string.IsNullOrEmpty(aimPointBoneName))
            {
                _aimPointBone = _skeletonAnimation.Skeleton.FindBone(aimPointBoneName);
            }

            if (_aimPointBone == null)
            {
                Debug.LogError("Aim bone not found!");
            }

            if (!string.IsNullOrEmpty(crossHairBoneName))
            {
                _crossHairBone = _skeletonAnimation.Skeleton.FindBone(crossHairBoneName);
            }

            if (_crossHairBone == null)
            {
                Debug.LogError("Cross hair bone not found!");
            }

            if (!string.IsNullOrEmpty(flamethrowerAimBoneName))
            {
                _flamethrowerAimBone = _skeletonAnimation.Skeleton.FindBone(flamethrowerAimBoneName);
            }

            if (_flamethrowerAimBone == null)
            {
                Debug.LogWarning($"Flamethrower aim bone '{flamethrowerAimBoneName}' not found.");
            }

            // Ensure desktop aim is active from frame 1, even before first input/state transition.
            AnimationReferenceAsset idle = GetIdleAnimationForCurrentWeapon();
            if (idle != null)
            {
                PlayLoop(idle);
            }
            PlayAimLoop();
            StopFlamethrowerVfxIfActive();
            StopTeslaLightningVfxIfActive();
        }

        void Update()
        {
            if (!_isInitialized)
                return;

            if (IsDead())
                return;

            _touchPos = ResolveAimWorldPoint();
            FaceTowardWorldX(_touchPos);
            SetCrosshair(_touchPos);
            UpdateFlamethrowerVfxPose();
            TickVisualRecoil();

            // Runtime failsafe: even if ParticleSystem has Play On Awake, never show flamethrower
            // unless controller currently holds shoot.
            if (_model == null ||
                _model.currentWeaponType != WeaponType.Flamethrower ||
                !_model.isShootingPressed)
            {
                StopFlamethrowerVfxIfActive();
            }
        }

        /// <summary>Optional world-space aim target for automated playtests.</summary>
        public void SetAutoAimWorldOverride(Vector2? worldPoint)
        {
            _overrideAimWorld = worldPoint;
        }

        private Vector2 ResolveAimWorldPoint()
        {
            Vector2 aim = _overrideAimWorld ?? ReadPointerAimWorldPoint();
            if (_model != null && _model.currentWeaponType == WeaponType.Flamethrower)
            {
                aim = ClampFlamethrowerAimWorldPoint(aim);
            }

            return aim;
        }

        private Vector2 ReadPointerAimWorldPoint()
        {
            Camera cam = _cam != null ? _cam : Camera.main;
            if (cam == null)
            {
                return transform.position;
            }

            Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(w.x, w.y);
        }

        private Vector2 ClampFlamethrowerAimWorldPoint(Vector2 rawAimWorld)
        {
            if (!TryGetFlamethrowerPose(out Vector2 origin, out _))
            {
                return rawAimWorld;
            }

            Vector2 toAim = rawAimWorld - origin;
            float dist = toAim.magnitude;
            if (dist <= 0.0001f)
            {
                return rawAimWorld;
            }

            Vector2 dir = toAim / dist;
            Camera cam = _cam != null ? _cam : Camera.main;
            if (!HeroCombatCameraReach_V2.TryGetReachPoint(
                    cam,
                    origin,
                    dir,
                    Mathf.Clamp01(_flamethrowerViewReachFraction),
                    out _,
                    out float maxReach))
            {
                return rawAimWorld;
            }

            return dist <= maxReach ? rawAimWorld : origin + dir * maxReach;
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= HandleStateChanged;

            if (_damageReceiver != null)
                _damageReceiver.OnDamageTaken -= HandleDamageTaken;

            if (_deathHandler != null)
                _deathHandler.OnDeathHandled -= HandleDeath;
        }

        internal void PlayVisualRecoil(WeaponType weaponType, Vector2 shotDirectionWorld)
        {
            Transform target = ResolveRecoilTarget();
            if (target == null)
            {
                return;
            }

            if (!_recoilBaseCached)
            {
                _recoilBaseLocalPosition = target.localPosition;
                _recoilBaseLocalRotation = target.localRotation;
                _recoilBaseCached = true;
            }

            Vector2 dir = shotDirectionWorld.sqrMagnitude > 0.0001f ? shotDirectionWorld.normalized : Vector2.right;
            Vector3 worldBack = -new Vector3(dir.x, dir.y, 0f);
            Vector3 localBack = target.parent != null ? target.parent.InverseTransformDirection(worldBack) : worldBack;
            Vector3 localUp = target.parent != null ? target.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            localBack.z = 0f;
            localUp.z = 0f;
            localBack = localBack.sqrMagnitude > 0.0001f ? localBack.normalized : Vector3.left;
            localUp = localUp.sqrMagnitude > 0.0001f ? localUp.normalized : Vector3.up;

            float back = 0f;
            float up = 0f;
            float angle = 0f;
            if (weaponType == WeaponType.Colt45)
            {
                back = _colt45RecoilBack;
                up = _colt45RecoilUp;
                angle = _colt45RecoilAngleZ;
            }
            else if (weaponType == WeaponType.Thompson)
            {
                back = _thompsonRecoilBack;
                up = _thompsonRecoilUp;
                angle = _thompsonRecoilAngleZ;
            }
            else if (weaponType == WeaponType.Bazooka)
            {
                back = _bazookaRecoilBack;
                up = _bazookaRecoilUp;
                angle = _bazookaRecoilAngleZ;
            }
            else if (weaponType == WeaponType.Tesla)
            {
                Vector2 jitter = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, _teslaVibrateAmplitude);
                _recoilOffsetLocal += new Vector3(jitter.x, jitter.y, 0f);
                _recoilAngleLocalZ += UnityEngine.Random.Range(-_teslaVibrateAngleZ, _teslaVibrateAngleZ);
                return;
            }
            else if (weaponType == WeaponType.Flamethrower)
            {
                back = _flamethrowerRecoilBack;
                up = _flamethrowerRecoilUp;
                angle = _flamethrowerRecoilAngleZ;
            }
            else
            {
                return;
            }

            _recoilOffsetLocal += localBack * Mathf.Max(0f, back);
            _recoilOffsetLocal += localUp * Mathf.Max(0f, up);
            _recoilAngleLocalZ += Mathf.Sign(-dir.x) * Mathf.Max(0f, angle);
        }

        private void TickVisualRecoil()
        {
            Transform target = ResolveRecoilTarget();
            if (target == null)
            {
                return;
            }

            if (!_recoilBaseCached)
            {
                _recoilBaseLocalPosition = target.localPosition;
                _recoilBaseLocalRotation = target.localRotation;
                _recoilBaseCached = true;
            }

            float lerp = 1f - Mathf.Exp(-Mathf.Max(0.01f, _recoilRecoverySpeed) * Mathf.Max(0f, Time.deltaTime));
            _recoilOffsetLocal = Vector3.Lerp(_recoilOffsetLocal, Vector3.zero, lerp);
            _recoilAngleLocalZ = Mathf.Lerp(_recoilAngleLocalZ, 0f, lerp);

            target.localPosition = _recoilBaseLocalPosition + _recoilOffsetLocal;
            target.localRotation = _recoilBaseLocalRotation * Quaternion.Euler(0f, 0f, _recoilAngleLocalZ);
        }

        private Transform ResolveRecoilTarget()
        {
            if (_skeletonAnimation != null)
            {
                return _skeletonAnimation.transform;
            }

            return transform;
        }

        // -------------------------
        // STATE → ANIMATION
        // -------------------------
        private void HandleStateChanged(HeroState from, HeroState to)
        {
            if (_debugViewLogs)
            {
                Debug.Log("HandleStateChanged: Hero state changed from " + from + " to " + to);
            }

            switch (to)
            {
                case HeroState.Idle:
                    StopFlamethrowerVfxIfActive();
                    PlayLoop(GetIdleAnimationForCurrentWeapon());
                    PlayAimLoop();
                    _jumpCombatInitialized = false;
                    break;

                //case HeroState.Moving:
                //    PlayLoop("run");
                //    break;

                case HeroState.Shooting:
                    // Base locomotion while shooting is controlled live by controller.
                    PlayLoop(GetIdleAnimationForCurrentWeapon());
                    _shootLocomotionInitialized = false;
                    _jumpCombatInitialized = false;
                    break;

                //case HeroState.Reloading:
                //    PlayOneShot("reload");
                //    break;

                //case HeroState.Dead:
                //    PlayOneShot("dead");
                //    break;

                case HeroState.Moving:
                    StopFlamethrowerVfxIfActive();
                    PlayLoop(GetRunAnimationForCurrentWeapon());
                    PlayAimLoop();
                    _jumpCombatInitialized = false;
                    break;

                case HeroState.Jumping:
                    StopFlamethrowerVfxIfActive();
                    AnimationReferenceAsset jumpAnim = GetJumpAnimationForCurrentWeapon();
                    if (jumpAnim != null)
                    {
                        PlayLoop(jumpAnim);
                    }
                    else
                    {
                        PlayLoop(GetRunAnimationForCurrentWeapon());
                    }
                    _jumpCombatInitialized = false;
                    break;

                case HeroState.Dead:
                    StopFlamethrowerVfxIfActive();
                    StopTeslaLightningVfxIfActive();
                    PlayDeathAnimation();
                    break;
            }
        }

        // -------------------------
        // DAMAGE VISUALS
        // -------------------------
        private void HandleDamageTaken(int damage)
        {
            // hit flash / recoil animation / vignette trigger etc
            if (_debugViewLogs)
            {
                Debug.Log($"Hero took {damage} damage");
            }
        }

        // -------------------------
        // FLIP (vänster/höger)
        // -------------------------
        public void Flip(float direction)
        {
            var scale = transform.localScale;
            scale.x = Mathf.Sign(direction) * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        // -------------------------
        // DEATH VISUALS
        // -------------------------
        private void HandleDeath()
        {
            // extra VFX layer (camera shake, particles, sound trigger)
            PlayDeathAnimation();
            if (_debugViewLogs)
            {
                Debug.Log("Hero death visuals triggered");
            }
        }

        private void PlayDeathAnimation()
        {
            if (_skeletonAnimation == null)
            {
                return;
            }

            _skeletonAnimation.AnimationState.ClearTrack(1);

            if (_landFallDownBackAnim != null)
            {
                _skeletonAnimation.AnimationState.SetAnimation(0, _landFallDownBackAnim, false);
                return;
            }

            // Fallback so Hero does not snap to an unrelated loop if death clip is not assigned yet.
            AnimationReferenceAsset fallbackIdle = GetFallbackAnimationSet().Idle;
            if (fallbackIdle != null)
            {
                _skeletonAnimation.AnimationState.SetAnimation(0, fallbackIdle, true);
            }
        }

        // -------------------------
        // HELPERS
        // -------------------------
        private void PlayLoop(AnimationReferenceAsset anim)
        {
            if (anim == null || _skeletonAnimation == null)
            {
                return;
            }
            _skeletonAnimation.AnimationState.SetAnimation(0, anim, true);
        }

        private void PlayOneShot(AnimationReferenceAsset anim)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, anim, false);
            _skeletonAnimation.AnimationState.AddAnimation(0, "idle", true, 0f);
        }

        private void PlayAimLoop()
        {
            if (IsDead())
            {
                return;
            }

            AnimationReferenceAsset aimAnim = GetAimAnimationForCurrentWeapon();
            if (aimAnim == null)
            {
                return;
            }

            _skeletonAnimation.AnimationState.SetAnimation(1, aimAnim, true);
        }

        private void SetCrosshair(Vector2 worldAimPos)
        {
            if (IsDead())
            {
                return;
            }

            if (_skeletonAnimation == null)
                Debug.LogError("SkeletonAnimation not found!");

            // World aim must share the skeleton's Z so InverseTransformPoint matches the same plane as
            // aim/crosshair bone math (Vector2 was effectively z=0 and skews local XY when the rig is
            // offset in Z or the transform has rotation).
            Vector3 worldAim3 = new Vector3(
                worldAimPos.x,
                worldAimPos.y,
                _skeletonAnimation.transform.position.z);
            var skeletonSpacePoint = _skeletonAnimation.transform.InverseTransformPoint(worldAim3);
            skeletonSpacePoint.x *= _skeletonAnimation.Skeleton.ScaleX;
            skeletonSpacePoint.y *= _skeletonAnimation.Skeleton.ScaleY;
            _crossHairBone.SetLocalPosition(skeletonSpacePoint);
        }

        private void FaceTowardWorldX(Vector2 worldTarget)
        {
            if (IsDead())
            {
                return;
            }

            // Desktop-only cursor flip. Mobile/touch uses different aim handling unless bot override is active.
#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
            if (!_overrideAimWorld.HasValue)
            {
                return;
            }
#endif
            Camera cam = _cam != null ? _cam : Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 delta = new Vector3(worldTarget.x, worldTarget.y, transform.position.z) - transform.position;

            if (delta.x > 0f && !_facingRight)
            {
                _skeletonAnimation.Skeleton.ScaleX *= -1f;
                _facingRight = true;
            }
            else if (delta.x < 0f && _facingRight)
            {
                _skeletonAnimation.Skeleton.ScaleX *= -1f;
                _facingRight = false;
            }
        }

        private bool IsDead()
        {
            if (_model != null && _model.isDead)
            {
                return true;
            }

            return _stateMachine != null && _stateMachine.CurrentState == HeroState.Dead;
        }

        public bool TryGetAimData(out Vector2 origin, out Vector2 direction)
        {
            origin = default;
            direction = default;

            if (_skeletonAnimation == null || _aimPointBone == null || _crossHairBone == null)
            {
                Debug.LogWarning($"[HeroView_V2] TryGetAimData failed. skeleton={_skeletonAnimation != null}, aimBone={_aimPointBone != null}, crossHairBone={_crossHairBone != null}");
                return false;
            }

            Vector2 aimPos = _skeletonAnimation.transform.TransformPoint(
                new Vector3(_aimPointBone.WorldX, _aimPointBone.WorldY, 0f)
            );
            Vector2 crossPos = _skeletonAnimation.transform.TransformPoint(
                new Vector3(_crossHairBone.WorldX, _crossHairBone.WorldY, 0f)
            );

            Vector2 shotDirection = crossPos - aimPos;
            if (shotDirection.sqrMagnitude <= 0.0001f)
            {
                Debug.LogWarning($"[HeroView_V2] TryGetAimData failed. Direction too small. aimPos={aimPos}, crossPos={crossPos}");
                return false;
            }

            origin = aimPos;
            direction = shotDirection.normalized;
            return true;
        }

        internal void PlayHitEffect(int obj)
        {
            HandleDamageTaken(obj);
        }

        internal void PlayHitBloodVfx(int damage, Vector2? incomingShotWorldDirection)
        {
            if (_heroBloodHitPrefab == null || damage <= 0 || IsDead())
            {
                return;
            }

            Vector2 origin = (_model != null ? (Vector2)_model.transform.position : (Vector2)transform.position) +
                _heroBloodWorldOffset;
            Vector2 dir = incomingShotWorldDirection ?? Vector2.left;
            BloodHitVfx_V2.Spawn(
                _heroBloodHitPrefab,
                origin,
                dir,
                damage,
                _heroBloodReferenceDamage);
        }

        internal void PlayDeathEffect()
        {
            HandleDeath();
        }

        internal void PlayShoot()
        {
            AnimationReferenceAsset shootAnim = GetShootAnimationForCurrentWeapon();
            if (shootAnim == null)
            {
                return;
            }

            _skeletonAnimation.AnimationState.SetAnimation(1, shootAnim, true);
            TryPlayFlamethrowerVfx();
        }

        internal void UpdateShootLocomotion(bool isMoving)
        {
            if (_stateMachine == null || _stateMachine.CurrentState != HeroState.Shooting)
            {
                return;
            }

            if (_shootLocomotionInitialized && _shootLocomotionIsMoving == isMoving)
            {
                return;
            }

            _shootLocomotionIsMoving = isMoving;
            _shootLocomotionInitialized = true;
            PlayLoop(isMoving ? GetRunAnimationForCurrentWeapon() : GetIdleAnimationForCurrentWeapon());
        }

        internal void UpdateJumpCombatOverlay(bool isShooting)
        {
            if (_stateMachine == null || _stateMachine.CurrentState != HeroState.Jumping)
            {
                return;
            }

            if (_jumpCombatInitialized && _jumpCombatIsShooting == isShooting)
            {
                return;
            }

            _jumpCombatIsShooting = isShooting;
            _jumpCombatInitialized = true;

            // Keep jump visible on base track while still allowing air aiming.
            PlayAimLoop();
        }

        /// <summary>
        /// Draws imported LightningBolt2D VFX between shot origin and impact. Returns true if a valid bolt component is assigned.
        /// </summary>
        internal bool TryPlayTeslaLightningForShot(Vector2 worldStart, Vector2 worldEnd)
        {
            TeslaLightningBoltCache cache = ResolveTeslaLightningCache();
            if (cache == null)
            {
                return false;
            }

            cache.PlayShot(
                worldStart,
                worldEnd,
                _syncTeslaLightningSortingWithTrail,
                ResolveTrailSortingLayerId(),
                _trailSortingOrder);
            return true;
        }

        private TeslaLightningBoltCache ResolveTeslaLightningCache()
        {
            if (teslaLightningBolt == null)
            {
                _teslaLightningCache = null;
                return null;
            }

            if (_teslaLightningCache == null || !ReferenceEquals(_teslaLightningCache.Target, teslaLightningBolt))
            {
                _teslaLightningCache = new TeslaLightningBoltCache(teslaLightningBolt);
            }

            return _teslaLightningCache.IsValid ? _teslaLightningCache : null;
        }

        private void StopTeslaLightningVfxIfActive()
        {
            if (teslaLightningBolt == null)
            {
                return;
            }

            TeslaLightningBoltCache cache = ResolveTeslaLightningCache();
            if (cache != null)
            {
                cache.Stop();
            }
            else
            {
                teslaLightningBolt.gameObject.SetActive(false);
            }
        }

        internal void PlayShotTrail(Vector2 origin, Vector2 finalPos)
        {
            if (_trailPrefab == null)
            {
                Debug.LogWarning("[HeroView_V2] PlayShotTrail skipped: trail prefab is not assigned.");
                return;
            }

            Transform trail = Instantiate(_trailPrefab, origin, Quaternion.identity);
            LineRenderer lr = trail.GetComponent<LineRenderer>();

            if (lr != null)
            {
                Color tint = _overrideTrailColor ? _trailColor : Color.white;
                lr.positionCount = 0; // clear prefab state
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.widthMultiplier = Mathf.Max(0.01f, _trailWidth);
                lr.startWidth = lr.widthMultiplier;
                lr.endWidth = lr.widthMultiplier;
                lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.View;
                lr.numCapVertices = 2;
                lr.startColor = new Color(tint.r, tint.g, tint.b, 1f);
                lr.endColor = new Color(tint.r, tint.g, tint.b, 1f);
                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(tint, 0f),
                        new GradientColorKey(tint, 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    });
                lr.colorGradient = gradient;
                lr.sortingOrder = Mathf.Max(5000, _trailSortingOrder);
                lr.sortingLayerID = ResolveTrailSortingLayerId();
                lr.enabled = true;

                Shader preferred = Shader.Find("Universal Render Pipeline/Unlit");
                if (preferred == null)
                {
                    preferred = Shader.Find("Sprites/Default");
                }
                if (preferred != null)
                {
                    Material runtimeMat = new Material(preferred);
                    ConfigureTrailMaterial(runtimeMat, tint);
                    lr.material = runtimeMat;
                }
                lr.SetPosition(0, origin);
                lr.SetPosition(1, finalPos);
                if (_debugTrailLogs)
                {
                    Debug.Log($"[HeroView_V2] PlayShotTrail OK. origin={origin}, finalPos={finalPos}, startColor={lr.startColor}, endColor={lr.endColor}, overrideTrailColor={_overrideTrailColor}, trailColor={_trailColor}");
                }
            }
            else
            {
                Debug.LogWarning($"[HeroView_V2] PlayShotTrail: LineRenderer missing on prefab '{_trailPrefab.name}'.");
            }

            // Keep same lifetime as legacy GunBase effect.
            Destroy(trail.gameObject, Mathf.Max(0.05f, _trailVisibleDuration));
        }

        public bool TryPlayMuzzleFlash(Vector2 origin, Vector2 direction)
        {
            if (_muzzleFlashPrefab == null)
            {
                return false;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + _muzzleFlashRotationOffsetDegrees;
            Vector3 pos = new Vector3(origin.x, origin.y, _muzzleFlashWorldZ);
            GameObject go = Instantiate(
                _muzzleFlashPrefab,
                pos,
                Quaternion.Euler(0f, 0f, angle),
                _muzzleFlashParent);

            float minScale = Mathf.Min(_muzzleFlashRandomScaleRange.x, _muzzleFlashRandomScaleRange.y);
            float maxScale = Mathf.Max(_muzzleFlashRandomScaleRange.x, _muzzleFlashRandomScaleRange.y);
            float scale = UnityEngine.Random.Range(Mathf.Max(0.01f, minScale), Mathf.Max(0.01f, maxScale));
            go.transform.localScale = go.transform.localScale * scale;
            Destroy(go, Mathf.Max(0.01f, _muzzleFlashLifetimeSeconds));
            return true;
        }

        public bool TryEjectShellCasing(WeaponType weaponType, Vector2 origin, Vector2 direction)
        {
            if (_shellCasingPrefab == null || !ShouldEjectShellCasingForWeapon(weaponType))
            {
                return false;
            }

            Vector2 shotDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            Vector2 spawnOffset =
                (shotDir * _shellCasingLocalSpawnOffset.x) +
                (Vector2.up * _shellCasingLocalSpawnOffset.y);
            Vector2 spawnPos = origin + spawnOffset;

            float backSpeed = UnityEngine.Random.Range(
                Mathf.Min(_shellCasingBackwardSpeedRange.x, _shellCasingBackwardSpeedRange.y),
                Mathf.Max(_shellCasingBackwardSpeedRange.x, _shellCasingBackwardSpeedRange.y));
            float upSpeed = UnityEngine.Random.Range(
                Mathf.Min(_shellCasingUpwardSpeedRange.x, _shellCasingUpwardSpeedRange.y),
                Mathf.Max(_shellCasingUpwardSpeedRange.x, _shellCasingUpwardSpeedRange.y));
            Vector2 ejectVelocity = (-shotDir * Mathf.Max(0f, backSpeed)) + (Vector2.up * Mathf.Max(0f, upSpeed));

            float angle = Mathf.Atan2(ejectVelocity.y, ejectVelocity.x) * Mathf.Rad2Deg + _shellCasingRotationOffsetDegrees;
            Vector3 pos = new Vector3(spawnPos.x, spawnPos.y, _shellCasingWorldZ);
            GameObject casing = SimplePrefabPool_V2.Spawn(
                _shellCasingPrefab,
                pos,
                Quaternion.Euler(0f, 0f, angle),
                _shellCasingParent);
            if (casing == null)
            {
                return false;
            }

            ShellCasing_V2 shellCasing = casing.GetComponent<ShellCasing_V2>();
            if (shellCasing == null)
            {
                shellCasing = casing.AddComponent<ShellCasing_V2>();
            }

            float spin = UnityEngine.Random.Range(
                Mathf.Min(_shellCasingAngularVelocityRange.x, _shellCasingAngularVelocityRange.y),
                Mathf.Max(_shellCasingAngularVelocityRange.x, _shellCasingAngularVelocityRange.y));
            if (UnityEngine.Random.value < 0.5f)
            {
                spin = -spin;
            }

            shellCasing.Arm(ejectVelocity, spin, _shellCasingLifetimeSeconds);
            return true;
        }

        private static bool ShouldEjectShellCasingForWeapon(WeaponType weaponType)
        {
            return weaponType == WeaponType.Colt45 || weaponType == WeaponType.Thompson;
        }

        [ContextMenu("Test Shell Casing Right")]
        private void TestShellCasingRight()
        {
            TryEjectShellCasing(WeaponType.Colt45, transform.position, Vector2.right);
        }

        private int ResolveTrailSortingLayerId()
        {
            if (!string.IsNullOrWhiteSpace(_trailSortingLayerName))
            {
                SortingLayer[] layers = SortingLayer.layers;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].name == _trailSortingLayerName)
                    {
                        return layers[i].id;
                    }
                }
            }

            return GetTopSortingLayerId();
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

        private static void ConfigureTrailMaterial(Material mat, Color tint)
        {
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            }
            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", Texture2D.whiteTexture);
            }

            Color solidTint = new Color(tint.r, tint.g, tint.b, 1f);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", solidTint);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", solidTint);
            }

            // Force line to render as solid color to avoid scene color bleed-through.
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 0f);
            }
            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f);
            }
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            }
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            }
            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 1f);
            }
            if (mat.HasProperty("_ZTest"))
            {
                mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            }

            mat.renderQueue = 5000;
        }

        internal void StopShoot()
        {
            StopFlamethrowerVfxIfActive();
            StopTeslaLightningVfxIfActive();
            AnimationReferenceAsset aimAnim = GetAimAnimationForCurrentWeapon();
            if (aimAnim != null)
            {
                _skeletonAnimation.AnimationState.SetAnimation(1, aimAnim, true);
            }
            else
            {
                _skeletonAnimation.AnimationState.ClearTrack(1);
            }
        }

        internal void PlayReload()
        {
            AnimationReferenceAsset reloadAnim = GetReloadAnimationForCurrentWeapon();
            if (reloadAnim == null)
            {
                Debug.LogWarning("[HeroView_V2] PlayReload skipped: reload animation is not assigned.");
                PlayAimLoop();
                return;
            }

            // Keep locomotion on track 0, play reload on weapon/upper-body track.
            _skeletonAnimation.AnimationState.SetAnimation(1, reloadAnim, false);
            AnimationReferenceAsset aimAnim = GetAimAnimationForCurrentWeapon();
            if (aimAnim != null)
            {
                _skeletonAnimation.AnimationState.AddAnimation(1, aimAnim, true, 0f);
            }
        }

        internal void PlayOutOfAmmo()
        {
            AnimationReferenceAsset dryFireAnim = GetDryFireAnimationForCurrentWeapon();
            if (dryFireAnim != null)
            {
                _skeletonAnimation.AnimationState.SetAnimation(1, dryFireAnim, false);
                AnimationReferenceAsset aimAnim = GetAimAnimationForCurrentWeapon();
                if (aimAnim != null)
                {
                    _skeletonAnimation.AnimationState.AddAnimation(1, aimAnim, true, 0f);
                }
                return;
            }

            if (_debugViewLogs)
            {
                Debug.Log("[HeroView_V2] Out of ammo.");
            }
        }

        internal void RefreshWeaponVisualsForCurrentState()
        {
            if (_stateMachine == null)
            {
                return;
            }

            HeroState current = _stateMachine.CurrentState;
            if (current == HeroState.Idle || current == HeroState.Shooting)
            {
                PlayLoop(GetIdleAnimationForCurrentWeapon());
                PlayAimLoop();
            }
        }

        private AnimationReferenceAsset GetIdleAnimationForCurrentWeapon()
        {
            return GetAnimationSetForCurrentWeapon().Idle;
        }

        private AnimationReferenceAsset GetAimAnimationForCurrentWeapon()
        {
            return GetAnimationSetForCurrentWeapon().Aim;
        }

        private AnimationReferenceAsset GetShootAnimationForCurrentWeapon()
        {
            return GetAnimationSetForCurrentWeapon().Shoot;
        }

        private AnimationReferenceAsset GetRunAnimationForCurrentWeapon()
        {
            return GetAnimationSetForCurrentWeapon().Run;
        }

        private AnimationReferenceAsset GetJumpAnimationForCurrentWeapon()
        {
            return GetAnimationSetForCurrentWeapon().Jump;
        }

        private AnimationReferenceAsset GetReloadAnimationForCurrentWeapon()
        {
            return GetAnimationSetForCurrentWeapon().Reload;
        }

        private AnimationReferenceAsset GetDryFireAnimationForCurrentWeapon()
        {
            return GetAnimationSetForCurrentWeapon().DryFire;
        }

        private WeaponAnimationSet GetAnimationSetForCurrentWeapon()
        {
            WeaponAnimationSet fallbackSet = GetFallbackAnimationSet();
            WeaponType currentWeaponType = _model != null ? _model.currentWeaponType : WeaponType.None;

            // Compatibility bridge: keep old explicit flamethrower slots available in HeroView_V2
            // while the rest of the weapon pipeline remains definition-driven.
            if (currentWeaponType == WeaponType.Flamethrower)
            {
                WeaponAnimationSet flamethrowerOverride = CreateAnimationSet(
                    idleFlamethrowerAnim,
                    aimFlamethrowerAnim,
                    shootingFlamethrowerAnim,
                    runFlamethrowerAnim,
                    jumpFlamethrowerAnim,
                    null,
                    null);
                return MergeWithFallback(flamethrowerOverride, fallbackSet);
            }

            HeroWeaponDefinition_V2 currentWeaponDefinition = _model != null
                ? _model.currentWeaponDefinition
                : null;

            WeaponAnimationSet merged = MergeWithFallback(
                CreateAnimationSetFromDefinition(currentWeaponDefinition),
                fallbackSet);

            if (_model != null && _model.currentWeaponType == WeaponType.Tesla)
            {
                merged = MergeWithTeslaAnimationOverrides(merged);
            }

            return merged;
        }

        private WeaponAnimationSet MergeWithTeslaAnimationOverrides(WeaponAnimationSet baseSet)
        {
            return new WeaponAnimationSet
            {
                Idle = idleTeslaAnim != null ? idleTeslaAnim : baseSet.Idle,
                Aim = aimTeslaAnim != null ? aimTeslaAnim : baseSet.Aim,
                Shoot = shootingTeslaAnim != null ? shootingTeslaAnim : baseSet.Shoot,
                Run = runTeslaAnim != null ? runTeslaAnim : baseSet.Run,
                Jump = jumpTeslaAnim != null ? jumpTeslaAnim : baseSet.Jump,
                Reload = baseSet.Reload,
                DryFire = baseSet.DryFire
            };
        }

        private void TryPlayFlamethrowerVfx()
        {
            if (_model == null || _model.currentWeaponType != WeaponType.Flamethrower)
            {
                StopFlamethrowerVfxIfActive();
                return;
            }

            // Safety gate: never emit flamethrower VFX unless fire input is actually held.
            if (!_model.isShootingPressed)
            {
                StopFlamethrowerVfxIfActive();
                return;
            }

            if (_flamethrowerTestPs == null)
            {
                return;
            }

            if (_lockFlamethrowerDirectionWhileShooting && !_hasLockedFlamethrowerDirection)
            {
                if (TryGetFlamethrowerPose(out _, out Vector2 initialDir))
                {
                    _lockedFlamethrowerDirection = initialDir;
                    _hasLockedFlamethrowerDirection = true;
                }
            }

            if (_overrideFlamethrowerParticleSettings)
            {
                ApplyFlamethrowerSprayProfile();
            }
            UpdateFlamethrowerVfxPose();
            if (!_flamethrowerTestPs.isPlaying)
            {
                _flamethrowerTestPs.Play(true);
            }

            if (_debugViewLogs)
            {
                LogFlamethrowerParticleDebug();
            }
        }

        private void StopFlamethrowerVfxIfActive()
        {
            _hasLockedFlamethrowerDirection = false;
            if (_flamethrowerTestPs == null)
            {
                return;
            }

            if (_flamethrowerTestPs.isPlaying || _flamethrowerTestPs.isEmitting || _flamethrowerTestPs.particleCount > 0)
            {
                _flamethrowerTestPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void UpdateFlamethrowerVfxPose()
        {
            if (_flamethrowerTestPs == null)
            {
                return;
            }

            if (!TryGetFlamethrowerPose(out Vector2 origin, out Vector2 liveDirection))
            {
                return;
            }

            Vector2 dir = (_lockFlamethrowerDirectionWhileShooting && _hasLockedFlamethrowerDirection)
                ? _lockedFlamethrowerDirection
                : liveDirection;

            float z = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + _flamethrowerVfxAngleOffsetDegrees;
            Transform vfxTransform = _flamethrowerTestPs.transform;
            vfxTransform.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, z));
            EnsureFlamethrowerSimulationSpaceLocal();
            ApplyFlamethrowerReachSprayTuning();
            ApplyFlamethrowerForceFromAim(dir);
        }

        private void EnsureFlamethrowerSimulationSpaceLocal()
        {
            if (_flamethrowerTestPs == null)
            {
                return;
            }

            ParticleSystem.MainModule main = _flamethrowerTestPs.main;
            if (main.simulationSpace != ParticleSystemSimulationSpace.Local)
            {
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        private void ApplyFlamethrowerForceFromAim(Vector2 direction)
        {
            if (!_driveFlamethrowerForceFromAim || _flamethrowerTestPs == null)
            {
                return;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 dir = direction.normalized;
            float strength = ComputeFlamethrowerAimForceStrength();

            ParticleSystem.ForceOverLifetimeModule force = _flamethrowerTestPs.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(dir.x * strength);
            force.y = new ParticleSystem.MinMaxCurve(dir.y * strength);
        }

        private float ComputeFlamethrowerAimForceStrength()
        {
            float baseStrength = Mathf.Max(0f, _flamethrowerAimForceStrength);
            if (!_autoTuneFlamethrowerParticleSpeed)
            {
                return baseStrength;
            }

            // Same fraction as gameplay reach: full force at 1.0, ~70% travel at 0.7.
            return baseStrength * FlamethrowerViewReachFraction;
        }

        private bool TryGetFlamethrowerPose(out Vector2 origin, out Vector2 direction)
        {
            origin = default;
            direction = default;

            if (_skeletonAnimation == null || _flamethrowerAimBone == null || _crossHairBone == null)
            {
                return false;
            }

            origin = _skeletonAnimation.transform.TransformPoint(
                new Vector3(_flamethrowerAimBone.WorldX, _flamethrowerAimBone.WorldY, 0f));
            Vector2 target = _skeletonAnimation.transform.TransformPoint(
                new Vector3(_crossHairBone.WorldX, _crossHairBone.WorldY, 0f));
            Vector2 dir = target - origin;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction = dir.normalized;
            return true;
        }

        private float ResolveFlamethrowerSprayDistanceMeters()
        {
            if (TryGetFlamethrowerPose(out Vector2 origin, out Vector2 direction))
            {
                Camera cam = _cam != null ? _cam : Camera.main;
                if (HeroCombatCameraReach_V2.TryGetReachPoint(
                        cam,
                        origin,
                        direction,
                        Mathf.Clamp01(_flamethrowerViewReachFraction),
                        out _,
                        out float reach))
                {
                    return reach;
                }
            }

            return _flamethrowerSprayDistanceMeters;
        }

        // Visual reach follows view fraction via scaled force + stretch length (no velocity cap — that shrinks Stretch mode).
        private void ApplyFlamethrowerReachSprayTuning()
        {
            if (!_autoTuneFlamethrowerParticleSpeed || _flamethrowerTestPs == null)
            {
                return;
            }

            ParticleSystem.VelocityOverLifetimeModule velocity = _flamethrowerTestPs.velocityOverLifetime;
            if (velocity.enabled)
            {
                velocity.enabled = false;
            }

            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity =
                _flamethrowerTestPs.limitVelocityOverLifetime;
            if (limitVelocity.enabled)
            {
                limitVelocity.enabled = false;
            }

            if (_autoConfigureFlamethrowerBeamRenderer &&
                _flamethrowerTestPs.TryGetComponent(out ParticleSystemRenderer psRenderer))
            {
                psRenderer.lengthScale = Mathf.Max(
                    0.1f,
                    _flamethrowerBeamLengthScale * FlamethrowerViewReachFraction);
            }
        }

        private void ApplyFlamethrowerSprayProfile()
        {
            ApplyFlamethrowerReachSprayTuning();
            if (!_autoTuneFlamethrowerParticleSpeed || _flamethrowerTestPs == null)
            {
                return;
            }

            ParticleSystem.MainModule main = _flamethrowerTestPs.main;
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Max(0.01f, _flamethrowerBeamStartSize));
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;

            ParticleSystem.EmissionModule emission = _flamethrowerTestPs.emission;
            if (!emission.enabled)
            {
                emission.enabled = true;
            }
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, _flamethrowerBeamEmissionRate));

            if (_autoConfigureFlamethrowerAsBeam)
            {
                ParticleSystem.ShapeModule shape = _flamethrowerTestPs.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = Mathf.Clamp(_flamethrowerBeamSpreadAngle, 0.1f, 25f);
                shape.radius = 0.01f;
                shape.radiusThickness = 1f;
                shape.arc = 360f;
                shape.alignToDirection = true;
                shape.randomDirectionAmount = 0f;
                shape.sphericalDirectionAmount = 0f;

                // Many imported fire presets rely on noise/turbulence for campfires.
                // Disable these for a narrow flamethrower jet profile.
                ParticleSystem.NoiseModule noise = _flamethrowerTestPs.noise;
                if (noise.enabled)
                {
                    noise.enabled = false;
                }

                ParticleSystem.VelocityOverLifetimeModule velocity = _flamethrowerTestPs.velocityOverLifetime;
                if (velocity.enabled)
                {
                    velocity.enabled = false;
                }

                ParticleSystem.ForceOverLifetimeModule force = _flamethrowerTestPs.forceOverLifetime;
                if (force.enabled)
                {
                    force.enabled = false;
                }

                ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = _flamethrowerTestPs.limitVelocityOverLifetime;
                if (limitVelocity.enabled)
                {
                    limitVelocity.enabled = false;
                }

                ParticleSystem.InheritVelocityModule inheritVelocity = _flamethrowerTestPs.inheritVelocity;
                if (inheritVelocity.enabled)
                {
                    inheritVelocity.enabled = false;
                }
            }

            if (_autoConfigureFlamethrowerBeamRenderer &&
                _flamethrowerTestPs.TryGetComponent(out ParticleSystemRenderer psRenderer))
            {
                psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                psRenderer.lengthScale = Mathf.Max(
                    0.1f,
                    _flamethrowerBeamLengthScale * FlamethrowerViewReachFraction);
                psRenderer.velocityScale = Mathf.Max(0f, _flamethrowerBeamSpeedScale);
                psRenderer.cameraVelocityScale = 0f;
                psRenderer.normalDirection = 1f;
                psRenderer.alignment = ParticleSystemRenderSpace.View;
                psRenderer.sortMode = ParticleSystemSortMode.None;
                psRenderer.shadowCastingMode = ShadowCastingMode.Off;
                psRenderer.receiveShadows = false;
            }
        }

        private void LogFlamethrowerParticleDebug()
        {
            if (_flamethrowerTestPs == null)
            {
                Debug.Log("[HeroView_V2 FlamethrowerVfx] Missing particle system reference.");
                return;
            }

            ParticleSystem.MainModule main = _flamethrowerTestPs.main;
            ParticleSystem.ShapeModule shape = _flamethrowerTestPs.shape;
            ParticleSystem.EmissionModule emission = _flamethrowerTestPs.emission;
            ParticleSystem.NoiseModule noise = _flamethrowerTestPs.noise;
            ParticleSystem.VelocityOverLifetimeModule velocity = _flamethrowerTestPs.velocityOverLifetime;
            ParticleSystem.ForceOverLifetimeModule force = _flamethrowerTestPs.forceOverLifetime;
            ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = _flamethrowerTestPs.limitVelocityOverLifetime;
            ParticleSystem.InheritVelocityModule inheritVelocity = _flamethrowerTestPs.inheritVelocity;
            Transform t = _flamethrowerTestPs.transform;

            Debug.Log(
                "[HeroView_V2 FlamethrowerVfx] " +
                $"playing={_flamethrowerTestPs.isPlaying} simSpace={main.simulationSpace} " +
                $"lifetime={main.startLifetime.constant:0.###} speed={main.startSpeed.constant:0.###} size={main.startSize.constant:0.###} " +
                $"gravity={main.gravityModifier.constant:0.###} emitterVelMode={main.emitterVelocityMode} " +
                $"shapeEnabled={shape.enabled} shapeType={shape.shapeType} angle={shape.angle:0.###} radius={shape.radius:0.###} " +
                $"randDir={shape.randomDirectionAmount:0.###} sphericalDir={shape.sphericalDirectionAmount:0.###} alignToDir={shape.alignToDirection} " +
                $"emissionEnabled={emission.enabled} rate={emission.rateOverTime.constant:0.###} " +
                $"noise={noise.enabled} velocityOverLifetime={velocity.enabled} forceOverLifetime={force.enabled} " +
                $"limitVelocity={limitVelocity.enabled} inheritVelocity={inheritVelocity.enabled} " +
                $"worldPos=({t.position.x:0.##},{t.position.y:0.##},{t.position.z:0.##}) eulerZ={t.eulerAngles.z:0.##}");
        }

        private static WeaponAnimationSet CreateAnimationSet(
            AnimationReferenceAsset idle,
            AnimationReferenceAsset aim,
            AnimationReferenceAsset shoot,
            AnimationReferenceAsset run,
            AnimationReferenceAsset jump,
            AnimationReferenceAsset reload,
            AnimationReferenceAsset dryFire)
        {
            return new WeaponAnimationSet
            {
                Idle = idle,
                Aim = aim,
                Shoot = shoot,
                Run = run,
                Jump = jump,
                Reload = reload,
                DryFire = dryFire
            };
        }

        private static WeaponAnimationSet CreateAnimationSetFromDefinition(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null)
            {
                return default;
            }

            return CreateAnimationSet(
                definition.IdleAnimation,
                definition.AimAnimation,
                definition.ShootAnimation,
                definition.RunAnimation,
                definition.JumpAnimation,
                definition.ReloadAnimation,
                definition.DryFireAnimation);
        }

        private WeaponAnimationSet GetFallbackAnimationSet()
        {
            return CreateAnimationSetFromDefinition(_fallbackWeaponDefinition);
        }

        private static WeaponAnimationSet MergeWithFallback(WeaponAnimationSet primary, WeaponAnimationSet fallback)
        {
            return new WeaponAnimationSet
            {
                Idle = primary.Idle != null ? primary.Idle : fallback.Idle,
                Aim = primary.Aim != null ? primary.Aim : fallback.Aim,
                Shoot = primary.Shoot != null ? primary.Shoot : fallback.Shoot,
                Run = primary.Run != null ? primary.Run : fallback.Run,
                Jump = primary.Jump != null ? primary.Jump : fallback.Jump,
                Reload = primary.Reload != null ? primary.Reload : fallback.Reload,
                DryFire = primary.DryFire != null ? primary.DryFire : fallback.DryFire
            };
        }

        /// <summary>
        /// LightningBolt2D asset may live in Assembly-CSharp; Hero V2 is in iStick2War.Game — reflection avoids a hard type reference.
        /// </summary>
        private sealed class TeslaLightningBoltCache
        {
            private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;

            private readonly MonoBehaviour _target;
            private readonly FieldInfo _startPoint;
            private readonly FieldInfo _endPoint;
            private readonly FieldInfo _isPlaying;
            private readonly FieldInfo _sortingLayer;
            private readonly FieldInfo _orderInLayer;
            private readonly MethodInfo _scheduleRegenerate;

            public MonoBehaviour Target => _target;

            public bool IsValid =>
                _target != null && _startPoint != null && _endPoint != null && _isPlaying != null;

            public TeslaLightningBoltCache(MonoBehaviour target)
            {
                _target = target;
                if (target == null)
                {
                    return;
                }

                Type t = target.GetType();
                _startPoint = t.GetField("startPoint", Flags);
                _endPoint = t.GetField("endPoint", Flags);
                _isPlaying = t.GetField("isPlaying", Flags);
                _sortingLayer = t.GetField("sortingLayer", Flags);
                _orderInLayer = t.GetField("orderInLayer", Flags);
                _scheduleRegenerate = t.GetMethod("ScheduleRegenerate", Flags);
            }

            public void PlayShot(
                Vector2 worldStart,
                Vector2 worldEnd,
                bool syncSorting,
                int sortingLayerId,
                int orderInLayer)
            {
                _startPoint.SetValue(_target, worldStart);
                _endPoint.SetValue(_target, worldEnd);
                _isPlaying.SetValue(_target, true);
                _target.enabled = true;
                _target.gameObject.SetActive(true);
                if (syncSorting)
                {
                    _sortingLayer?.SetValue(_target, sortingLayerId);
                    _orderInLayer?.SetValue(_target, orderInLayer);
                }

                _scheduleRegenerate?.Invoke(_target, null);
            }

            public void Stop()
            {
                if (_isPlaying != null)
                {
                    _isPlaying.SetValue(_target, false);
                }

                _target.gameObject.SetActive(false);
            }
        }
    }
}
