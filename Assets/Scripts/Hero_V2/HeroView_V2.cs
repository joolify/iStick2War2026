using Spine;
using Spine.Unity;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
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
        public SkeletonAnimation _skeletonAnimation;

        private HeroStateMachine_V2 _stateMachine;
        private HeroDamageReceiver_V2 _damageReceiver;
        private HeroDeathHandler_V2 _deathHandler;
        public Bone _crossHairBone;
        public Bone _aimPointBone;
        private bool _facingRight;

        [SpineBone(dataField: "skeletonAnimation")] public string aimPointBoneName;

        [SpineBone(dataField: "skeletonAnimation")] public string crossHairBoneName;

        private Vector2 _touchPos;

        [SerializeField] private Camera _cam;

        private bool _isInitialized;
        private bool _shootLocomotionIsMoving;
        private bool _shootLocomotionInitialized;
        private bool _jumpCombatIsShooting;
        private bool _jumpCombatInitialized;

        [Header("Animations")]
        public AnimationReferenceAsset _idleThompsonAnim;
        public AnimationReferenceAsset _aimThompsonAnim;
        public AnimationReferenceAsset _shootingThompsonAnim;
        public AnimationReferenceAsset _runThompsonAnim;
        public AnimationReferenceAsset _jumpThompsonAnim;
        public AnimationReferenceAsset _reloadThompsonAnim;
        public AnimationReferenceAsset _dryFireThompsonAnim;
        public AnimationReferenceAsset _landFallDownBackAnim;
        
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

        // -------------------------
        // INIT
        // -------------------------
        public void Init(
            HeroStateMachine_V2 stateMachine,
            HeroDamageReceiver_V2 damageReceiver,
            HeroDeathHandler_V2 deathHandler,
            SkeletonAnimation skeletonAnimation)
        {
            _stateMachine = stateMachine;
            _damageReceiver = damageReceiver;
            _deathHandler = deathHandler;
            _skeletonAnimation = skeletonAnimation;

            _crossHairBone = _skeletonAnimation.Skeleton.FindBone("crosshair");
            _facingRight = _skeletonAnimation.Skeleton.ScaleX >= 0f;

            if (_crossHairBone == null)
                Debug.LogError("Crosshair bone not found in skeleton!");

            // Subscribe to events
            stateMachine.OnStateChanged += HandleStateChanged;
            damageReceiver.OnDamageTaken += HandleDamageTaken;
            deathHandler.OnDeathHandled += HandleDeath;

            _isInitialized = true;

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

            // Ensure desktop aim is active from frame 1, even before first input/state transition.
            if (_idleThompsonAnim != null)
            {
                PlayLoop(_idleThompsonAnim);
            }
            PlayAimLoop();
        }

        void Update()
        {
            if (!_isInitialized)
                return;

            FaceMouse();

            _touchPos = new Vector2(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, Camera.main.ScreenToWorldPoint(Input.mousePosition).y);

            SetCrosshair(_touchPos);
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
                    PlayLoop(_idleThompsonAnim);
                    PlayAimLoop();
                    _jumpCombatInitialized = false;
                    break;

                //case HeroState.Moving:
                //    PlayLoop("run");
                //    break;

                case HeroState.Shooting:
                    // Base locomotion while shooting is controlled live by controller.
                    PlayLoop(_idleThompsonAnim);
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
                    PlayLoop(_runThompsonAnim);
                    PlayAimLoop();
                    _jumpCombatInitialized = false;
                    break;

                case HeroState.Jumping:
                    if (_jumpThompsonAnim != null)
                    {
                        PlayLoop(_jumpThompsonAnim);
                    }
                    else
                    {
                        PlayLoop(_runThompsonAnim);
                    }
                    _jumpCombatInitialized = false;
                    break;

                case HeroState.Dead:
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
            if (_idleThompsonAnim != null)
            {
                _skeletonAnimation.AnimationState.SetAnimation(0, _idleThompsonAnim, true);
            }
        }

        // -------------------------
        // HELPERS
        // -------------------------
        private void PlayLoop(AnimationReferenceAsset anim)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, anim, true);
        }

        private void PlayOneShot(AnimationReferenceAsset anim)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, anim, false);
            _skeletonAnimation.AnimationState.AddAnimation(0, "idle", true, 0f);
        }

        private void PlayAimLoop()
        {
            if (_aimThompsonAnim == null)
            {
                return;
            }

            _skeletonAnimation.AnimationState.SetAnimation(1, _aimThompsonAnim, true);
        }

        private void SetCrosshair(Vector2 localTouchPos)
        {
            if (_skeletonAnimation == null)
                Debug.LogError("SkeletonAnimation not found!");

            var skeletonSpacePoint = _skeletonAnimation.transform.InverseTransformPoint(localTouchPos);
            skeletonSpacePoint.x *= _skeletonAnimation.Skeleton.ScaleX;
            skeletonSpacePoint.y *= _skeletonAnimation.Skeleton.ScaleY;
            _crossHairBone.SetLocalPosition(skeletonSpacePoint);
        }

        void FaceMouse()
        {
            // Desktop-only cursor flip. Mobile/touch uses different aim handling.
#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
            return;
#else
            Vector3 delta = _cam.ScreenToWorldPoint(Input.mousePosition) - transform.position;

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
#endif
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

        internal void PlayDeathEffect()
        {
            HandleDeath();
        }

        private void CheckAnimationNames()
        {
            //if (!aimBazookaAnim.name.Equals("H_bazooka_aim")) Debug.LogError(nameof(aimBazookaAnim) + " has wrong animation");
            //if (!crouchGrenadeBazookaAnim.name.Equals("H_bazooka_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeBazookaAnim) + " has wrong animation");
            //if (!crouchIdleBazookaAnim.name.Equals("H_bazooka_crouch_idle")) Debug.LogError(nameof(crouchIdleBazookaAnim) + " has wrong animation");
            //if (!crouchReloadBazookaAnim.name.Equals("H_bazooka_crouch_reload")) Debug.LogError(nameof(crouchReloadBazookaAnim) + " has wrong animation");
            //if (!crouchShootBazookaAnim.name.Equals("H_bazooka_crouch_shoot")) Debug.LogError(nameof(crouchShootBazookaAnim) + " has wrong animation");
            //if (!crouchWalkBazookaAnim.name.Equals("H_bazooka_crouch_walk")) Debug.LogError(nameof(crouchWalkBazookaAnim) + " has wrong animation");
            //if (!grenadeBazookaAnim.name.Equals("H_bazooka_grenade")) Debug.LogError(nameof(grenadeBazookaAnim) + " has wrong animation");
            //if (!idleBazookaAnim.name.Equals("H_bazooka_idle")) Debug.LogError(nameof(idleBazookaAnim) + " has wrong animation");
            //if (!jumpBazookaAnim.name.Equals("H_bazooka_jump")) Debug.LogError(nameof(jumpBazookaAnim) + " has wrong animation");
            //if (!reloadBazookaAnim.name.Equals("H_bazooka_reload")) Debug.LogError(nameof(reloadBazookaAnim) + " has wrong animation");
            //if (!runBazookaAnim.name.Equals("H_bazooka_run")) Debug.LogError(nameof(runBazookaAnim) + " has wrong animation");
            //if (!shootingBazookaAnim.name.Equals("H_bazooka_shoot")) Debug.LogError(nameof(shootingBazookaAnim) + " has wrong animation");

            //if (!aimCarbineAnim.name.Equals("H_carbine_aim")) Debug.LogError(nameof(aimCarbineAnim) + " has wrong animation");
            //if (!crouchGrenadeCarbineAnim.name.Equals("H_carbine_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeCarbineAnim) + " has wrong animation");
            //if (!crouchIdleCarbineAnim.name.Equals("H_carbine_crouch_idle")) Debug.LogError(nameof(crouchIdleCarbineAnim) + " has wrong animation");
            //if (!crouchReloadCarbineAnim.name.Equals("H_carbine_crouch_reload")) Debug.LogError(nameof(crouchReloadCarbineAnim) + " has wrong animation");
            //if (!crouchShootCarbineAnim.name.Equals("H_carbine_crouch_shoot")) Debug.LogError(nameof(crouchShootCarbineAnim) + " has wrong animation");
            //if (!crouchWalkCarbineAnim.name.Equals("H_carbine_crouch_walk")) Debug.LogError(nameof(crouchWalkCarbineAnim) + " has wrong animation");
            //if (!grenadeCarbineAnim.name.Equals("H_carbine_grenade")) Debug.LogError(nameof(grenadeCarbineAnim) + " has wrong animation");
            //if (!idleCarbineAnim.name.Equals("H_carbine_idle")) Debug.LogError(nameof(idleCarbineAnim) + " has wrong animation");
            //if (!jumpCarbineAnim.name.Equals("H_carbine_jump")) Debug.LogError(nameof(jumpCarbineAnim) + " has wrong animation");
            //if (!reloadCarbineAnim.name.Equals("H_carbine_reload")) Debug.LogError(nameof(reloadCarbineAnim) + " has wrong animation");
            //if (!runCarbineAnim.name.Equals("H_carbine_run")) Debug.LogError(nameof(runCarbineAnim) + " has wrong animation");
            //if (!shootingCarbineAnim.name.Equals("H_carbine_shoot")) Debug.LogError(nameof(shootingCarbineAnim) + " has wrong animation");

            //if (!aimColt45Anim.name.Equals("H_colt_aim")) Debug.LogError(nameof(aimColt45Anim) + " has wrong animation");
            //if (!crouchGrenadeColt45Anim.name.Equals("H_colt_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeColt45Anim) + " has wrong animation");
            //if (!crouchIdleColt45Anim.name.Equals("H_colt_crouch_idle")) Debug.LogError(nameof(crouchIdleColt45Anim) + " has wrong animation");
            //if (!crouchReloadColt45Anim.name.Equals("H_colt_crouch_reload")) Debug.LogError(nameof(crouchReloadColt45Anim) + " has wrong animation");
            //if (!crouchShootColt45Anim.name.Equals("H_colt_crouch_shoot")) Debug.LogError(nameof(crouchShootColt45Anim) + " has wrong animation");
            //if (!crouchWalkColt45Anim.name.Equals("H_colt_crouch_walk")) Debug.LogError(nameof(crouchWalkColt45Anim) + " has wrong animation");
            //if (!grenadeColt45Anim.name.Equals("H_colt_grenade")) Debug.LogError(nameof(grenadeColt45Anim) + " has wrong animation");
            //if (!idleColt45Anim.name.Equals("H_colt_idle")) Debug.LogError(nameof(idleColt45Anim) + " has wrong animation");
            //if (!jumpColt45Anim.name.Equals("H_colt_jump")) Debug.LogError(nameof(jumpColt45Anim) + " has wrong animation");
            //if (!reloadColt45Anim.name.Equals("H_colt_reload")) Debug.LogError(nameof(reloadColt45Anim) + " has wrong animation");
            //if (!runColt45Anim.name.Equals("H_colt_run")) Debug.LogError(nameof(runColt45Anim) + " has wrong animation");
            //if (!shootingColt45Anim.name.Equals("H_colt_shoot")) Debug.LogError(nameof(shootingColt45Anim) + " has wrong animation");

            //if (!aimFlamethrowerAnim.name.Equals("H_flamethrower_aim")) Debug.LogError(nameof(aimFlamethrowerAnim) + " has wrong animation");
            //if (!crouchGrenadeFlamethrowerAnim.name.Equals("H_flamethrower_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeFlamethrowerAnim) + " has wrong animation");
            //if (!crouchIdleFlamethrowerAnim.name.Equals("H_flamethrower_crouch_idle")) Debug.LogError(nameof(crouchIdleFlamethrowerAnim) + " has wrong animation");
            //if (!crouchShootFlamethrowerAnim.name.Equals("H_flamethrower_crouch_shoot")) Debug.LogError(nameof(crouchShootFlamethrowerAnim) + " has wrong animation");
            //if (!crouchWalkFlamethrowerAnim.name.Equals("H_flamethrower_crouch_walk")) Debug.LogError(nameof(crouchWalkFlamethrowerAnim) + " has wrong animation");
            //if (!grenadeFlamethrowerAnim.name.Equals("H_flamethrower_grenade")) Debug.LogError(nameof(grenadeFlamethrowerAnim) + " has wrong animation");
            //if (!idleFlamethrowerAnim.name.Equals("H_flamethrower_idle")) Debug.LogError(nameof(idleFlamethrowerAnim) + " has wrong animation");
            //if (!jumpFlamethrowerAnim.name.Equals("H_flamethrower_jump")) Debug.LogError(nameof(jumpFlamethrowerAnim) + " has wrong animation");
            //if (!runFlamethrowerAnim.name.Equals("H_flamethrower_run")) Debug.LogError(nameof(runFlamethrowerAnim) + " has wrong animation");
            //if (!shootingFlamethrowerAnim.name.Equals("H_flamethrower_shoot")) Debug.LogError(nameof(shootingFlamethrowerAnim) + " has wrong animation");

            //if (!aimIthacaAnim.name.Equals("H_ithaca_aim")) Debug.LogError(nameof(aimIthacaAnim) + " has wrong animation");
            //if (!crouchGrenadeIthacaAnim.name.Equals("H_ithaca_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeIthacaAnim) + " has wrong animation");
            //if (!crouchIdleIthacaAnim.name.Equals("H_ithaca_crouch_idle")) Debug.LogError(nameof(crouchIdleIthacaAnim) + " has wrong animation");
            //if (!crouchReloadIthacaAnim.name.Equals("H_ithaca_crouch_reload")) Debug.LogError(nameof(crouchReloadIthacaAnim) + " has wrong animation");
            //if (!crouchShootIthacaAnim.name.Equals("H_ithaca_crouch_shoot")) Debug.LogError(nameof(crouchShootIthacaAnim) + " has wrong animation");
            //if (!crouchWalkIthacaAnim.name.Equals("H_ithaca_crouch_walk")) Debug.LogError(nameof(crouchWalkIthacaAnim) + " has wrong animation");
            //if (!grenadeIthacaAnim.name.Equals("H_ithaca_grenade")) Debug.LogError(nameof(grenadeIthacaAnim) + " has wrong animation");
            //if (!idleIthacaAnim.name.Equals("H_ithaca_idle")) Debug.LogError(nameof(idleIthacaAnim) + " has wrong animation");
            //if (!jumpIthacaAnim.name.Equals("H_ithaca_jump")) Debug.LogError(nameof(jumpIthacaAnim) + " has wrong animation");
            //if (!reloadIthacaAnim.name.Equals("H_ithaca_reload")) Debug.LogError(nameof(reloadIthacaAnim) + " has wrong animation");
            //if (!runIthacaAnim.name.Equals("H_ithaca_run")) Debug.LogError(nameof(runIthacaAnim) + " has wrong animation");
            //if (!shootingIthacaAnim.name.Equals("H_ithaca_shoot")) Debug.LogError(nameof(shootingIthacaAnim) + " has wrong animation");

            //if (!aimMagicStaffAnim.name.Equals("H_magic_staff_aim")) Debug.LogError(nameof(aimMagicStaffAnim) + " has wrong animation");
            //if (!crouchGrenadeMagicStaffAnim.name.Equals("H_magic_staff_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeMagicStaffAnim) + " has wrong animation");
            //if (!crouchIdleMagicStaffAnim.name.Equals("H_magic_staff_crouch_idle")) Debug.LogError(nameof(crouchIdleMagicStaffAnim) + " has wrong animation");
            //if (!crouchShootMagicStaffAnim.name.Equals("H_magic_staff_crouch_shoot")) Debug.LogError(nameof(crouchShootMagicStaffAnim) + " has wrong animation");
            //if (!crouchWalkMagicStaffAnim.name.Equals("H_magic_staff_crouch_walk")) Debug.LogError(nameof(crouchWalkMagicStaffAnim) + " has wrong animation");
            //if (!grenadeMagicStaffAnim.name.Equals("H_magic_staff_grenade")) Debug.LogError(nameof(grenadeMagicStaffAnim) + " has wrong animation");
            //if (!idleMagicStaffAnim.name.Equals("H_magic_staff_idle")) Debug.LogError(nameof(idleMagicStaffAnim) + " has wrong animation");
            //if (!jumpMagicStaffAnim.name.Equals("H_magic_staff_jump")) Debug.LogError(nameof(jumpMagicStaffAnim) + " has wrong animation");
            //if (!runMagicStaffAnim.name.Equals("H_magic_staff_run")) Debug.LogError(nameof(runMagicStaffAnim) + " has wrong animation");
            //if (!shootingMagicStaffAnim.name.Equals("H_magic_staff_shoot")) Debug.LogError(nameof(shootingMagicStaffAnim) + " has wrong animation");

            //if (!aimSterlingL2A3Anim.name.Equals("H_sterlingL2A3_aim")) Debug.LogError(nameof(aimSterlingL2A3Anim) + " has wrong animation");
            //if (!crouchGrenadeSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeSterlingL2A3Anim) + " has wrong animation");
            //if (!crouchIdleSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_idle")) Debug.LogError(nameof(crouchIdleSterlingL2A3Anim) + " has wrong animation");
            //if (!crouchReloadSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_reload")) Debug.LogError(nameof(crouchReloadSterlingL2A3Anim) + " has wrong animation");
            //if (!crouchShootSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_shoot")) Debug.LogError(nameof(crouchShootSterlingL2A3Anim) + " has wrong animation");
            //if (!crouchWalkSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_walk")) Debug.LogError(nameof(crouchWalkSterlingL2A3Anim) + " has wrong animation");
            //if (!grenadeSterlingL2A3Anim.name.Equals("H_sterlingL2A3_grenade")) Debug.LogError(nameof(grenadeSterlingL2A3Anim) + " has wrong animation");
            //if (!idleSterlingL2A3Anim.name.Equals("H_sterlingL2A3_idle")) Debug.LogError(nameof(idleSterlingL2A3Anim) + " has wrong animation");
            //if (!jumpSterlingL2A3Anim.name.Equals("H_sterlingL2A3_jump")) Debug.LogError(nameof(jumpSterlingL2A3Anim) + " has wrong animation");
            //if (!reloadSterlingL2A3Anim.name.Equals("H_sterlingL2A3_reload")) Debug.LogError(nameof(reloadSterlingL2A3Anim) + " has wrong animation");
            //if (!runSterlingL2A3Anim.name.Equals("H_sterlingL2A3_run")) Debug.LogError(nameof(runSterlingL2A3Anim) + " has wrong animation");
            //if (!shootingSterlingL2A3Anim.name.Equals("H_sterlingL2A3_shoot")) Debug.LogError(nameof(shootingSterlingL2A3Anim) + " has wrong animation");

            //if (!aimTeslaAnim.name.Equals("H_tesla_aim")) Debug.LogError(nameof(aimTeslaAnim) + " has wrong animation");
            //if (!crouchGrenadeTeslaAnim.name.Equals("H_tesla_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeTeslaAnim) + " has wrong animation");
            //if (!crouchIdleTeslaAnim.name.Equals("H_tesla_crouch_idle")) Debug.LogError(nameof(crouchIdleTeslaAnim) + " has wrong animation");
            //if (!crouchShootTeslaAnim.name.Equals("H_tesla_crouch_shoot")) Debug.LogError(nameof(crouchShootTeslaAnim) + " has wrong animation");
            //if (!crouchWalkTeslaAnim.name.Equals("H_tesla_crouch_walk")) Debug.LogError(nameof(crouchWalkTeslaAnim) + " has wrong animation");
            //if (!grenadeTeslaAnim.name.Equals("H_tesla_grenade")) Debug.LogError(nameof(grenadeTeslaAnim) + " has wrong animation");
            //if (!idleTeslaAnim.name.Equals("H_tesla_idle")) Debug.LogError(nameof(idleTeslaAnim) + " has wrong animation");
            //if (!jumpTeslaAnim.name.Equals("H_tesla_jump")) Debug.LogError(nameof(jumpTeslaAnim) + " has wrong animation");
            //if (!runTeslaAnim.name.Equals("H_tesla_run")) Debug.LogError(nameof(runTeslaAnim) + " has wrong animation");
            //if (!shootingTeslaAnim.name.Equals("H_tesla_shoot")) Debug.LogError(nameof(shootingTeslaAnim) + " has wrong animation");

            if (!_aimThompsonAnim.name.Equals("H_thompson_aim")) Debug.LogError(nameof(_aimThompsonAnim) + " has wrong animation");
            //if (!crouchGrenadeThompsonAnim.name.Equals("H_thompson_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeThompsonAnim) + " has wrong animation");
            //if (!crouchIdleThompsonAnim.name.Equals("H_thompson_crouch_idle")) Debug.LogError(nameof(crouchIdleThompsonAnim) + " has wrong animation");
            //if (!crouchReloadThompsonAnim.name.Equals("H_thompson_crouch_reload")) Debug.LogError(nameof(crouchReloadThompsonAnim) + " has wrong animation");
            //if (!crouchShootThompsonAnim.name.Equals("H_thompson_crouch_shoot")) Debug.LogError(nameof(crouchShootThompsonAnim) + " has wrong animation");
            //if (!crouchWalkThompsonAnim.name.Equals("H_thompson_crouch_walk")) Debug.LogError(nameof(crouchWalkThompsonAnim) + " has wrong animation");
            //if (!grenadeThompsonAnim.name.Equals("H_thompson_grenade")) Debug.LogError(nameof(grenadeThompsonAnim) + " has wrong animation");
            if (!_idleThompsonAnim.name.Equals("H_thompson_idle")) Debug.LogError(nameof(_idleThompsonAnim) + " has wrong animation");
            if (!_jumpThompsonAnim.name.Equals("H_thompson_jump")) Debug.LogError(nameof(_jumpThompsonAnim) + " has wrong animation");
            //if (!reloadThompsonAnim.name.Equals("H_thompson_reload")) Debug.LogError(nameof(reloadThompsonAnim) + " has wrong animation");
            if (!_runThompsonAnim.name.Equals("H_thompson_run")) Debug.LogError(nameof(_runThompsonAnim) + " has wrong animation");
            if (!_shootingThompsonAnim.name.Equals("H_thompson_shoot")) Debug.LogError(nameof(_shootingThompsonAnim) + " has wrong animation");
            if (!_landFallDownBackAnim.name.Equals("E_land_fall_down_back")) Debug.LogError(nameof(_landFallDownBackAnim) + " has wrong animation");
        }

        internal void PlayShoot()
        {
            _skeletonAnimation.AnimationState.SetAnimation(1, _shootingThompsonAnim, true);
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
            PlayLoop(isMoving ? _runThompsonAnim : _idleThompsonAnim);
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
            if (_aimThompsonAnim != null)
            {
                _skeletonAnimation.AnimationState.SetAnimation(1, _aimThompsonAnim, true);
            }
            else
            {
                _skeletonAnimation.AnimationState.ClearTrack(1);
            }
        }

        internal void PlayReload()
        {
            if (_reloadThompsonAnim == null)
            {
                Debug.LogWarning("[HeroView_V2] PlayReload skipped: reload animation is not assigned.");
                PlayAimLoop();
                return;
            }

            // Keep locomotion on track 0, play reload on weapon/upper-body track.
            _skeletonAnimation.AnimationState.SetAnimation(1, _reloadThompsonAnim, false);
            if (_aimThompsonAnim != null)
            {
                _skeletonAnimation.AnimationState.AddAnimation(1, _aimThompsonAnim, true, 0f);
            }
        }

        internal void PlayOutOfAmmo()
        {
            if (_dryFireThompsonAnim != null)
            {
                _skeletonAnimation.AnimationState.SetAnimation(1, _dryFireThompsonAnim, false);
                if (_aimThompsonAnim != null)
                {
                    _skeletonAnimation.AnimationState.AddAnimation(1, _aimThompsonAnim, true, 0f);
                }
                return;
            }

            if (_debugViewLogs)
            {
                Debug.Log("[HeroView_V2] Out of ammo.");
            }
        }
    }
}
