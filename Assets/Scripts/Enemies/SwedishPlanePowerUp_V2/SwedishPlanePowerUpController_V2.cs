using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUpController_V2 — parachute deploy descent, land touchdown, hero pickup.
     * Pickup uses Spine bounding boxes (powerup-bb) as triggers; hero walks through the crate.
     */
    [DefaultExecutionOrder(100)]
    public sealed class SwedishPlanePowerUpController_V2 : MonoBehaviour
    {
        private const string DefaultPickupHitboxObjectName = "PickupHitbox_V2";

        [Header("Descent")]
        // Fallback only when Deploy clip progress cannot be read (no view).
        [SerializeField] private float _deployDescentSpeed = 0.7f;
        [SerializeField] private float _groundProbeMaxDistance = 30f;
        [SerializeField] private string _groundLayerName = "Ground";
        // Used when bbox sampling fails — keeps composition root above probed surface instead of on it.
        [SerializeField] private float _landingRootFallbackAboveSurface = 2.5f;

        [Header("Pickup")]
        [SerializeField] private string _pickupBoundingBoxSlotName = "powerup-bb";
        [SerializeField] private float _pickupRadius = 1.35f;
        [SerializeField] private float _pickupRadiusFallbackMultiplier = 1.6f;
        [SerializeField] private float _pickupBoundsPadding = 0.12f;
        [SerializeField] private float _landPickupEnableFallbackSeconds = 0.55f;
        [SerializeField] private float _maxLifetimeSeconds = 90f;

        private SwedishPlanePowerUpModel_V2 _model;
        private SwedishPlanePowerUpStateMachine_V2 _stateMachine;
        private SwedishPlanePowerUpView_V2 _view;
        private SwedishPlanePowerUpRewardPreview_V2 _rewardPreview;
        private WaveManager_V2 _waveManager;
        private Hero_V2 _hero;
        private Rigidbody2D _pickupRigidbody;
        private Collider2D[] _pickupTriggerColliders;
        private float _expireAt;
        private float _spawnY;
        private float _targetGroundY;
        private float _probedSurfaceY;
        private float _touchdownRootY;
        private bool _hasTargetGroundY;
        private int _groundLayerMask;
        private float _landStateEnteredAt = -1f;
        private bool _deployClipFinished;
        private bool _descentFinished;
        private static HeroModel_V2 s_cachedHeroModel;

        public void Initialize(
            SwedishPlanePowerUpModel_V2 model,
            SwedishPlanePowerUpStateMachine_V2 stateMachine,
            SwedishPlanePowerUpView_V2 view,
            SwedishPlanePowerUpRewardPreview_V2 rewardPreview = null)
        {
            _model = model;
            _stateMachine = stateMachine;
            _view = view;
            _rewardPreview = rewardPreview;

            if (_view != null)
            {
                _view.DeployClipCompleted -= HandleDeployClipCompleted;
                _view.DeployClipCompleted += HandleDeployClipCompleted;
                _view.LandClipCompleted -= HandleLandClipCompleted;
                _view.LandClipCompleted += HandleLandClipCompleted;
            }

            EnsurePickupPhysics();
            ResolveGroundLayerMask();
            ResolveRewardPreviewIfNeeded();
        }

        internal void BindRewardPreview(SwedishPlanePowerUpRewardPreview_V2 rewardPreview)
        {
            _rewardPreview = rewardPreview;
        }

        private void ResolveRewardPreviewIfNeeded()
        {
            if (_rewardPreview != null)
            {
                return;
            }

            _rewardPreview = GetComponentInChildren<SwedishPlanePowerUpRewardPreview_V2>(true);
        }

        public void BeginDrop(SurvivalPowerUpOffer_V2 offer, WaveManager_V2 waveManager, Hero_V2 hero)
        {
            _waveManager = waveManager;
            _hero = hero;
            if (_hero == null)
            {
                _hero = FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Include);
            }

            if (_model == null || _stateMachine == null)
            {
                return;
            }

            _model.rolledOffer = offer;
            _model.pickupEnabled = false;
            _landStateEnteredAt = -1f;
            _deployClipFinished = false;
            _descentFinished = false;
            _expireAt = Time.time + Mathf.Max(8f, _maxLifetimeSeconds);
            _spawnY = transform.position.y;

            EnsurePickupPhysics();
            _view?.PrepareDeployPoseForLandingSample();
            _hasTargetGroundY = TryProbeGroundY(transform.position.x, out _probedSurfaceY);
            if (_hasTargetGroundY)
            {
                // Deploy pose includes parachute bbox — idle sampling would target too low and fight descent.
                _targetGroundY = ComputeLandingRootY(_probedSurfaceY, useIdlePoseForMeasure: false);
                _touchdownRootY = _targetGroundY;
            }

            SetPickupTriggerCollidersEnabled(false);
            ApplyHeroWalkThroughIgnoreCollisions();

            _stateMachine.ChangeState(SwedishPlanePowerUpState_V2.Deploy);
            transform.position = new Vector3(transform.position.x, _spawnY, transform.position.z);
            SurvivalPowerUpRewardHud_V2.EnsureInitializedFromScene();
            _rewardPreview?.ClearForSpawn();
        }

        internal void NotifyPickupTrigger(Collider2D other)
        {
            TryPickupFromCollider(other);
        }

        private void Update()
        {
            if (_model == null || _stateMachine == null)
            {
                return;
            }

            SwedishPlanePowerUpState_V2 state = _stateMachine.CurrentState;
            if (state == SwedishPlanePowerUpState_V2.PickedUp)
            {
                return;
            }

            if (state == SwedishPlanePowerUpState_V2.Deploy)
            {
                // Descent is driven in LateUpdate so Y tracks Deploy clip progress after Spine advances.
            }

            if (state == SwedishPlanePowerUpState_V2.Land)
            {
                LockTouchdownRootY();
                TryEnablePickupAfterLandFallback();
                TryProximityPickup();
            }

            if (Time.time >= _expireAt)
            {
                DespawnSelf();
            }
        }

        private void LateUpdate()
        {
            if (_model == null || _stateMachine == null)
            {
                return;
            }

            if (_stateMachine.CurrentState == SwedishPlanePowerUpState_V2.Deploy)
            {
                TickDeployDescentSyncedToClip();
            }
        }

        private void TickDeployDescentSyncedToClip()
        {
            if (!_hasTargetGroundY)
            {
                return;
            }

            if (_view != null)
            {
                float progress = _view.GetDeployClipProgress01();
                ApplyMonotonicDeployDescentY(progress);
                return;
            }

            float speed = Mathf.Max(0.1f, _deployDescentSpeed);
            float nextY = transform.position.y - speed * Time.deltaTime;
            nextY = Mathf.Max(_touchdownRootY, nextY);
            transform.position = new Vector3(transform.position.x, nextY, transform.position.z);
            ApplyDeployDescentFloorClamp();
        }

        // Clip progress only lowers root Y; floor clamp keeps Deploy parachute + crate above probed Ground.
        private void ApplyMonotonicDeployDescentY(float deployClipProgress01)
        {
            RefreshTouchdownRootYFromDeployPose();
            float idealY = Mathf.Lerp(_spawnY, _touchdownRootY, deployClipProgress01);
            float newY = Mathf.Min(transform.position.y, idealY);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            ApplyDeployDescentFloorClamp();
        }

        private void RefreshTouchdownRootYFromDeployPose()
        {
            if (!_hasTargetGroundY || _view == null)
            {
                return;
            }

            SetPickupTriggerCollidersEnabled(true);
            RefreshPickupTriggerColliders();
            if (_view.TrySampleLandingRootY(
                    transform,
                    _probedSurfaceY,
                    _pickupTriggerColliders,
                    out float sampledRootY,
                    useIdlePoseForMeasure: false))
            {
                _touchdownRootY = Mathf.Max(_touchdownRootY, sampledRootY);
            }

            SetPickupTriggerCollidersEnabled(false);
        }

        private void ApplyDeployDescentFloorClamp()
        {
            if (!_hasTargetGroundY || _view == null)
            {
                return;
            }

            SetPickupTriggerCollidersEnabled(true);
            RefreshPickupTriggerColliders();
            if (_view.TryGetMinimumRootYForDescentVisualOnSurface(
                    transform,
                    _probedSurfaceY,
                    _pickupTriggerColliders,
                    out float minimumRootY) &&
                transform.position.y < minimumRootY - 0.001f)
            {
                transform.position = new Vector3(transform.position.x, minimumRootY, transform.position.z);
                _touchdownRootY = Mathf.Max(_touchdownRootY, minimumRootY);
                _targetGroundY = transform.position.y;
            }

            SetPickupTriggerCollidersEnabled(false);
        }

        private bool IsCrateBottomAlignedWithSurface(bool useIdlePoseForMeasure, float epsilon = 0.05f)
        {
            if (_view == null)
            {
                return false;
            }

            SetPickupTriggerCollidersEnabled(true);
            RefreshPickupTriggerColliders();
            bool aligned = _view.IsCrateBottomNearSurface(
                _probedSurfaceY,
                _pickupTriggerColliders,
                useIdlePoseForMeasure,
                epsilon);
            SetPickupTriggerCollidersEnabled(false);
            return aligned;
        }

        private void HandleDeployClipCompleted()
        {
            _deployClipFinished = true;
            FinalizeDeployTouchdownHeight();
            _descentFinished = true;
            TryBeginLandState();
        }

        private void FinalizeDeployTouchdownHeight()
        {
            if (!_hasTargetGroundY)
            {
                return;
            }

            EnforceCrateBottomOnOrAboveSurface(useIdlePoseForMeasure: false);
        }

        private void EnforceCrateBottomOnOrAboveSurface(bool useIdlePoseForMeasure)
        {
            if (!_hasTargetGroundY || _view == null)
            {
                return;
            }

            SetPickupTriggerCollidersEnabled(true);
            RefreshPickupTriggerColliders();
            _view.AlignRootSoCrateBottomOnSurface(
                transform,
                _probedSurfaceY,
                _pickupTriggerColliders,
                useIdlePoseForMeasure);
            SetPickupTriggerCollidersEnabled(false);
            _targetGroundY = transform.position.y;
            _touchdownRootY = transform.position.y;
        }

        private void LockTouchdownRootY()
        {
            if (!_hasTargetGroundY)
            {
                return;
            }

            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - _touchdownRootY) > 0.001f)
            {
                pos.y = _touchdownRootY;
                transform.position = pos;
            }
        }

        private void TryBeginLandState()
        {
            if (_model == null || _stateMachine == null)
            {
                return;
            }

            if (!_deployClipFinished || !_descentFinished)
            {
                return;
            }

            if (_stateMachine.CurrentState != SwedishPlanePowerUpState_V2.Deploy)
            {
                return;
            }

            _stateMachine.ChangeState(SwedishPlanePowerUpState_V2.Land);
            _landStateEnteredAt = Time.time;
        }

        private void HandleLandClipCompleted()
        {
            if (!IsCrateBottomAlignedWithSurface(useIdlePoseForMeasure: true))
            {
                SnapCrateBottomToSurface();
            }
            else
            {
                _touchdownRootY = transform.position.y;
                _targetGroundY = _touchdownRootY;
            }

            EnablePickup();
        }

        private void TryEnablePickupAfterLandFallback()
        {
            if (_model.pickupEnabled || _landStateEnteredAt < 0f)
            {
                return;
            }

            if (Time.time - _landStateEnteredAt >= Mathf.Max(0.1f, _landPickupEnableFallbackSeconds))
            {
                EnablePickup();
            }
        }

        private void EnablePickup()
        {
            if (_model == null)
            {
                return;
            }

            _model.pickupEnabled = true;
            EnsurePickupPhysics();
            SetPickupTriggerCollidersEnabled(true);
            ApplyHeroWalkThroughIgnoreCollisions();
        }

        private void TryProximityPickup()
        {
            if (_model == null || !_model.pickupEnabled || _stateMachine == null)
            {
                return;
            }

            if (_stateMachine.CurrentState != SwedishPlanePowerUpState_V2.Land)
            {
                return;
            }

            Hero_V2 hero = ResolveHero();
            if (hero == null || hero.IsDead())
            {
                return;
            }

            if (!IsHeroWithinPickupRange(hero.transform.position))
            {
                return;
            }

            TryPickupHero(hero);
        }

        private void TryPickupFromCollider(Collider2D other)
        {
            if (_model == null || !_model.pickupEnabled || _stateMachine == null)
            {
                return;
            }

            if (_stateMachine.CurrentState != SwedishPlanePowerUpState_V2.Land)
            {
                return;
            }

            Hero_V2 hero = ResolveHeroFromCollider(other);
            if (hero == null || hero.IsDead())
            {
                return;
            }

            TryPickupHero(hero);
        }

        private void TryPickupHero(Hero_V2 hero)
        {
            if (hero == null || hero.IsDead())
            {
                return;
            }

            if (_model.rolledOffer == null)
            {
                return;
            }

            SurvivalPowerUpApplicator_V2.TryApplyForPickup(_waveManager, hero, _model.rolledOffer);
            AudioManager_V2.PlayPurchaseSuccess();
            SurvivalPowerUpRewardHud_V2.ShowPickupReward(_model.rolledOffer);
            _rewardPreview?.ClearForSpawn();
            _stateMachine.ChangeState(SwedishPlanePowerUpState_V2.PickedUp);
            DespawnSelf();
        }

        private Hero_V2 ResolveHeroFromCollider(Collider2D other)
        {
            if (other == null)
            {
                return null;
            }

            Hero_V2 hero = other.GetComponentInParent<Hero_V2>();
            if (hero != null)
            {
                return hero;
            }

            HeroModel_V2 model = other.GetComponentInParent<HeroModel_V2>();
            return model != null ? model.GetComponentInParent<Hero_V2>() : null;
        }

        private Hero_V2 ResolveHero()
        {
            if (_hero != null && !_hero.IsDead())
            {
                return _hero;
            }

            _hero = FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Include);
            return _hero;
        }

        private bool IsHeroWithinPickupRange(Vector3 heroWorldPosition)
        {
            if (TryGetPickupWorldBounds(out Bounds bounds))
            {
                Vector3 closest = bounds.ClosestPoint(heroWorldPosition);
                float padding = Mathf.Max(0f, _pickupBoundsPadding);
                float dx = heroWorldPosition.x - closest.x;
                float dy = heroWorldPosition.y - closest.y;
                return dx * dx + dy * dy <= padding * padding;
            }

            Vector3 pickupCenter = GetPickupWorldCenter();
            float radius = Mathf.Max(0.25f, _pickupRadius) * Mathf.Max(1f, _pickupRadiusFallbackMultiplier);
            float fallbackDx = heroWorldPosition.x - pickupCenter.x;
            float fallbackDy = heroWorldPosition.y - pickupCenter.y;
            return fallbackDx * fallbackDx + fallbackDy * fallbackDy <= radius * radius;
        }

        private Vector3 GetPickupWorldCenter()
        {
            if (TryGetPickupWorldBounds(out Bounds bounds))
            {
                return bounds.center;
            }

            return _view != null ? _view.GetPickupWorldCenter() : transform.position;
        }

        private bool TryGetPickupWorldBounds(out Bounds bounds)
        {
            bounds = default;
            RefreshPickupTriggerColliders();
            if (_pickupTriggerColliders == null || _pickupTriggerColliders.Length == 0)
            {
                return false;
            }

            bool hasBounds = false;
            for (int i = 0; i < _pickupTriggerColliders.Length; i++)
            {
                Collider2D pickupCollider = _pickupTriggerColliders[i];
                if (pickupCollider == null || !pickupCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = pickupCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(pickupCollider.bounds);
                }
            }

            return hasBounds;
        }

        private float ComputeLandingRootY(float probedSurfaceY, bool useIdlePoseForMeasure)
        {
            SetPickupTriggerCollidersEnabled(true);
            RefreshPickupTriggerColliders();

            float landingRootY = probedSurfaceY + Mathf.Max(0f, _landingRootFallbackAboveSurface);
            if (_view != null &&
                _view.TrySampleLandingRootY(
                    transform,
                    probedSurfaceY,
                    _pickupTriggerColliders,
                    out float alignedRootY,
                    useIdlePoseForMeasure))
            {
                landingRootY = alignedRootY;
            }

            SetPickupTriggerCollidersEnabled(false);
            return landingRootY;
        }

        private void SnapCrateBottomToSurface(bool useIdlePoseForMeasure = true)
        {
            EnforceCrateBottomOnOrAboveSurface(useIdlePoseForMeasure);
        }

        private bool TryProbeGroundY(float worldX, out float groundY)
        {
            groundY = transform.position.y;
            if (_groundLayerMask == 0)
            {
                ResolveGroundLayerMask();
            }

            if (_groundLayerMask == 0)
            {
                return false;
            }

            float probeTop = transform.position.y + 1f;
            float probeDist = Mathf.Max(1f, _groundProbeMaxDistance);
            Vector2 origin = new Vector2(worldX, probeTop);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, probeDist, _groundLayerMask);
            if (hit.collider == null)
            {
                return false;
            }

            groundY = hit.point.y;
            return true;
        }

        private void ResolveGroundLayerMask()
        {
            int groundLayer = LayerMask.NameToLayer(_groundLayerName);
            _groundLayerMask = groundLayer >= 0 ? 1 << groundLayer : 0;
        }

        private void EnsurePickupPhysics()
        {
            DisableRootBlockingColliders();

            if (_pickupRigidbody == null)
            {
                _pickupRigidbody = GetComponent<Rigidbody2D>();
                if (_pickupRigidbody == null)
                {
                    _pickupRigidbody = gameObject.AddComponent<Rigidbody2D>();
                }
            }

            _pickupRigidbody.bodyType = RigidbodyType2D.Kinematic;
            _pickupRigidbody.simulated = true;
            _pickupRigidbody.gravityScale = 0f;
            _pickupRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;

            EnsureSpineBoundingBoxPickupCollider();
            RefreshPickupTriggerColliders();
            ConfigurePickupTriggerColliders();
            ApplyHeroWalkThroughIgnoreCollisions();
        }

        private void DisableRootBlockingColliders()
        {
            Collider2D[] rootColliders = GetComponents<Collider2D>();
            for (int i = 0; i < rootColliders.Length; i++)
            {
                Collider2D rootCollider = rootColliders[i];
                if (rootCollider == null)
                {
                    continue;
                }

                // Legacy placeholder boxes on the composition root should never block hero movement.
                rootCollider.enabled = false;
            }
        }

        private void EnsureSpineBoundingBoxPickupCollider()
        {
            if (_view == null || string.IsNullOrWhiteSpace(_pickupBoundingBoxSlotName))
            {
                return;
            }

            SkeletonAnimation skeletonAnimation = _view.SkeletonAnimation;
            if (skeletonAnimation == null)
            {
                return;
            }

            GameObject hitboxObject = TryFindExistingBoundingBoxHitboxObject(skeletonAnimation);
            if (hitboxObject == null)
            {
                Transform hitboxRoot = skeletonAnimation.transform.Find(DefaultPickupHitboxObjectName);
                if (hitboxRoot == null)
                {
                    hitboxObject = new GameObject(DefaultPickupHitboxObjectName);
                    hitboxObject.transform.SetParent(skeletonAnimation.transform, false);
                }
                else
                {
                    hitboxObject = hitboxRoot.gameObject;
                }
            }

            BoundingBoxFollower follower = hitboxObject.GetComponent<BoundingBoxFollower>();
            if (follower == null)
            {
                follower = hitboxObject.AddComponent<BoundingBoxFollower>();
            }

            follower.skeletonRenderer = skeletonAnimation;
            follower.slotName = _pickupBoundingBoxSlotName;
            follower.isTrigger = true;

            if (hitboxObject.GetComponent<PolygonCollider2D>() == null)
            {
                hitboxObject.AddComponent<PolygonCollider2D>();
            }

            SwedishPlanePowerUpPickupTrigger_V2 triggerRelay =
                hitboxObject.GetComponent<SwedishPlanePowerUpPickupTrigger_V2>();
            if (triggerRelay == null)
            {
                triggerRelay = hitboxObject.AddComponent<SwedishPlanePowerUpPickupTrigger_V2>();
            }

            triggerRelay.Bind(this);
        }

        private GameObject TryFindExistingBoundingBoxHitboxObject(SkeletonAnimation skeletonAnimation)
        {
            BoundingBoxFollower[] followers = skeletonAnimation.GetComponentsInChildren<BoundingBoxFollower>(true);
            for (int i = 0; i < followers.Length; i++)
            {
                BoundingBoxFollower follower = followers[i];
                if (follower == null || follower.slotName != _pickupBoundingBoxSlotName)
                {
                    continue;
                }

                return follower.gameObject;
            }

            return null;
        }

        private void RefreshPickupTriggerColliders()
        {
            Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
            if (allColliders == null || allColliders.Length == 0)
            {
                _pickupTriggerColliders = null;
                return;
            }

            int count = 0;
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider2D collider = allColliders[i];
                if (collider != null && collider.gameObject != gameObject)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                _pickupTriggerColliders = null;
                return;
            }

            var pickupColliders = new Collider2D[count];
            int writeIndex = 0;
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider2D collider = allColliders[i];
                if (collider == null || collider.gameObject == gameObject)
                {
                    continue;
                }

                pickupColliders[writeIndex++] = collider;
            }

            _pickupTriggerColliders = pickupColliders;
        }

        private void ConfigurePickupTriggerColliders()
        {
            RefreshPickupTriggerColliders();
            if (_pickupTriggerColliders == null)
            {
                return;
            }

            for (int i = 0; i < _pickupTriggerColliders.Length; i++)
            {
                Collider2D pickupCollider = _pickupTriggerColliders[i];
                if (pickupCollider == null)
                {
                    continue;
                }

                pickupCollider.isTrigger = true;
            }
        }

        private void SetPickupTriggerCollidersEnabled(bool enabled)
        {
            RefreshPickupTriggerColliders();
            if (_pickupTriggerColliders == null)
            {
                return;
            }

            for (int i = 0; i < _pickupTriggerColliders.Length; i++)
            {
                Collider2D pickupCollider = _pickupTriggerColliders[i];
                if (pickupCollider != null)
                {
                    pickupCollider.enabled = enabled;
                }
            }
        }

        private void ApplyHeroWalkThroughIgnoreCollisions()
        {
            Collider2D[] heroColliders = CollectHeroMovementColliders();
            Collider2D[] powerupColliders = GetComponentsInChildren<Collider2D>(true);
            if (heroColliders == null || powerupColliders == null)
            {
                return;
            }

            for (int i = 0; i < powerupColliders.Length; i++)
            {
                Collider2D powerupCollider = powerupColliders[i];
                if (powerupCollider == null || !powerupCollider.enabled || powerupCollider.isTrigger)
                {
                    continue;
                }

                for (int h = 0; h < heroColliders.Length; h++)
                {
                    Collider2D heroCollider = heroColliders[h];
                    if (heroCollider == null || !heroCollider.enabled || heroCollider.isTrigger)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(powerupCollider, heroCollider, true);
                }
            }
        }

        private static Collider2D[] CollectHeroMovementColliders()
        {
            if (s_cachedHeroModel == null)
            {
                s_cachedHeroModel = Object.FindAnyObjectByType<HeroModel_V2>();
            }

            if (s_cachedHeroModel == null)
            {
                return null;
            }

            Rigidbody2D heroRb = s_cachedHeroModel.GetComponent<Rigidbody2D>();
            if (heroRb == null)
            {
                return s_cachedHeroModel.GetComponentsInChildren<Collider2D>(true);
            }

            var scratch = new Collider2D[32];
            int count = heroRb.GetAttachedColliders(scratch);
            if (count <= 0)
            {
                Collider2D rootCol = s_cachedHeroModel.GetComponent<Collider2D>();
                return rootCol != null ? new[] { rootCol } : null;
            }

            var result = new Collider2D[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = scratch[i];
            }

            return result;
        }

        private void DespawnSelf()
        {
            SimplePrefabPool_V2.Despawn(gameObject);
        }

        private void OnDestroy()
        {
            if (_view != null)
            {
                _view.DeployClipCompleted -= HandleDeployClipCompleted;
                _view.LandClipCompleted -= HandleLandClipCompleted;
            }
        }
    }
}
