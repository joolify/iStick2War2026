using Assets.Scripts.Components;
using iStick2War;
using System;
using System.Collections.Generic;
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
    [Header("EMP / combat stun")]
    [SerializeField] private bool _enableTeslaEmpCombatStun = true;
    [SerializeField] private float _teslaEmpCombatStunSeconds = 0.35f;

    [Header("Explosive Death")]
    [SerializeField] private bool _enableExplosiveGibDeath = true;
    [SerializeField] private bool _explodeOnAnyExplosiveHit = true;
    [SerializeField] private float _explosiveGibDamageThreshold = 40f;
    [Header("Flamethrower burn death")]
    [SerializeField] private bool _enableFlamethrowerBurnDeath = true;
    [SerializeField] private float _flamethrowerBurnDeathDelaySeconds = 1.8f;
    [SerializeField] private float _flamethrowerAirborneBurnDeathDelaySeconds = 1.3f;
    [Header("Body part severing")]
    [SerializeField] private bool _enableBodyPartSevering = true;
    [SerializeField] private float _minFinalDamageToSever = 18f;
    [SerializeField] private bool _allowTorsoSever = false;
    [SerializeField] private bool _allowHeadSever = true;
    [SerializeField] private bool _debugDamagePathLogs = true;

    private ParatrooperModel_V2 _model;
    private ParatrooperStateMachine_V2 _stateMachine;
    private Paratrooper _paratrooper;
    private bool _deathStateSent;
    private readonly HashSet<BodyPartType> _severedParts = new HashSet<BodyPartType>();
    public event System.Action<Vector2, float> OnExploded;
    public event System.Action<BodyPartType, Vector2, float> OnBodyPartSevered;
    /// <summary>Raised after HP was reduced this frame; for VFX only (final damage amount after armor/multipliers).</summary>
    public event Action<DamageInfo, float> OnDamagePresentation;

    private void OnEnable()
    {
        _deathStateSent = false;
        _severedParts.Clear();
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

        if (info.SourceWeapon == WeaponType.Tesla)
        {
            _model.lastUnscaledTimeReceivedHeroTeslaHit = Time.unscaledTime;
            if (_enableTeslaEmpCombatStun)
            {
                ParatrooperWeaponSystem_V2 ws =
                    _paratrooper != null
                        ? _paratrooper.GetComponentInChildren<ParatrooperWeaponSystem_V2>(true)
                        : GetComponentInChildren<ParatrooperWeaponSystem_V2>(true);
                if (ws != null)
                {
                    ws.ApplyCombatStun(_teslaEmpCombatStunSeconds, "tesla_emp");
                    WaveRunTelemetry_V2.NotifyEmpCombatStunApplied();
                }
            }
        }

        bool isFlamethrowerHit = info.SourceWeapon == WeaponType.Flamethrower;
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
        bool severedPart = TrySeverBodyPart(info, finalDamage, isDead);

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

        bool teslaKillShowElectrocuteFirst =
            isDead &&
            !shouldExplode &&
            info.SourceWeapon == WeaponType.Tesla &&
            !_deathStateSent;

        if (teslaKillShowElectrocuteFirst)
        {
            LogDamagePath(
                "tesla_lethal_electrocute",
                info,
                finalDamage,
                isDead,
                shouldExplode,
                severedPart);
            _deathStateSent = true;
            _model.pendingDieAfterElectrocuteAnim = true;
            _model.hasResumeStateAfterTeslaElectrocute = false;
            StickmanBodyState before = _stateMachine.CurrentState;
            // Include GlideElectrocuted: a prior non-lethal Tesla tick can already be in this state when the killing hit lands.
            // Routing that kill to Electrocuted (ground) snaps physics and never advances to GlideDie.
            bool isAirbornePara =
                before == StickmanBodyState.Glide ||
                before == StickmanBodyState.Deploy ||
                before == StickmanBodyState.GlideElectrocuted;
            StickmanBodyState target =
                isAirbornePara ? StickmanBodyState.GlideElectrocuted : StickmanBodyState.Electrocuted;
            _stateMachine.ChangeState(target);
            // Already electrocuted (non-lethal Tesla): ChangeState is a no-op — replay clip so death sequencing runs.
            if (before == _stateMachine.CurrentState && before == target && _paratrooper != null)
            {
                ParatrooperView_V2 view = _paratrooper.GetComponentInChildren<ParatrooperView_V2>(true);
                view?.PlayAnimation(target);
            }
        }
        else if (isDead || shouldExplode)
        {
            LogDamagePath(
                shouldExplode ? "explosive_death" : "lethal_death",
                info,
                finalDamage,
                isDead,
                shouldExplode,
                severedPart);
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
            LogDamagePath(
                severedPart ? "body_part_sever" : "normal_damage",
                info,
                finalDamage,
                isDead,
                shouldExplode,
                severedPart);
            // Keep air states when shot in the air so parachute glide movement is not interrupted.
            // Also keep combat states while taking damage on the ground so we do not
            // break Grenade -> Shoot flow by forcing a Land restart.
            var currentState = _stateMachine.CurrentState;
            bool isAirborne =
                currentState == StickmanBodyState.Glide ||
                currentState == StickmanBodyState.Deploy ||
                currentState == StickmanBodyState.GlideElectrocuted;
            bool isGroundCombat = currentState == StickmanBodyState.Shoot || currentState == StickmanBodyState.Grenade;

            if (info.SourceWeapon == WeaponType.Tesla)
            {
                StickmanBodyState cur = _stateMachine.CurrentState;
                if (cur != StickmanBodyState.GlideElectrocuted && cur != StickmanBodyState.Electrocuted)
                {
                    _model.resumeStateAfterTeslaElectrocute = cur;
                    _model.hasResumeStateAfterTeslaElectrocute = true;
                }

                if (isAirborne)
                {
                    _stateMachine.ChangeState(StickmanBodyState.GlideElectrocuted);
                }
                else
                {
                    _stateMachine.ChangeState(StickmanBodyState.Electrocuted);
                }
            }
            else if (!isAirborne && !isGroundCombat)
            {
                _stateMachine.ChangeState(StickmanBodyState.Land);
            }
        }

        float totalDealt = hpBefore - _model.health;
        if (totalDealt > 0.001f)
        {
            OnDamagePresentation?.Invoke(info, totalDealt);
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

    /// <summary>
    /// Called by presentation after lethal Tesla electrocute clip (or timeout). Enters normal death state.
    /// </summary>
    public void CompletePendingElectrocuteDeath()
    {
        if (_model == null || _stateMachine == null || !_model.pendingDieAfterElectrocuteAnim)
        {
            return;
        }

        _model.pendingDieAfterElectrocuteAnim = false;
        bool useAirborneDeath = _paratrooper != null && _paratrooper.ShouldUseAirborneDeathFlow();
        _stateMachine.ChangeState(useAirborneDeath ? StickmanBodyState.GlideDie : StickmanBodyState.Die);
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

    private bool TrySeverBodyPart(DamageInfo info, float finalDamage, bool isDead)
    {
        // Allow severing even on lethal hits.
        // Otherwise, high-damage hits (commonly headshots) kill the enemy before severing can trigger.
        if (!_enableBodyPartSevering)
        {
            return false;
        }

        if (info.IsExplosive || info.SourceWeapon == WeaponType.Tesla || info.SourceWeapon == WeaponType.Flamethrower)
        {
            return false;
        }

        if (finalDamage < _minFinalDamageToSever)
        {
            return false;
        }

        BodyPartType part = info.BodyPart;
        if ((!_allowHeadSever && part == BodyPartType.Head) ||
            (!_allowTorsoSever && part == BodyPartType.Torso))
        {
            return false;
        }

        if (_severedParts.Contains(part))
        {
            return false;
        }

        _severedParts.Add(part);
        OnBodyPartSevered?.Invoke(part, info.HitPoint, Mathf.Max(0.2f, finalDamage / 22f));
        return true;
    }

    private void LogDamagePath(
        string pathTag,
        DamageInfo info,
        float finalDamage,
        bool isDead,
        bool shouldExplode,
        bool severedPart)
    {
        if (!_debugDamagePathLogs)
        {
            return;
        }

        Debug.Log(
            $"[ParatrooperDamageReceiver_V2] DamagePath={pathTag}, weapon={info.SourceWeapon}, part={info.BodyPart}, " +
            $"finalDamage={finalDamage:0.##}, explosiveHit={info.IsExplosive}, shouldExplode={shouldExplode}, " +
            $"severed={severedPart}, hp={_model.health:0.##}, dead={isDead}");
    }
}
}
