using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using iStick2War;

namespace iStick2War_V2
{
    /*
 * Hero_V2 Architecture Principle
 *
 * Hero_V2 acts as the COMPOSITION ROOT (entry point) of the character system.
 *
 * ❌ It MUST NOT contain any gameplay or runtime logic:
 * - Movement logic
 * - Input handling
 * - Animation logic
 * - State logic
 * - Damage logic
 *
 * ✅ It is ONLY responsible for:
 * - Creating and collecting references
 * - Initializing all subsystems
 * - Wiring dependencies between systems
 * - Providing a clear lifecycle entry point (Init / Tick)
 *
 * ---------------------------------------------------------
 * ARCHITECTURE MODEL
 *
 * Hero_V2   = Composition Root (main entry point / bootstrap)
 * Controller = Brain (decision making / gameplay logic)
 * Systems    = Organs (weapon, damage, state, etc.)
 * Model      = DNA (pure data, no behaviour)
 * View       = Body (visual representation: animation, VFX)
 *
 * ---------------------------------------------------------
 * DESIGN GOAL
 *
 * Hero_V2 should remain extremely thin and stable.
 * It should only coordinate systems, never implement logic.
 *
 * This ensures:
 * - High modularity
 * - Easy testing and debugging
 * - Clear separation of concerns
 * - Scalable architecture for future systems
 */
    public class Hero_V2 : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _debugLifecycleLogs = false;
        [SerializeField] private bool _debugDamageLogs = false;

        [Header("Core References")]
        [SerializeField] private HeroModel_V2 _model;
        [SerializeField] private HeroView_V2 _view;
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [Header("Weapons")]
        [SerializeField] private List<HeroWeaponDefinition_V2> _initialWeapons = new List<HeroWeaponDefinition_V2>();
        [SerializeField] private WeaponType _startingWeapon = WeaponType.Thompson;

        private HeroInput_V2 _input;
        private HeroController_V2 _controller;
        private HeroStateMachine_V2 _stateMachine;
        private HeroSpineEventForwarder_V2 _spineEventForwarder;

        private HeroMovementSystem_V2 _movementSystem;
        private HeroWeaponSystem_V2 _weaponSystem;

        private HeroDamageReceiver_V2 _damageReceiver;
        private HeroDeathHandler_V2 _deathHandler;

        private void Awake()
        {
            if (_debugLifecycleLogs)
            {
                Debug.Log("HERE AWAKE");
            }
            BindComponents();
            CreateSystems();
            InitSystems();

            // -------------------------
            // EVENTS
            // -------------------------

            _damageReceiver.OnDamageTaken += _view.PlayHitEffect;

            _damageReceiver.OnDeath += _deathHandler.HandleDeath;

            _deathHandler.OnDeathHandled += _view.PlayDeathEffect;

            _deathHandler.OnDeathHandled += OnGameOver;
        }

        /*
         * damageReceiver.OnDamageTaken += view.PlayHitEffect;
damageReceiver.OnDeath += deathHandler.HandleDeath;
        deathHandler.OnDeathHandled += view.PlayDeathEffect;
        deathHandler.OnDeathHandled += () => Debug.Log("GAME OVER");
        */

        private void Update()
        {
            float dt = Time.deltaTime;

            _input.Tick();

            _controller.Tick(dt);
        }

        private void OnGameOver()
        {
            if (_debugLifecycleLogs)
            {
                Debug.Log("GAME OVER");
            }
        }

        public void Init(HeroModel_V2 model, HeroView_V2 view)
        {
            // This method can be called externally to re-initialize the Hero (e.g. on respawn)
            _model = model;
            _view = view;
        }

        private void BindComponents()
        {
            _input = GetComponent<HeroInput_V2>();
            _spineEventForwarder = GetComponent<HeroSpineEventForwarder_V2>();
        }

        private void CreateSystems()
        {
            _stateMachine = new HeroStateMachine_V2();
            _movementSystem = new HeroMovementSystem_V2(_model);
            _weaponSystem = new HeroWeaponSystem_V2(_model, _initialWeapons, _startingWeapon);
            _damageReceiver = new HeroDamageReceiver_V2(_model);
            _deathHandler = new HeroDeathHandler_V2(_model, _stateMachine, _movementSystem, _weaponSystem);

            _controller = new HeroController_V2(
                _model,
                _view,
                _input,
                _stateMachine,
                _movementSystem,
                _weaponSystem
            );
        }

        private void InitSystems()
        {
            _damageReceiver.Init(_model, _stateMachine, _deathHandler);
            _deathHandler.Init(_model, _stateMachine, _view);

            _view.Init(_model, _stateMachine, _damageReceiver, _deathHandler, _skeletonAnimation);

            if (_spineEventForwarder != null)
            {
                _spineEventForwarder.Init(_controller, _skeletonAnimation);
            }
            else
            {
                Debug.LogWarning($"{nameof(HeroSpineEventForwarder_V2)} missing on Hero_V2 object.");
            }

            if (_debugLifecycleLogs)
            {
                Debug.Log("HERE2 SYSTEMS INITIALIZED");
            }
        }

        public void ReceiveDamage(int damage)
        {
            if (_debugDamageLogs)
            {
                Debug.Log($"[Hero_V2] ReceiveDamage called. damage={damage}");
            }
            if (_damageReceiver != null)
            {
                _damageReceiver.ApplyDamage(damage);
                return;
            }

            if (_model != null)
            {
                _model.TakeDamage(damage);
            }
        }

        public bool UnlockWeapon(HeroWeaponDefinition_V2 definition, bool autoEquip)
        {
            if (_weaponSystem == null || definition == null)
            {
                return false;
            }

            return _weaponSystem.UnlockWeapon(definition, autoEquip);
        }

        public bool HasWeaponUnlocked(HeroWeaponDefinition_V2 definition)
        {
            return _weaponSystem != null && definition != null && _weaponSystem.HasWeaponUnlocked(definition);
        }

        public bool TryRefillWeaponMagazine(HeroWeaponDefinition_V2 definition)
        {
            return _weaponSystem != null && _weaponSystem.TryRefillMagazineForWeapon(definition);
        }

        public bool IsWeaponMagazineFull(HeroWeaponDefinition_V2 definition)
        {
            return _weaponSystem == null || definition == null || _weaponSystem.IsMagazineFullForWeapon(definition);
        }

        public bool IsHealthFull()
        {
            return _model != null && _model.currentHealth >= _model.maxHealth;
        }

        public void Heal(int amount)
        {
            if (_model == null || amount <= 0)
            {
                return;
            }

            _model.Heal(amount);
        }

        public bool IsDead()
        {
            return _model != null && _model.isDead;
        }
    }
}
