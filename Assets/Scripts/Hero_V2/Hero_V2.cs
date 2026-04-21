using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
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

        [Header("Physics")]
        [Tooltip(
            "Bunker cover often uses solid Collider2D while shots use the same geometry. The hero Rigidbody2D would " +
            "otherwise rest on those colliders even when Ground raycasts miss. Adds Bunker to Rigidbody2D.excludeLayers " +
            "(Unity 6). BunkerHitbox / ray weapons are unaffected.")]
        [SerializeField] private bool _excludeBunkerLayerFromHeroRigidbodyContacts = true;

        [Header("Core References")]
        [SerializeField] private HeroModel_V2 _model;
        [SerializeField] private HeroView_V2 _view;
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [Header("Weapons")]
        [SerializeField] private List<HeroWeaponDefinition_V2> _initialWeapons = new List<HeroWeaponDefinition_V2>();
        [SerializeField] private WeaponType _startingWeapon = WeaponType.Colt45;

        private HeroInput_V2 _input;
        private HeroController_V2 _controller;
        private HeroStateMachine_V2 _stateMachine;
        private HeroSpineEventForwarder_V2 _spineEventForwarder;

        private HeroMovementSystem_V2 _movementSystem;
        private HeroWeaponSystem_V2 _weaponSystem;

        private HeroDamageReceiver_V2 _damageReceiver;
        private HeroDeathHandler_V2 _deathHandler;
        private WaveManager_V2 _cachedWaveManager;
        private int _heroRigidbodyBunkerExcludeBits;
        private AutoHero_V2 _autoHero;

        private void Awake()
        {
            if (_debugLifecycleLogs)
            {
                Debug.Log("HERE AWAKE");
            }
            BindComponents();
            ApplyHeroRigidbodyBunkerContactExclusion();
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

        private void OnDestroy()
        {
            ClearHeroRigidbodyBunkerContactExclusion();
        }

        private void ApplyHeroRigidbodyBunkerContactExclusion()
        {
            if (!_excludeBunkerLayerFromHeroRigidbodyContacts || _model == null)
            {
                return;
            }

            Rigidbody2D rb = _model.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                return;
            }

            int bunker = LayerMask.NameToLayer("Bunker");
            if (bunker < 0)
            {
                return;
            }

            _heroRigidbodyBunkerExcludeBits = 1 << bunker;
            rb.excludeLayers |= _heroRigidbodyBunkerExcludeBits;
        }

        private void ClearHeroRigidbodyBunkerContactExclusion()
        {
            if (_heroRigidbodyBunkerExcludeBits == 0 || _model == null)
            {
                return;
            }

            Rigidbody2D rb = _model.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.excludeLayers &= ~_heroRigidbodyBunkerExcludeBits;
            }

            _heroRigidbodyBunkerExcludeBits = 0;
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

            if (_autoHero != null && _autoHero.isActiveAndEnabled)
            {
                _autoHero.TickBeforeHeroFrame(dt);
            }

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
            _autoHero = GetComponent<AutoHero_V2>();
        }

        private void CreateSystems()
        {
            _stateMachine = new HeroStateMachine_V2();
            _movementSystem = new HeroMovementSystem_V2(_model);
            _weaponSystem = new HeroWeaponSystem_V2(_model, BuildStartupLoadout(), _startingWeapon);
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

        private IEnumerable<HeroWeaponDefinition_V2> BuildStartupLoadout()
        {
            if (_initialWeapons == null || _initialWeapons.Count == 0)
            {
                return _initialWeapons;
            }

            List<HeroWeaponDefinition_V2> matchingWeapons = _initialWeapons
                .Where(definition => definition != null && definition.WeaponType == _startingWeapon)
                .ToList();

            if (matchingWeapons.Count > 0)
            {
                return matchingWeapons;
            }

            Debug.LogWarning(
                $"[Hero_V2] No weapon definition in _initialWeapons matched starting weapon '{_startingWeapon}'. " +
                "Falling back to the full configured loadout.");
            return _initialWeapons;
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

        /// <param name="damage">Positive HP loss.</param>
        /// <param name="ignoreBunkerSafeZone">
        /// When true, bunker cover does not block damage (e.g. bomb splash / heavy ordnance). Small-arms paths keep false.
        /// </param>
        public void ReceiveDamage(int damage, bool ignoreBunkerSafeZone = false)
        {
            if (damage > 0 && !ignoreBunkerSafeZone)
            {
                if (_cachedWaveManager == null)
                {
                    _cachedWaveManager = FindAnyObjectByType<WaveManager_V2>();
                }

                // Cover only exists while bunker has HP; after breach, small-arms logic must not block (e.g. bomb splash).
                if (_cachedWaveManager != null &&
                    _cachedWaveManager.BunkerHealth > 0 &&
                    _cachedWaveManager.IsHeroInsideBunker(this))
                {
                    return;
                }
            }

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

        /// <summary>Raised after successful healing (shop or other systems).</summary>
        public event Action<int> OnHealed;

        public void Heal(int amount)
        {
            if (_model == null || amount <= 0)
            {
                return;
            }

            _model.Heal(amount);
            OnHealed?.Invoke(amount);
        }

        /// <summary>For telemetry / tools; non-null after <see cref="Awake"/>.</summary>
        public HeroDamageReceiver_V2 DamageReceiver => _damageReceiver;

        /// <summary>For telemetry / tools; non-null after <see cref="Awake"/>.</summary>
        public HeroWeaponSystem_V2 WeaponSystem => _weaponSystem;

        public WeaponType CurrentWeaponType => _model != null ? _model.currentWeaponType : WeaponType.Colt45;

        public bool IsDead()
        {
            return _model != null && _model.isDead;
        }

        public int GetCurrentHealth()
        {
            return _model != null ? _model.currentHealth : 0;
        }

        public int GetMaxHealth()
        {
            return _model != null ? _model.maxHealth : 0;
        }

        public string GetCurrentWeaponDisplayName()
        {
            if (_model == null)
            {
                return "None";
            }

            if (_model.currentWeaponDefinition != null && !string.IsNullOrWhiteSpace(_model.currentWeaponDefinition.DisplayName))
            {
                return _model.currentWeaponDefinition.DisplayName;
            }

            return _model.currentWeaponType.ToString();
        }

        public int GetCurrentWeaponAmmo()
        {
            return _model != null ? _model.currentAmmo : 0;
        }

        public int GetCurrentWeaponMaxAmmo()
        {
            return _model != null ? _model.maxAmmo : 0;
        }

        public int GetCurrentWeaponReserveAmmo()
        {
            return _model != null ? _model.currentReserveAmmo : 0;
        }

        public int GetCurrentWeaponMaxReserveAmmo()
        {
            return _model != null ? _model.maxReserveAmmo : 0;
        }

        public bool ShouldShowReloadPrompt()
        {
            if (_model == null || _model.isDead)
            {
                return false;
            }

            // Show prompt when magazine is empty but there is reserve ammo to reload from.
            return _model.currentAmmo <= 0 && _model.currentReserveAmmo > 0;
        }

        public bool TrySwitchToWeaponType(WeaponType weaponType)
        {
            return _weaponSystem != null && _weaponSystem.TrySwitchToWeaponType(weaponType);
        }

        public bool HasUnlockedWeaponOfType(WeaponType weaponType)
        {
            return _weaponSystem != null && _weaponSystem.HasUnlockedWeaponOfType(weaponType);
        }

        public bool HasUsableAmmoForWeaponType(WeaponType weaponType)
        {
            return _weaponSystem != null && _weaponSystem.HasUsableAmmoForWeaponType(weaponType);
        }

        public bool TrySwitchToAnyWeaponWithAmmo()
        {
            return _weaponSystem != null && _weaponSystem.TrySwitchToAnyWeaponWithAmmo();
        }

        /// <summary>Used by <see cref="GameplaySceneProfileApplier_V2"/> (e.g. Colt-only runs) after hero systems exist.</summary>
        public void ApplySceneWeaponAllowlist(IReadOnlyList<WeaponType> allowedWeaponTypes)
        {
            if (_weaponSystem == null || allowedWeaponTypes == null || allowedWeaponTypes.Count == 0)
            {
                return;
            }

            _weaponSystem.RestrictInventoryToAllowedWeaponTypes(allowedWeaponTypes);
        }
    }
}
