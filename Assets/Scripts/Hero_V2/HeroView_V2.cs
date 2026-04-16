using Spine;
using Spine.Unity;
using UnityEngine;
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
        [SerializeField] private HeroWeaponDefinition_V2 _fallbackWeaponDefinition;
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
            AnimationReferenceAsset idle = GetIdleAnimationForCurrentWeapon();
            if (idle != null)
            {
                PlayLoop(idle);
            }
            PlayAimLoop();
        }

        void Update()
        {
            if (!_isInitialized)
                return;

            if (IsDead())
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
                    PlayLoop(GetRunAnimationForCurrentWeapon());
                    PlayAimLoop();
                    _jumpCombatInitialized = false;
                    break;

                case HeroState.Jumping:
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

        private void SetCrosshair(Vector2 localTouchPos)
        {
            if (IsDead())
            {
                return;
            }

            if (_skeletonAnimation == null)
                Debug.LogError("SkeletonAnimation not found!");

            var skeletonSpacePoint = _skeletonAnimation.transform.InverseTransformPoint(localTouchPos);
            skeletonSpacePoint.x *= _skeletonAnimation.Skeleton.ScaleX;
            skeletonSpacePoint.y *= _skeletonAnimation.Skeleton.ScaleY;
            _crossHairBone.SetLocalPosition(skeletonSpacePoint);
        }

        void FaceMouse()
        {
            if (IsDead())
            {
                return;
            }

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

            HeroWeaponDefinition_V2 currentWeaponDefinition = _model != null
                ? _model.currentWeaponDefinition
                : null;

            return MergeWithFallback(CreateAnimationSetFromDefinition(currentWeaponDefinition), fallbackSet);
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
    }
}
