using Assets.Scripts.Components;
using iStick2War;
using UnityEditor.Experimental.GraphView;
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
public class ParatrooperDamageReceiver_V2 : MonoBehaviour
{
    private ParatrooperModel_V2 _model;
    private ParatrooperStateMachine_V2 _stateMachine;

    void Awake()
    {
        _model = GetComponentInParent<ParatrooperModel_V2>();
        _stateMachine = GetComponentInParent<ParatrooperStateMachine_V2>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Applies damage to the Paratrooper based on the provided DamageInfo.
    /// </summary>
    public void TakeDamage(DamageInfo info)
    {
        Debug.Log($"Hit {info.BodyPart} for {info.BaseDamage}");

        float multiplier = _model.GetMultiplier(info.BodyPart);

        float finalDamage = info.BaseDamage * multiplier * _model.armorMultiplier;

        float remainingHealth = _model.ApplyDamage(finalDamage);

        if (_model.IsDead())
        {
            _stateMachine.ChangeState(StickmanBodyState.Die);
        }
        else
        {
            _stateMachine.ChangeState(StickmanBodyState.Land);
        }

        Debug.Log($"Final damage: {finalDamage}, HP left: {remainingHealth}");
    }

    private float GetBodyPartMultiplier(BodyPartType bodyPart)
    {
        if (_model.damageMultipliers.TryGetValue(bodyPart, out var value))
            return value;

        return 1f;
    }
}
