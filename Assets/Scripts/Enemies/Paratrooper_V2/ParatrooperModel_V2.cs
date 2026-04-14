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
    public float health = 55f;
    public float armorMultiplier = 1f;

    public StickmanBodyState currentState;

    public Dictionary<BodyPartType, float> damageMultipliers;

    void Awake()
    {
        if (health <= 0f)
        {
            health = 55f;
        }

        if (armorMultiplier <= 0f)
        {
            armorMultiplier = 1f;
        }

        SetDamageMultipliers();
    }

    private void SetDamageMultipliers()
    {
        damageMultipliers = new Dictionary<BodyPartType, float>
    {
        { BodyPartType.Head, 5f },
        { BodyPartType.Torso, 1f },
        { BodyPartType.ArmUpperFront, 0.85f },
        { BodyPartType.ArmUpperBack, 0.85f },
        { BodyPartType.ArmLowerBack, 0.8f },
        { BodyPartType.ArmLowerFront, 0.8f },
        { BodyPartType.LegLowerBack, 0.8f },
        { BodyPartType.LegLowerFront, 0.8f },
        { BodyPartType.LegUpperBack, 0.9f },
        { BodyPartType.LegUpperFront, 0.9f },
        { BodyPartType.FootBack, 0.7f },
        { BodyPartType.FootFront, 0.7f }
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
