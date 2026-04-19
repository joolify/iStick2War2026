using System.Collections.Generic;
using iStick2War;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Minimal autonomous player for balance and integration-style runs: aim/shoot, bunker retreat,
    /// shop purchases, and starting the next wave. Add to the same GameObject as <see cref="Hero_V2"/> and enable it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoHero_V2 : MonoBehaviour
    {
        [Header("References (optional — resolved at runtime if empty)")]
        [SerializeField] private Hero_V2 _hero;
        [SerializeField] private HeroModel_V2 _model;
        [SerializeField] private HeroView_V2 _view;
        [SerializeField] private HeroInput_V2 _input;
        [SerializeField] private WaveManager_V2 _waveManager;

        [Header("Combat")]
        [SerializeField] private float _enemySearchRadius = 90f;
        [SerializeField] private float _shootIfEnemyWithinWeaponRangeFactor = 1f;
        [SerializeField] private float _bazookaPreferWithinHorizontal = 42f;
        [Tooltip(
            "When true, any Paratrooper body hitbox in the search radius wins over the helicopter. " +
            "Otherwise the single nearest EnemyBodyPart collider is used (aircraft often steals the aim).")]
        [SerializeField] private bool _prioritizeInfantryOverAircraft = true;

        [Header("Bunker")]
        [SerializeField] [Range(0.05f, 0.95f)] private float _lowHealthEnterBunkerFraction = 0.35f;

        [Header("Shop")]
        [SerializeField] private int _maxShopPurchasesPerShopVisit = 24;

        [Header("Telemetry (optional)")]
        [SerializeField] private bool _logShopAndWave;

        private readonly Collider2D[] _overlapBuffer = new Collider2D[64];
        private int _enemyBodyPartLayer = -1;
        private WaveLoopState_V2 _lastWaveState = WaveLoopState_V2.Preparing;
        private int _shopPhasesCompleted;
        private bool _shopExitScheduledThisVisit;

        private void Awake()
        {
            CacheReferences();
            _enemyBodyPartLayer = LayerMask.NameToLayer("EnemyBodyPart");
        }

        private void OnDisable()
        {
            _shopExitScheduledThisVisit = false;
            if (_input != null)
            {
                _input.SetBotDriving(false);
            }

            if (_view != null)
            {
                _view.SetAutoAimWorldOverride(null);
            }
        }

        private void CacheReferences()
        {
            if (_hero == null)
            {
                _hero = GetComponent<Hero_V2>();
            }

            if (_model == null)
            {
                _model = GetComponent<HeroModel_V2>();
            }

            if (_view == null)
            {
                _view = GetComponent<HeroView_V2>();
            }

            if (_input == null)
            {
                _input = GetComponent<HeroInput_V2>();
            }
        }

        private void CacheWaveManagerIfNeeded()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>();
            }
        }

        /// <summary>Called from <see cref="Hero_V2.Update"/> before input and controller tick.</summary>
        public void TickBeforeHeroFrame(float deltaTime)
        {
            CacheReferences();
            CacheWaveManagerIfNeeded();

            if (_hero == null || _model == null || _view == null || _input == null)
            {
                return;
            }

            if (!_hero.isActiveAndEnabled)
            {
                return;
            }

            _input.SetBotDriving(true);

            if (_hero.IsDead())
            {
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                return;
            }

            if (_waveManager == null)
            {
                TickCombatOnly(deltaTime);
                return;
            }

            WaveLoopState_V2 state = _waveManager.State;
            if (state == WaveLoopState_V2.GameOver)
            {
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            if (state == WaveLoopState_V2.Shop)
            {
                if (_lastWaveState != WaveLoopState_V2.Shop)
                {
                    _shopExitScheduledThisVisit = false;
                    _shopPhasesCompleted++;
                    if (_logShopAndWave)
                    {
                        Debug.Log(
                            $"[AutoHero_V2] Shop phase #{_shopPhasesCompleted} (after wave context: manager wave # = {_waveManager.CurrentWaveNumber}).");
                    }
                }

                if (!_shopExitScheduledThisVisit)
                {
                    TickShop();
                    _shopExitScheduledThisVisit = true;
                }

                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            if (state == WaveLoopState_V2.Preparing)
            {
                _shopExitScheduledThisVisit = false;
                _view.SetAutoAimWorldOverride(null);
                _input.SetBotFrame(Vector2.zero, false, false);
                _lastWaveState = state;
                return;
            }

            _lastWaveState = state;
            TickCombatOnly(deltaTime);
        }

        private void TickShop()
        {
            for (int i = 0; i < _maxShopPurchasesPerShopVisit; i++)
            {
                ShopOfferConfig_V2 best = PickBestAffordableOffer(_waveManager);
                if (best == null)
                {
                    break;
                }

                if (!_waveManager.TryPurchaseOffer(best))
                {
                    break;
                }

                if (_logShopAndWave)
                {
                    Debug.Log($"[AutoHero_V2] Purchased shop offer: {best.DisplayName} ({best.Kind}).");
                }
            }

            _waveManager.StartNextWaveFromShop();
            if (_logShopAndWave)
            {
                Debug.Log("[AutoHero_V2] StartNextWaveFromShop() after purchases.");
            }
        }

        private ShopOfferConfig_V2 PickBestAffordableOffer(WaveManager_V2 wave)
        {
            ShopPanel_V2 panel = wave.ShopPanel;
            if (panel == null)
            {
                return null;
            }

            IReadOnlyList<ShopOfferConfig_V2> offers = panel.ConfiguredShopOffers;
            if (offers == null || offers.Count == 0)
            {
                return null;
            }

            ShopOfferConfig_V2 best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < offers.Count; i++)
            {
                ShopOfferConfig_V2 o = offers[i];
                if (o == null)
                {
                    continue;
                }

                if (!IsOfferApplicable(wave, o))
                {
                    continue;
                }

                int cost = ResolveOfferSpendCost(wave, o);
                if (!wave.CanAfford(cost))
                {
                    continue;
                }

                int score = ScoreOffer(wave, o);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = o;
                }
            }

            return best;
        }

        private static int ResolveOfferSpendCost(WaveManager_V2 wave, ShopOfferConfig_V2 o)
        {
            return o.Kind is ShopOfferKind_V2.HealthPack or ShopOfferKind_V2.BunkerMaxUpgrade
                ? wave.GetOfferEffectiveCost(o)
                : Mathf.Max(0, o.Cost);
        }

        private bool IsOfferApplicable(WaveManager_V2 wave, ShopOfferConfig_V2 o)
        {
            switch (o.Kind)
            {
                case ShopOfferKind_V2.HealthPack:
                    return !_hero.IsHealthFull();
                case ShopOfferKind_V2.BunkerRepair:
                    return !wave.IsBunkerFullHealth();
                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    return !wave.IsBunkerMaxAtCap();
                case ShopOfferKind_V2.WeaponUnlock:
                    return o.Weapon != null && !_hero.HasWeaponUnlocked(o.Weapon);
                case ShopOfferKind_V2.AmmoRefill:
                    return o.Weapon != null &&
                           _hero.HasWeaponUnlocked(o.Weapon) &&
                           !_hero.IsWeaponMagazineFull(o.Weapon);
                default:
                    return false;
            }
        }

        private int ScoreOffer(WaveManager_V2 wave, ShopOfferConfig_V2 o)
        {
            float hpRatio = _model.maxHealth > 0 ? (float)_model.currentHealth / _model.maxHealth : 1f;
            float bunkerRatio = wave.BunkerMaxHealth > 0 ? (float)wave.BunkerHealth / wave.BunkerMaxHealth : 1f;

            switch (o.Kind)
            {
                case ShopOfferKind_V2.WeaponUnlock:
                    return 10_000 + Mathf.Clamp(o.Cost, 0, 5_000);
                case ShopOfferKind_V2.AmmoRefill:
                    return 4_000 + Mathf.Clamp(o.Cost, 0, 2_000);
                case ShopOfferKind_V2.HealthPack:
                {
                    int urgency = Mathf.RoundToInt((1f - Mathf.Clamp01(hpRatio)) * 6_000f);
                    return 3_000 + urgency;
                }
                case ShopOfferKind_V2.BunkerRepair:
                {
                    int urgency = Mathf.RoundToInt((1f - Mathf.Clamp01(bunkerRatio)) * 5_000f);
                    return 2_500 + urgency;
                }
                case ShopOfferKind_V2.BunkerMaxUpgrade:
                    return 2_000 + Mathf.Clamp(o.Cost, 0, 1_000);
                default:
                    return 0;
            }
        }

        private void TickCombatOnly(float _)
        {
            Vector2 heroPos = _model.transform.position;
            Vector2? bunkerAnchor = TryGetBunkerInteriorWorldPoint();

            bool inside = _waveManager != null && _waveManager.IsHeroInsideBunker(_hero);
            float hpRatio = _model.maxHealth > 0 ? (float)_model.currentHealth / _model.maxHealth : 1f;
            bool wantBunker =
                bunkerAnchor.HasValue &&
                hpRatio < _lowHealthEnterBunkerFraction &&
                !inside;

            Collider2D target = FindNearestEnemyCollider(heroPos);
            Vector2 aimPoint = heroPos + Vector2.right * 8f;
            bool hasTarget = false;

            if (target != null)
            {
                aimPoint = target.bounds.center;
                hasTarget = true;
            }

            MaybeSelectWeaponForThreat(heroPos, aimPoint, hasTarget);

            Vector2 move = Vector2.zero;
            if (wantBunker)
            {
                float dx = bunkerAnchor.Value.x - heroPos.x;
                move = new Vector2(dx > 0.08f ? 1f : dx < -0.08f ? -1f : 0f, 0f);
            }

            float weaponRange = _model.currentWeaponDefinition != null
                ? _model.currentWeaponDefinition.Range
                : 40f;
            float maxShootDist = weaponRange * Mathf.Max(0.1f, _shootIfEnemyWithinWeaponRangeFactor);
            float verticalSlack = IsParatrooperCollider(target) ? 1.35f : 0.85f;
            bool inRange =
                hasTarget &&
                Mathf.Abs(aimPoint.x - heroPos.x) <= maxShootDist &&
                Mathf.Abs(aimPoint.y - heroPos.y) <= maxShootDist * verticalSlack;

            // Do not hold fire when the active weapon is completely dry: HeroController_V2 requires a release to
            // clear _outOfAmmoLatched after a dry-fire; holding shoot forever would soft-lock shooting.
            bool canHoldFire =
                _model.currentAmmo > 0 || _model.currentReserveAmmo > 0;

            bool shootHeld = inRange && !wantBunker && canHoldFire;
            bool reload = _hero.ShouldShowReloadPrompt();

            _view.SetAutoAimWorldOverride(hasTarget ? aimPoint : heroPos + Vector2.right * 6f);
            _input.SetBotFrame(move, shootHeld, reload);
        }

        private void MaybeSelectWeaponForThreat(Vector2 heroPos, Vector2 enemyPos, bool hasTarget)
        {
            if (!hasTarget)
            {
                return;
            }

            float horiz = Mathf.Abs(enemyPos.x - heroPos.x);
            bool wantBazookaRange = horiz <= _bazookaPreferWithinHorizontal;
            bool wantCarbineRange = horiz > _bazookaPreferWithinHorizontal * 1.15f;

            bool bazookaAmmo =
                _hero.HasUnlockedWeaponOfType(WeaponType.Bazooka) &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Bazooka);
            bool carbineAmmo =
                _hero.HasUnlockedWeaponOfType(WeaponType.Carbine) &&
                _hero.HasUsableAmmoForWeaponType(WeaponType.Carbine);

            if (wantBazookaRange)
            {
                if (bazookaAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Bazooka);
                    return;
                }

                if (carbineAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Carbine);
                    return;
                }

                _hero.TrySwitchToAnyWeaponWithAmmo();
                return;
            }

            if (wantCarbineRange)
            {
                if (carbineAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Carbine);
                    return;
                }

                if (bazookaAmmo)
                {
                    _hero.TrySwitchToWeaponType(WeaponType.Bazooka);
                    return;
                }

                _hero.TrySwitchToAnyWeaponWithAmmo();
                return;
            }

            // Hysteresis band: do not bounce weapons, but escape a totally dry current weapon.
            if (!_hero.HasUsableAmmoForWeaponType(_model.currentWeaponType))
            {
                _hero.TrySwitchToAnyWeaponWithAmmo();
            }
        }

        private Collider2D FindNearestEnemyCollider(Vector2 from)
        {
            if (_enemyBodyPartLayer < 0)
            {
                return null;
            }

            ContactFilter2D filter = default;
            filter.SetLayerMask(1 << _enemyBodyPartLayer);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(from, _enemySearchRadius, filter, _overlapBuffer);
            if (count <= 0)
            {
                return null;
            }

            Collider2D bestAny = null;
            float bestAnyDist = float.MaxValue;
            Collider2D bestInfantry = null;
            float bestInfantryDist = float.MaxValue;
            Collider2D bestAircraft = null;
            float bestAircraftDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D c = _overlapBuffer[i];
                if (c == null)
                {
                    continue;
                }

                bool isParatrooper = IsParatrooperCollider(c);
                if (isParatrooper && !IsViableParatrooperTarget(c))
                {
                    continue;
                }

                Vector2 p = c.bounds.center;
                float d = (p - from).sqrMagnitude;
                if (d < bestAnyDist)
                {
                    bestAnyDist = d;
                    bestAny = c;
                }

                if (isParatrooper)
                {
                    if (d < bestInfantryDist)
                    {
                        bestInfantryDist = d;
                        bestInfantry = c;
                    }
                }
                else if (IsAircraftCollider(c))
                {
                    if (d < bestAircraftDist)
                    {
                        bestAircraftDist = d;
                        bestAircraft = c;
                    }
                }
            }

            if (_prioritizeInfantryOverAircraft && bestInfantry != null)
            {
                return bestInfantry;
            }

            if (bestInfantry != null && bestAircraft != null)
            {
                return bestInfantryDist <= bestAircraftDist ? bestInfantry : bestAircraft;
            }

            if (bestInfantry != null)
            {
                return bestInfantry;
            }

            if (bestAircraft != null)
            {
                return bestAircraft;
            }

            return bestAny;
        }

        private static bool IsParatrooperCollider(Collider2D c)
        {
            if (c == null)
            {
                return false;
            }

            return c.GetComponent<ParatrooperBodyPart_V2>() != null ||
                   c.GetComponentInParent<ParatrooperBodyPart_V2>() != null;
        }

        /// <summary>
        /// Dead paratroopers often keep EnemyBodyPart colliders during ragdoll/ground clips; overlap would otherwise
        /// keep aiming at the corpse while a new paratrooper spawns at nearly the same X.
        /// </summary>
        private static bool IsViableParatrooperTarget(Collider2D c)
        {
            ParatrooperBodyPart_V2 part =
                c.GetComponent<ParatrooperBodyPart_V2>() ?? c.GetComponentInParent<ParatrooperBodyPart_V2>();
            if (part == null)
            {
                return false;
            }

            return part.IsLivingCharacterForTargeting();
        }

        private static bool IsAircraftCollider(Collider2D c)
        {
            if (c == null)
            {
                return false;
            }

            return c.GetComponent<AircraftHealth_V2>() != null ||
                   c.GetComponentInParent<AircraftHealth_V2>() != null;
        }

        private static Vector2? TryGetBunkerInteriorWorldPoint()
        {
            BunkerInteriorZone_V2 zone =
                FindAnyObjectByType<BunkerInteriorZone_V2>(FindObjectsInactive.Include);
            Collider2D col = zone != null ? zone.GetComponent<Collider2D>() : null;
            if (col == null)
            {
                return null;
            }

            return col.bounds.center;
        }
    }
}
