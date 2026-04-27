using Assets.Scripts.Components;
using iStick2War;
using UnityEngine;

/// <summary>
/// ParatrooperDamageReceiver (Damage Processing Layer)
/// </summary>
/// <remarks>
/// The ParatrooperDamageReceiver is responsible for handling all incoming damage
/// to the Paratrooper entity. It acts as the single entry point for damage processing,
/// ensuring consistent and centralized damage logic.
///
/// Damage Flow:
/// BodyPart → DamageReceiver → Model → StateMachine
///
/// Responsibilities:
/// - Processes all incoming damage requests
/// - Applies body part-specific damage multipliers (e.g., headshots)
/// - Applies armor and global damage modifiers
/// - Updates the Model (health, state-related data)
/// - Triggers state changes (e.g., HitReaction, Dead) via StateMachine
///
/// Constraints:
/// - MUST be the only system allowed to modify health
/// - MUST NOT contain AI decision logic (handled by Controller)
/// - MUST NOT handle animations or visual effects (handled by View)
/// - MUST remain deterministic and consistent for all damage sources

/// Separation of concerns (AAA-level thinking)
/// System           Responsibility
/// BodyPart         detects hit
/// DamageReceiver   calculates damage
/// Model            stores result
/// StateMachine     reacts (Dead / HitReaction)
/// View             shows it
/// </remarks>
namespace iStick2War_V2
{
public class ParatrooperDamageReceiver_V2 : MonoBehaviour
{
    [Header("Explosive Death")]
    [SerializeField] private bool _enableExplosiveGibDeath = true;
    [SerializeField] private bool _explodeOnAnyExplosiveHit = true;
    [SerializeField] private float _explosiveGibDamageThreshold = 40f;
    [Header("Flamethrower burn death")]
    [SerializeField] private bool _enableFlamethrowerBurnDeath = true;
    [SerializeField] private float _flamethrowerBurnDeathDelaySeconds = 1.8f;
    [SerializeField] private float _flamethrowerAirborneBurnDeathDelaySeconds = 1.3f;

    private ParatrooperModel_V2 _model;
    private ParatrooperStateMachine_V2 _stateMachine;
    private Paratrooper _paratrooper;
    private bool _deathStateSent;
    public event System.Action<Vector2, float> OnExploded;

    private void OnEnable()
    {
        _deathStateSent = false;
    }

    void Awake()
    {
        _model = GetComponentInParent<ParatrooperModel_V2>();
        _stateMachine = GetComponentInParent<ParatrooperStateMachine_V2>();
        _paratrooper = GetComponentInParent<Paratrooper>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        TickBurnDeath();
    }

