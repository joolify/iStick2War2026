using iStick2War;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * MechRobotBoss_V2 (composition root — MonoBehaviour type: MechRobotBoss)
 *
 * MechRobotBoss acts as the COMPOSITION ROOT (entry point) of the mech boss stack.
 *
 * ❌ It MUST NOT contain walk/run AI, weapon hit resolution, or Spine track selection:
 * - Attack pattern / MG, cannon, missile timing (MechRobotBossWeaponSystem_V2 + controller)
 * - Locomotion and range-based aim/shoot state rules (MechRobotBossController_V2)
 * - Animation playback and body presentation (MechRobotBossView_V2)
 *
 * ✅ It is ONLY responsible for:
 * - Ensuring Model / StateMachine / Controller / View / WeaponSystem / DamageReceiver / DeathHandler /
 *   SpineEventForwarder / SkeletonAnimation references exist (serialized or GetComponent)
 * - Wiring dependencies in WireSystems (Initialize on children, Init spine forwarder)
 * - Awake bootstrap: InitializeDependencies, WireSystems, controller.StartGame()
 * - PrepareForSpawn: reset model/state/controller/weapon/view, re-enable RB/colliders, StartGame
 * - ApplyWaveDifficultyMultipliers for EnemySpawner_V2 after spawn
 * - Forwarding Die to MechRobotBossDeathHandler_V2 when the state machine enters Die
 *
 * ---------------------------------------------------------
 * ARCHITECTURE MODEL
 *
 * MechRobotBoss   = Composition root (bootstrap + prefab wiring)
 * Controller      = Brain (movement toward hero, attack pattern vs range, Spine event reactions)
 * StateMachine    = Rules (MechRobotBossBodyState + events for the View)
 * Model           = DNA (HP, flags, wave multipliers)
 * View            = Body (Spine clips / presentation)
 * WeaponSystem    = Combat execution (bursts, cannon telegraph, missiles)
 *
 * ---------------------------------------------------------
 * DESIGN GOAL
 *
 * MechRobotBoss stays thin: one place for dependency resolution and lifecycle glue,
 * mirroring Bombplane_V2’s separation between root and systems.
 */
    [DefaultExecutionOrder(600)]
    public sealed class MechRobotBoss : MonoBehaviour
    {
        private static readonly string[] FootProbeBoneNames =
        {
            "toe-front",
            "toe-back",
            "foot-front",
            "foot-back",
        };

        [SerializeField] private MechRobotBossModel_V2 _model;
        [SerializeField] private MechRobotBossStateMachine_V2 _stateMachine;
        [SerializeField] private MechRobotBossController_V2 _controller;
        [SerializeField] private MechRobotBossView_V2 _view;
        [SerializeField] private MechRobotBossWeaponSystem_V2 _weaponSystem;
        [SerializeField] private MechRobotBossDamageReceiver_V2 _damageReceiver;
        [SerializeField] private MechRobotBossDeathHandler_V2 _deathHandler;
        [SerializeField] private MechRobotBossSpineEventForwarder_V2 _spineEventForwarder;
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        // Visual feet can sit below frozen Spine hitboxes; negative values sink slightly into snow.
        [SerializeField] private float _feetGroundBiasWorld;

        [Header("UI")]
        [Tooltip("Optional world-space health bar prefab (HealthBarCanvas_V2). Also accepts a child canvas on this prefab.")]
        [SerializeField] private GameObject _worldHealthBarCanvasPrefab;
        [Tooltip("World offset from the controlcabin Spine bone (falls back to view transform).")]
        [SerializeField] private Vector3 _healthBarWorldOffset = new Vector3(-0.9f, 2.25f, 0f);
        [SerializeField] private string _healthBarAnchorBoneName = "controlcabin";
        [Tooltip("Uniform scale multiplier applied to the spawned health bar canvas (0.5 = half size).")]
        [SerializeField] private float _healthBarWorldScale = 0.5f;

        // Unparented world UI so mech scale / movement does not shear the bar.
        private GameObject _runtimeHealthBarRoot;
        private WorldHealthBarFollower_V2 _cachedHealthBarFollower;
        private Vector3 _healthBarPrefabLocalScale = Vector3.one;
        private bool _healthBarPrefabScaleCached;

        private void Awake()
        {
            InitializeDependencies();
            WireSystems();
            Rigidbody2D rb = ResolveBossRigidbody();
            PrepareBossGroundAndPhysics(rb);
            EnsureWorldHealthBar();
            _controller.StartGame();
        }

        public void PrepareForSpawn()
        {
            InitializeDependencies();

            if (_model != null)
            {
                _model.ResetForSpawn();
            }

            _stateMachine?.ResetForSpawn();
            _controller?.ResetForSpawn();
            _weaponSystem?.ResetForSpawn();
            _view?.ResetVisualStateForSpawn();

            Rigidbody2D rb = ResolveBossRigidbody();
            PrepareBossGroundAndPhysics(rb);
            EnsureWorldHealthBar();

            _controller?.StartGame();
        }

        private void OnDisable()
        {
            if (_runtimeHealthBarRoot != null)
            {
                _runtimeHealthBarRoot.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (_model == null || _model.IsDead())
            {
                return;
            }

            AbsorbViewLocalMotionIntoRoot();
            AlignFeetToGround();
        }

        // Applied by EnemySpawner_V2 after spawn.
        public void ApplyWaveDifficultyMultipliers(float healthMultiplier, float damageMultiplier)
        {
            if (_model == null)
            {
                _model = GetComponent<MechRobotBossModel_V2>();
            }

            if (_model != null)
            {
                _model.ApplyWaveHealthMultiplier(healthMultiplier);
            }

            if (_weaponSystem == null)
            {
                _weaponSystem = GetComponent<MechRobotBossWeaponSystem_V2>();
            }

            if (_weaponSystem == null)
            {
                _weaponSystem = GetComponentInChildren<MechRobotBossWeaponSystem_V2>(true);
            }

            if (_weaponSystem != null)
            {
                _weaponSystem.ApplyWaveDamageMultiplier(damageMultiplier);
            }
        }

        private void InitializeDependencies()
        {
            if (_model == null)
            {
                _model = GetComponent<MechRobotBossModel_V2>();
            }

            if (_stateMachine == null)
            {
                _stateMachine = GetComponent<MechRobotBossStateMachine_V2>();
            }

            if (_controller == null)
            {
                _controller = GetComponent<MechRobotBossController_V2>();
            }

            if (_view == null)
            {
                _view = GetComponent<MechRobotBossView_V2>();
            }

            if (_damageReceiver == null)
            {
                _damageReceiver = GetComponent<MechRobotBossDamageReceiver_V2>();
            }

            if (_deathHandler == null)
            {
                _deathHandler = GetComponent<MechRobotBossDeathHandler_V2>();
            }

            if (_spineEventForwarder == null)
            {
                _spineEventForwarder = GetComponent<MechRobotBossSpineEventForwarder_V2>();
            }

            if (_weaponSystem == null)
            {
                _weaponSystem = GetComponent<MechRobotBossWeaponSystem_V2>();
            }

            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponent<SkeletonAnimation>();
                if (_skeletonAnimation == null)
                {
                    _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
                }
            }
        }

        private void WireSystems()
        {
            if (_model == null)
            {
                Debug.LogError("[MechRobotBoss] Missing MechRobotBossModel_V2.");
                return;
            }

            _stateMachine.Initialize(_model);
            _controller.Initialize(_model, _stateMachine, _weaponSystem);
            _view.Initialize(_stateMachine);

            if (_deathHandler != null)
            {
                // Death handler has no Initialize in slim version; state drives Die.
            }

            _weaponSystem?.Initialize(_model);

            if (_spineEventForwarder != null && _skeletonAnimation != null)
            {
                _spineEventForwarder.Init(_controller, _skeletonAnimation);
            }

            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
                _stateMachine.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }

            if (_runtimeHealthBarRoot != null)
            {
                Destroy(_runtimeHealthBarRoot);
                _runtimeHealthBarRoot = null;
            }
        }

        private void HandleStateChanged(MechRobotBossBodyState from, MechRobotBossBodyState to)
        {
            if (to == MechRobotBossBodyState.Die && _deathHandler != null)
            {
                _deathHandler.Die();
            }
        }

        private readonly struct MechHitboxSlotDef
        {
            public readonly string SlotName;
            public readonly BodyPartType BodyPart;

            public MechHitboxSlotDef(string slotName, BodyPartType bodyPart)
            {
                SlotName = slotName;
                BodyPart = bodyPart;
            }
        }

        private static readonly MechHitboxSlotDef[] DefaultMechHitboxSlots =
        {
            new MechHitboxSlotDef("controlcabin-bb", BodyPartType.Torso),
            new MechHitboxSlotDef("cannon-bb", BodyPartType.Torso),
            new MechHitboxSlotDef("thigh-front-bb", BodyPartType.LegUpperFront),
            new MechHitboxSlotDef("calf-front-bb", BodyPartType.LegLowerFront),
            new MechHitboxSlotDef("foot-front-bb", BodyPartType.FootFront),
            new MechHitboxSlotDef("toe-front-bb", BodyPartType.FootFront),
            new MechHitboxSlotDef("thigh-back-bb", BodyPartType.LegUpperBack),
            new MechHitboxSlotDef("calf-back-bb", BodyPartType.LegLowerBack),
            new MechHitboxSlotDef("foot-back-bb", BodyPartType.FootBack),
            new MechHitboxSlotDef("toe-back-bb", BodyPartType.FootBack),
        };

        private void EnsureBoundingBoxHitboxes()
        {
            if (_skeletonAnimation == null)
            {
                return;
            }

            DisableLegacyBoxColliderPlaceholder();

            Transform hitboxesRoot = _skeletonAnimation.transform.Find("MechRobot BodyParts_V2");
            if (hitboxesRoot == null)
            {
                var rootGo = new GameObject("MechRobot BodyParts_V2");
                hitboxesRoot = rootGo.transform;
                hitboxesRoot.SetParent(_skeletonAnimation.transform, false);
            }

            WireUserAddedBoundingBoxFollowers();

            for (int i = 0; i < DefaultMechHitboxSlots.Length; i++)
            {
                MechHitboxSlotDef slotDef = DefaultMechHitboxSlots[i];
                if (HasBoundingBoxFollowerForSlot(slotDef.SlotName))
                {
                    continue;
                }

                EnsureHitboxForSlot(hitboxesRoot, slotDef);
            }
        }

        private bool HasBoundingBoxFollowerForSlot(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
            {
                return false;
            }

            BoundingBoxFollower[] followers = GetComponentsInChildren<BoundingBoxFollower>(true);
            for (int i = 0; i < followers.Length; i++)
            {
                BoundingBoxFollower follower = followers[i];
                if (follower != null && follower.slotName == slotName)
                {
                    return true;
                }
            }

            return false;
        }

        private void DisableLegacyBoxColliderPlaceholder()
        {
            BoxCollider2D legacyBox = _skeletonAnimation.GetComponent<BoxCollider2D>();
            if (legacyBox != null)
            {
                legacyBox.enabled = false;
            }

            MechRobotBossBodyPart_V2 legacyRelay = _skeletonAnimation.GetComponent<MechRobotBossBodyPart_V2>();
            if (legacyRelay != null)
            {
                legacyRelay.enabled = false;
            }
        }

        private void EnsureHitboxForSlot(Transform hitboxesRoot, MechHitboxSlotDef slotDef)
        {
            string hitboxName = slotDef.SlotName + "-Hitbox";
            Transform existing = hitboxesRoot.Find(hitboxName);
            GameObject hitboxGo = existing != null ? existing.gameObject : null;
            if (hitboxGo == null)
            {
                hitboxGo = new GameObject(hitboxName);
                hitboxGo.transform.SetParent(hitboxesRoot, false);

                BoundingBoxFollower follower = hitboxGo.AddComponent<BoundingBoxFollower>();
                follower.skeletonRenderer = _skeletonAnimation;
                follower.slotName = slotDef.SlotName;
                follower.isTrigger = false;
            }
            else
            {
                BoundingBoxFollower follower = hitboxGo.GetComponent<BoundingBoxFollower>();
                if (follower == null)
                {
                    follower = hitboxGo.AddComponent<BoundingBoxFollower>();
                }

                follower.skeletonRenderer = _skeletonAnimation;
                if (string.IsNullOrWhiteSpace(follower.slotName))
                {
                    follower.slotName = slotDef.SlotName;
                }

                follower.isTrigger = false;
            }

            MechRobotBossBodyPart_V2 relay = hitboxGo.GetComponent<MechRobotBossBodyPart_V2>();
            if (relay == null)
            {
                relay = hitboxGo.AddComponent<MechRobotBossBodyPart_V2>();
            }

            relay.bodyPart = slotDef.BodyPart;
            relay.enabled = true;
        }

        // Pick up BoundingBoxFollower children the user added in the prefab/scene outside the default list.
        private void WireUserAddedBoundingBoxFollowers()
        {
            BoundingBoxFollower[] followers = GetComponentsInChildren<BoundingBoxFollower>(true);
            for (int i = 0; i < followers.Length; i++)
            {
                BoundingBoxFollower follower = followers[i];
                if (follower == null)
                {
                    continue;
                }

                if (follower.skeletonRenderer == null)
                {
                    follower.skeletonRenderer = _skeletonAnimation;
                }

                MechRobotBossBodyPart_V2 relay = follower.GetComponent<MechRobotBossBodyPart_V2>();
                if (relay == null)
                {
                    relay = follower.gameObject.AddComponent<MechRobotBossBodyPart_V2>();
                }

                relay.bodyPart = InferBodyPartFromSlotName(follower.slotName);
                relay.enabled = true;
            }
        }

        private static BodyPartType InferBodyPartFromSlotName(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
            {
                return BodyPartType.Torso;
            }

            string normalized = slotName.ToLowerInvariant();
            if (normalized.Contains("controlcabin") || normalized.Contains("cannon"))
            {
                return BodyPartType.Torso;
            }

            if (normalized.Contains("thigh-front"))
            {
                return BodyPartType.LegUpperFront;
            }

            if (normalized.Contains("calf-front"))
            {
                return BodyPartType.LegLowerFront;
            }

            if (normalized.Contains("foot-front") || normalized.Contains("toe-front"))
            {
                return BodyPartType.FootFront;
            }

            if (normalized.Contains("thigh-back"))
            {
                return BodyPartType.LegUpperBack;
            }

            if (normalized.Contains("calf-back"))
            {
                return BodyPartType.LegLowerBack;
            }

            if (normalized.Contains("foot-back") || normalized.Contains("toe-back"))
            {
                return BodyPartType.FootBack;
            }

            return BodyPartType.Torso;
        }

        private void PrepareBodyPartHitboxesForSpawn()
        {
            MechRobotBossBodyPart_V2[] parts = GetComponentsInChildren<MechRobotBossBodyPart_V2>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                MechRobotBossBodyPart_V2 part = parts[i];
                if (part == null || !part.enabled)
                {
                    continue;
                }

                part.PrepareForSpawn();
            }
        }

        private Rigidbody2D ResolveBossRigidbody()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = GetComponentInChildren<Rigidbody2D>(true);
            }

            return rb;
        }

        private void PrepareBossGroundAndPhysics(Rigidbody2D rb)
        {
            EnsurePositiveRootScale();
            EnsureBoundingBoxHitboxes();
            PrepareBodyPartHitboxesForSpawn();
            if (_skeletonAnimation != null)
            {
                _skeletonAnimation.Update(0f);
            }

            Physics2D.SyncTransforms();
            AlignFeetToGround();
            ConfigureBossRigidbody(rb);
        }

        private void EnsureWorldHealthBar()
        {
            if (_runtimeHealthBarRoot == null)
            {
                HealthBarCanvas_V2 existing = GetComponentInChildren<HealthBarCanvas_V2>(true);
                if (existing != null)
                {
                    _runtimeHealthBarRoot = existing.gameObject;
                }
                else if (_worldHealthBarCanvasPrefab != null)
                {
                    _runtimeHealthBarRoot = Instantiate(_worldHealthBarCanvasPrefab);
                    _runtimeHealthBarRoot.name = "MechRobotHealthBarCanvas";
                    _healthBarPrefabScaleCached = false;
                }
                else
                {
                    return;
                }
            }

            if (_runtimeHealthBarRoot.transform.parent != null)
            {
                _runtimeHealthBarRoot.transform.SetParent(null, true);
            }

            NormalizeHealthBarCanvasPivot();
            ApplyHealthBarVisualTuning();
            _runtimeHealthBarRoot.SetActive(true);

            WorldHealthBarFollower_V2 follower = _runtimeHealthBarRoot.GetComponent<WorldHealthBarFollower_V2>();
            if (follower == null)
            {
                follower = _runtimeHealthBarRoot.GetComponentInChildren<WorldHealthBarFollower_V2>(true);
            }

            if (follower == null)
            {
                follower = _runtimeHealthBarRoot.GetComponentInParent<WorldHealthBarFollower_V2>();
            }

            follower?.SetFollowTarget(transform);
            follower?.SetWorldAnchorOverride(GetHealthBarAnchorWorld);
            follower?.SetWorldOffset(_healthBarWorldOffset);
            _cachedHealthBarFollower = follower;

            if (_model == null)
            {
                _model = GetComponent<MechRobotBossModel_V2>();
            }

            HealthBarCanvas_V2 barCanvas = _runtimeHealthBarRoot.GetComponentInChildren<HealthBarCanvas_V2>(true);
            if (barCanvas != null && _model != null)
            {
                barCanvas.ConfigureForMechRobotBoss(_model, revealOnDamage: false);
            }
        }

        private void NormalizeHealthBarCanvasPivot()
        {
            if (_runtimeHealthBarRoot == null)
            {
                return;
            }

            RectTransform rt = _runtimeHealthBarRoot.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            Vector2 pivot = rt.pivot;
            if (Mathf.Approximately(pivot.x, 0.5f) && Mathf.Approximately(pivot.y, 0.5f))
            {
                return;
            }

            Vector3 worldBefore = rt.position;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = worldBefore;
        }

        private void ApplyHealthBarVisualTuning()
        {
            if (_runtimeHealthBarRoot == null)
            {
                return;
            }

            RectTransform rt = _runtimeHealthBarRoot.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            if (!_healthBarPrefabScaleCached)
            {
                _healthBarPrefabLocalScale = rt.localScale;
                _healthBarPrefabScaleCached = true;
            }

            float uniform = Mathf.Max(0.05f, _healthBarWorldScale);
            rt.localScale = _healthBarPrefabLocalScale * uniform;
        }

        // Kinematic view RB accumulates local X/Y while the composition root stays at spawn; fold into root each frame.
        private void AbsorbViewLocalMotionIntoRoot()
        {
            Rigidbody2D rb = ResolveBossRigidbody();
            if (rb == null)
            {
                return;
            }

            Transform view = rb.transform;
            if (view == transform)
            {
                return;
            }

            Vector3 local = view.localPosition;
            if (local.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.position += new Vector3(local.x, local.y, 0f);
            view.localPosition = new Vector3(0f, 0f, local.z);
            Physics2D.SyncTransforms();
        }

        public void HideRuntimeHealthBarForDeath()
        {
            if (_runtimeHealthBarRoot != null)
            {
                _runtimeHealthBarRoot.SetActive(false);
            }
        }

        public bool TryGetHealthBarAnchorWorld(out Vector3 worldPoint)
        {
            worldPoint = GetHealthBarAnchorWorld();
            return true;
        }

        private Vector3 GetHealthBarAnchorWorld()
        {
            if (TryGetSpineBoneWorldPoint(_healthBarAnchorBoneName, out Vector3 boneWorld))
            {
                return boneWorld;
            }

            if (_skeletonAnimation != null)
            {
                return _skeletonAnimation.transform.position;
            }

            Rigidbody2D rb = ResolveBossRigidbody();
            return rb != null ? rb.transform.position : transform.position;
        }

        private bool TryGetSpineBoneWorldPoint(string boneName, out Vector3 worldPoint)
        {
            worldPoint = default;
            if (string.IsNullOrWhiteSpace(boneName))
            {
                return false;
            }

            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }

            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return false;
            }

            Bone bone = _skeletonAnimation.Skeleton.FindBone(boneName);
            if (bone == null)
            {
                return false;
            }

            Transform boneSpace = _skeletonAnimation.transform;
            worldPoint = boneSpace.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            return true;
        }

        // Boss is script-driven: kinematic RB, no gravity tumble, feet snapped to Ground on spawn.
        private static void ConfigureBossRigidbody(Rigidbody2D rb)
        {
            if (rb == null)
            {
                return;
            }

            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.rotation = 0f;
        }

        private void EnsurePositiveRootScale()
        {
            Vector3 rootScale = transform.localScale;
            rootScale.x = Mathf.Abs(rootScale.x);
            if (rootScale.x < 0.001f)
            {
                rootScale.x = 1f;
            }

            rootScale.y = Mathf.Abs(rootScale.y);
            if (rootScale.y < 0.001f)
            {
                rootScale.y = 1f;
            }

            transform.localScale = rootScale;

            if (_skeletonAnimation == null)
            {
                return;
            }

            Transform viewTransform = _skeletonAnimation.transform;
            Vector3 viewScale = viewTransform.localScale;
            viewScale.x = Mathf.Abs(viewScale.x);
            if (viewScale.x < 0.001f)
            {
                viewScale.x = 1f;
            }

            viewScale.y = Mathf.Abs(viewScale.y);
            if (viewScale.y < 0.001f)
            {
                viewScale.y = 1f;
            }

            viewTransform.localScale = viewScale;
        }

        private void AlignFeetToGround()
        {
            int groundMask = LayerMask.GetMask("Ground");
            if (groundMask == 0)
            {
                return;
            }

            if (!TryGetFootProbe(out float footY, out float footX))
            {
                return;
            }

            footY += _feetGroundBiasWorld;
            if (!TryResolveGroundSurfaceY(footX, groundMask, footY, out float groundY))
            {
                return;
            }

            float deltaY = groundY - footY;
            if (Mathf.Abs(deltaY) <= 0.001f)
            {
                return;
            }

            transform.position += new Vector3(0f, deltaY, 0f);
            Physics2D.SyncTransforms();
        }

        // Lowest visual foot point from Spine toe/foot bones (origin + bone tip), plus probe X for ground column.
        private bool TryGetFootProbe(out float footY, out float footX)
        {
            footY = float.MaxValue;
            footX = transform.position.x;
            float sumFootX = 0f;
            int footSampleCount = 0;

            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
            }

            if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
            {
                return false;
            }

            Skeleton skeleton = _skeletonAnimation.Skeleton;
            Transform boneSpace = _skeletonAnimation.transform;
            bool any = false;
            for (int i = 0; i < FootProbeBoneNames.Length; i++)
            {
                Bone bone = skeleton.FindBone(FootProbeBoneNames[i]);
                if (bone == null)
                {
                    continue;
                }

                if (TrySampleBoneLowestWorld(bone, boneSpace, ref footY, ref sumFootX, ref footSampleCount))
                {
                    any = true;
                }
            }

            if (any && footSampleCount > 0)
            {
                footX = sumFootX / footSampleCount;
            }

            Rigidbody2D rb = ResolveBossRigidbody();
            if (rb != null)
            {
                footX = any && footSampleCount > 0 ? footX : rb.position.x;
            }

            return any && footY < float.MaxValue * 0.5f;
        }

        private static bool TrySampleBoneLowestWorld(
            Bone bone,
            Transform boneSpace,
            ref float footY,
            ref float sumFootX,
            ref int footSampleCount)
        {
            if (bone == null || boneSpace == null)
            {
                return false;
            }

            Vector3 originWorld = boneSpace.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            footY = Mathf.Min(footY, originWorld.y);
            sumFootX += originWorld.x;
            footSampleCount++;

            bone.LocalToWorld(bone.Data.Length, 0f, out float tipSkeletonX, out float tipSkeletonY);
            Vector3 tipWorld = boneSpace.TransformPoint(new Vector3(tipSkeletonX, tipSkeletonY, 0f));
            footY = Mathf.Min(footY, tipWorld.y);
            sumFootX += tipWorld.x;
            footSampleCount++;
            return true;
        }

        // Prefer the lowest Ground hit in the column (thin strips above the main snow floor otherwise win).
        private bool TryResolveGroundSurfaceY(float worldX, int groundMask, float referenceFootY, out float groundY)
        {
            groundY = 0f;
            float skyStartY = referenceFootY + 40f;
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                skyStartY = Mathf.Max(skyStartY, cam.transform.position.y + cam.orthographicSize + 2f);
            }

            const float rayLengthWorld = 160f;
            if (TrySelectLowestGroundHit(
                    Physics2D.RaycastAll(new Vector2(worldX, skyStartY), Vector2.down, rayLengthWorld, groundMask),
                    out groundY))
            {
                return true;
            }

            if (cam == null || !cam.orthographic)
            {
                return false;
            }

            float camTopY = cam.transform.position.y + cam.orthographicSize + 2f;
            return TrySelectLowestGroundHit(
                Physics2D.RaycastAll(new Vector2(cam.transform.position.x, camTopY), Vector2.down, rayLengthWorld, groundMask),
                out groundY);
        }

        private bool TrySelectLowestGroundHit(RaycastHit2D[] hits, out float groundY)
        {
            groundY = 0f;
            bool found = false;
            float bestY = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D col = hits[i].collider;
                if (col == null || col.isTrigger)
                {
                    continue;
                }

                if (col.transform.IsChildOf(transform))
                {
                    continue;
                }

                float hitY = hits[i].point.y;
                if (hitY >= bestY)
                {
                    continue;
                }

                bestY = hitY;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            groundY = bestY;
            return true;
        }
    }
}
