using iStick2War;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ParatrooperModel (Data Layer)
/// </summary>
/// <remarks>
/// The ParatrooperModel represents the pure data layer of the Paratrooper entity.
/// It stores all gameplay-relevant state but contains no behavior or logic.
/// Immutable-friendly data container for Paratrooper state.
///
/// Responsibilities:
/// - Holds entity state (health, armor, current state)
/// - Provides data used by Controller, StateMachine, and other systems
/// - Defines damage multipliers per body part
///
/// Constraints:
/// - MUST NOT contain any game logic
/// - MUST NOT implement Update() or any ticking behavior
/// - MUST NOT reference Unity-specific classes (MonoBehaviour, Transform, etc.)
/// - MUST remain a plain C# object (POCO)
/// </remarks>
public class ParatrooperModel_V2 : MonoBehaviour
{
    public float health;
    public float armorMultiplier;

    public StickmanBodyState currentState;

    public Dictionary<BodyPartType, float> damageMultipliers;

    void Awake()
    {
        SetDamageMultipliers();
    }

    private void SetDamageMultipliers()
    {
        damageMultipliers = new Dictionary<BodyPartType, float>
    {
        { BodyPartType.Head, 2f },
        { BodyPartType.Torso, 1f },
        { BodyPartType.ArmUpperFront, 0.5f },
        { BodyPartType.ArmUpperBack, 0.5f },
        { BodyPartType.ArmLowerBack, 0.5f },
        { BodyPartType.ArmLowerFront, 0.5f },
        { BodyPartType.LegLowerBack, 0.7f },
        { BodyPartType.LegLowerFront, 0.7f },
        { BodyPartType.LegUpperBack, 0.8f },
        { BodyPartType.LegUpperFront, 0.8f },
        { BodyPartType.FootBack, 0.5f },
        { BodyPartType.FootFront, 0.5f }
    };
    }

    public float ApplyDamage(float damage)
    {
        health -= damage;

        if (health < 0)
            health = 0;

        return health;
    }

    public bool IsDead()
    {
        return health <= 0;
    }

    public float GetMultiplier(BodyPartType part)
    {
        if (damageMultipliers != null &&
            damageMultipliers.TryGetValue(part, out var value))
        {
            return value;
        }

        return 1f;
    }
}
