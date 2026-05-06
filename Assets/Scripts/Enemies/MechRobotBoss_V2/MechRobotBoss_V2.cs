using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>Composition root for Mech Robot Boss (Spine: idle/walk/run/aim/shoot).</summary>
    [DefaultExecutionOrder(400)]
    public sealed class MechRobotBoss : MonoBehaviour
    {
        [SerializeField] private MechRobotBossModel_V2 _model;
        [SerializeField] private MechRobotBossStateMachine_V2 _stateMachine;
        [SerializeField] private MechRobotBossController_V2 _controller;
        [SerializeField] private MechRobotBossView_V2 _view;
        [SerializeField] private MechRobotBossWeaponSystem_V2 _weaponSystem;
        [SerializeField] private MechRobotBossDamageReceiver_V2 _damageReceiver;
        [SerializeField] private MechRobotBossDeathHandler_V2 _deathHandler;
        [SerializeField] private MechRobotBossSpineEventForwarder_V2 _spineEventForwarder;
        [SerializeField] private SkeletonAnimation _skeletonAnimation;

        private void Awake()
        {
            InitializeDependencies();
            WireSystems();
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

            // RB/colliders often live on the Spine view child (root is composition only).
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = GetComponentInChildren<Rigidbody2D>(true);
            }

            if (rb != null)
            {
                rb.simulated = true;
                if (rb.bodyType != RigidbodyType2D.Dynamic)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                }

                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            Collider2D rootCol = GetComponent<Collider2D>();
            if (rootCol == null)
            {
                rootCol = GetComponentInChildren<Collider2D>(true);
            }

            if (rootCol != null)
            {
                rootCol.enabled = true;
            }

            _controller?.StartGame();
        }

        /// <summary>Applied by <see cref="EnemySpawner_V2"/> after spawn.</summary>
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
        }

        private void HandleStateChanged(MechRobotBossBodyState from, MechRobotBossBodyState to)
        {
            if (to == MechRobotBossBodyState.Die && _deathHandler != null)
            {
                _deathHandler.Die();
            }
        }
    }
}