    /// <summary>
    /// Applies damage to the Paratrooper based on the provided DamageInfo.
    /// </summary>
    public void TakeDamage(DamageInfo info)
    {
        if (_model == null || _stateMachine == null)
        {
            Debug.LogWarning("[ParatrooperDamageReceiver_V2] TakeDamage skipped: missing model or state machine.");
            return;
        }

        if (_model.IsDead())
        {
            return;
        }

        bool isFlamethrowerHit = info.WeaponType == WeaponType.Flamethrower;
        bool isGroundedTarget = _paratrooper == null || !_paratrooper.ShouldUseAirborneDeathFlow();
        if (_enableFlamethrowerBurnDeath &&
            isFlamethrowerHit &&
            !_deathStateSent &&
            !_model.isBurning)
        {
            bool airborneBurn = !isGroundedTarget;
            float delay = airborneBurn
                ? _flamethrowerAirborneBurnDeathDelaySeconds
                : _flamethrowerBurnDeathDelaySeconds;
            _model.StartBurning(delay, airborneBurn);
            _stateMachine.ChangeState(airborneBurn ? StickmanBodyState.Glide : StickmanBodyState.Run);
            Debug.Log($"[ParatrooperDamageReceiver_V2] Burn started by flamethrower. dieAt={_model.burnDieAtTime:0.##}");
            return;
        }

        if (_model.isBurning && isFlamethrowerHit)
        {
            return;
        }

        float hpBefore = _model.health;

        float multiplier = _model.GetMultiplier(info.BodyPart);

        float finalDamage = info.BaseDamage * multiplier * _model.armorMultiplier;
        bool isExplosiveHit = _enableExplosiveGibDeath && info.IsExplosive;
        bool passesExplosiveThreshold = finalDamage >= _explosiveGibDamageThreshold;
        bool shouldExplode = isExplosiveHit && (_explodeOnAnyExplosiveHit || passesExplosiveThreshold);

        float remainingHealth = _model.ApplyDamage(finalDamage);
        bool isDead = _model.IsDead();

        if (shouldExplode && !isDead)
        {
            // Explosive gib kill should always finish the unit immediately.
            remainingHealth = _model.ApplyDamage(_model.health + 1f);
            isDead = _model.IsDead();
        }

        // Safety: if explosive hit already killed the target, still trigger gib path
        // even when old serialized settings had _explodeOnAnyExplosiveHit=false.
        if (isExplosiveHit && isDead)
        {
            shouldExplode = true;
        }

        if (isDead || shouldExplode)
        {
            if (!_deathStateSent)
            {
                _deathStateSent = true;
                if (shouldExplode)
                {
                    float force = Mathf.Max(2f, info.ExplosionForce);
                    OnExploded?.Invoke(info.HitPoint, force);
                    // Must match non-explosive lethal path: going Deploy/Glide → Die makes HandleStateChanged try
                    // Die → GlideDie, but the machine is already in Die and blocks that transition, so
                    // ParatrooperDeathHandler_V2.Die() never runs and wave tracking never clears.
                    bool useAirborneDeath = _paratrooper != null && _paratrooper.ShouldUseAirborneDeathFlow();
                    _stateMachine.ChangeState(
                        useAirborneDeath ? StickmanBodyState.GlideDie : StickmanBodyState.Die);
                }
                else
                {
                    bool useAirborneDeath = _paratrooper != null && _paratrooper.ShouldUseAirborneDeathFlow();
                    _stateMachine.ChangeState(useAirborneDeath ? StickmanBodyState.GlideDie : StickmanBodyState.Die);
                }
            }
        }
        else
        {
            // Keep air states when shot in the air so parachute glide movement is not interrupted.
            // Also keep combat states while taking damage on the ground so we do not
            // break Grenade -> Shoot flow by forcing a Land restart.
            var currentState = _stateMachine.CurrentState;
            bool isAirborne = currentState == StickmanBodyState.Glide || currentState == StickmanBodyState.Deploy;
            bool isGroundCombat = currentState == StickmanBodyState.Shoot || currentState == StickmanBodyState.Grenade;
            if (!isAirborne && !isGroundCombat)
            {
                _stateMachine.ChangeState(StickmanBodyState.Land);
            }
        }

        Debug.Log(
            $"[ParatrooperDamageReceiver_V2] Hit part={info.BodyPart}, base={info.BaseDamage:0.##}, " +
            $"mult={multiplier:0.##}, armor={_model.armorMultiplier:0.##}, final={finalDamage:0.##}, " +
            $"hp={hpBefore:0.##}->{remainingHealth:0.##}, dead={isDead}, profile={_model.GetDamageProfileMode()}, hitPoint={info.HitPoint}"
        );

        if (isDead)
        {
            Debug.LogWarning($"[ParatrooperDamageReceiver_V2] LETHAL HIT on {info.BodyPart}.");
            return;
        }

        if (info.BodyPart == BodyPartType.Head)
        {
            Debug.LogWarning("[ParatrooperDamageReceiver_V2] HEADSHOT!");
            return;
        }

        if (multiplier <= 0.8f)
        {
            Debug.Log($"[ParatrooperDamageReceiver_V2] Low-damage limb hit ({info.BodyPart}).");
        }
    }

    private void TickBurnDeath()
    {
        if (_model == null || _stateMachine == null || _deathStateSent)
        {
            return;
        }

        if (!_model.isBurning)
        {
            return;
        }

        if (Time.time < _model.burnDieAtTime)
        {
            return;
        }

        bool airborneBurnDeath = _model.burnFromAirborneFlamethrower;
        _model.StopBurning();
        _model.ApplyDamage(_model.health + 1f);
        if (_model.IsDead())
        {
            _deathStateSent = true;
            _stateMachine.ChangeState(
                airborneBurnDeath ? StickmanBodyState.GlideDie : StickmanBodyState.Die);
        }
    }

    private float GetBodyPartMultiplier(BodyPartType bodyPart)
    {
        if (_model.damageMultipliers.TryGetValue(bodyPart, out var value))
            return value;

        return 1f;
    }
}
}
