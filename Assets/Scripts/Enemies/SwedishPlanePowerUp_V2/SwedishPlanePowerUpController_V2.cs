using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlanePowerUpController_V2 — parachute deploy descent, land touchdown, hero pickup.
     */
    [RequireComponent(typeof(Collider2D))]
    public sealed class SwedishPlanePowerUpController_V2 : MonoBehaviour
    {
        [Header("Descent")]
        [SerializeField] private float _deployDescentSpeed = 1.1f;
        [SerializeField] private float _groundProbeMaxDistance = 30f;
        [SerializeField] private string _groundLayerName = "Ground";

        [Header("Pickup")]
        [SerializeField] private float _pickupRadius = 1.35f;
        [SerializeField] private float _pickupRadiusFallbackMultiplier = 1.6f;
        [SerializeField] private float _landPickupEnableFallbackSeconds = 0.55f;
        [SerializeField] private float _maxLifetimeSeconds = 90f;

        private SwedishPlanePowerUpModel_V2 _model;
        private SwedishPlanePowerUpStateMachine_V2 _stateMachine;
        private SwedishPlanePowerUpView_V2 _view;
        private WaveManager_V2 _waveManager;
        private Hero_V2 _hero;
        private CircleCollider2D _pickupCollider;
        private Rigidbody2D _pickupRigidbody;
        private float _expireAt;
        private float _targetGroundY;
        private bool _hasTargetGroundY;
        private int _groundLayerMask;
        private float _landStateEnteredAt = -1f;

        public void Initialize(
            SwedishPlanePowerUpModel_V2 model,
            SwedishPlanePowerUpStateMachine_V2 stateMachine,
            SwedishPlanePowerUpView_V2 view)
        {
            _model = model;
            _stateMachine = stateMachine;
            _view = view;

            if (_view != null)
            {
                _view.DeployClipCompleted -= HandleDeployClipCompleted;
                _view.DeployClipCompleted += HandleDeployClipCompleted;
                _view.LandClipCompleted -= HandleLandClipCompleted;
                _view.LandClipCompleted += HandleLandClipCompleted;
            }

            EnsurePickupPhysics();
            ResolveGroundLayerMask();
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
            _expireAt = Time.time + Mathf.Max(8f, _maxLifetimeSeconds);
            _hasTargetGroundY = TryProbeGroundY(transform.position.x, out _targetGroundY);

            EnsurePickupPhysics();
            if (_pickupCollider != null)
            {
                _pickupCollider.enabled = false;
            }

            _stateMachine.ChangeState(SwedishPlanePowerUpState_V2.Deploy);
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
                TickDeployDescent();
            }

            if (state == SwedishPlanePowerUpState_V2.Land)
            {
                TryEnablePickupAfterLandFallback();
                TryProximityPickup();
            }

            if (Time.time >= _expireAt)
            {
                DespawnSelf();
            }
        }

        private void TickDeployDescent()
        {
            if (!_hasTargetGroundY)
            {
                return;
            }

            Vector3 pos = transform.position;
            if (pos.y <= _targetGroundY + 0.02f)
            {
                pos.y = _targetGroundY;
                transform.position = pos;
                return;
            }

            float speed = Mathf.Max(0.1f, _deployDescentSpeed);
            pos.y = Mathf.Max(_targetGroundY, pos.y - speed * Time.deltaTime);
            transform.position = pos;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryPickupFromCollider(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryPickupFromCollider(other);
        }

        private void HandleDeployClipCompleted()
        {
            if (_model == null || _stateMachine == null)
            {
                return;
            }

            if (_hasTargetGroundY)
            {
                Vector3 pos = transform.position;
                pos.y = _targetGroundY;
                transform.position = pos;
            }

            _stateMachine.ChangeState(SwedishPlanePowerUpState_V2.Land);
            _landStateEnteredAt = Time.time;
        }

        private void HandleLandClipCompleted()
        {
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
            if (_pickupCollider != null)
            {
                _pickupCollider.enabled = true;
            }

            SyncPickupColliderToVisual();
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

            Vector3 pickupCenter = GetPickupWorldCenter();
            float radius = Mathf.Max(0.25f, _pickupRadius) * Mathf.Max(1f, _pickupRadiusFallbackMultiplier);
            float dx = hero.transform.position.x - pickupCenter.x;
            float dy = hero.transform.position.y - pickupCenter.y;
            if (dx * dx + dy * dy > radius * radius)
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
            if (!SurvivalPowerUpApplicator_V2.TryApplyForPickup(_waveManager, hero, _model.rolledOffer))
            {
                return;
            }

            AudioManager_V2.PlayPurchaseSuccess();
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

        private Vector3 GetPickupWorldCenter()
        {
            return _view != null ? _view.GetPickupWorldCenter() : transform.position;
        }

        private void SyncPickupColliderToVisual()
        {
            if (_pickupCollider == null)
            {
                return;
            }

            Vector3 worldCenter = GetPickupWorldCenter();
            Vector3 local = transform.InverseTransformPoint(worldCenter);
            _pickupCollider.offset = new Vector2(local.x, local.y);
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

            if (_pickupCollider == null)
            {
                _pickupCollider = GetComponent<CircleCollider2D>();
                if (_pickupCollider == null)
                {
                    _pickupCollider = gameObject.AddComponent<CircleCollider2D>();
                }
            }

            _pickupCollider.isTrigger = true;
            _pickupCollider.radius = Mathf.Max(0.25f, _pickupRadius);
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
