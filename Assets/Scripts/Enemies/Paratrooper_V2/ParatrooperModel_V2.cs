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
namespace iStick2War_V2
{
public class ParatrooperModel_V2 : MonoBehaviour
{
    public enum DamageProfileMode
    {
        CS16Mode,
        TestMode
    }

    [Header("Damage profile")]
    [SerializeField] private DamageProfileMode _damageProfileMode = DamageProfileMode.CS16Mode;

    public float health = 55f;
    public float armorMultiplier = 1f;

    /// <summary>Scales <see cref="health"/> after Awake profile setup (e.g. per-wave difficulty).</summary>
    public void ApplyWaveHealthMultiplier(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        health *= multiplier;
    }

    public StickmanBodyState currentState;

    /// <summary>
    /// When true, LandFinished (Spine event) should go to Die instead of Shoot — used after GlideDie reaches Ground
    /// and plays the land clip before ground death.
    /// </summary>
    public bool pendingDieAfterLandAnim;

    /// <summary>Skip a second ground-death clip: impact already ran on <see cref="StickmanBodyState.Land"/> after <see cref="StickmanBodyState.GlideDie"/>.</summary>
    public bool suppressDieAnimationFromAirborneImpact;

    public Dictionary<BodyPartType, float> damageMultipliers;

    void Awake()
    {
        ApplyDamageProfile();
    }

    private void OnValidate()
    {
        ApplyDamageProfile();
    }

    public DamageProfileMode GetDamageProfileMode()
    {
        return _damageProfileMode;
    }

    public void ApplyDamageProfile()
    {
        switch (_damageProfileMode)
        {
            case DamageProfileMode.TestMode:
                health = 220f;
                armorMultiplier = 0.7f;
                damageMultipliers = new Dictionary<BodyPartType, float>
                {
                    { BodyPartType.Head, 2.2f },
                    { BodyPartType.Torso, 0.8f },
                    { BodyPartType.ArmUpperFront, 0.6f },
                    { BodyPartType.ArmUpperBack, 0.6f },
                    { BodyPartType.ArmLowerBack, 0.5f },
                    { BodyPartType.ArmLowerFront, 0.5f },
                    { BodyPartType.LegLowerBack, 0.58f },
                    { BodyPartType.LegLowerFront, 0.58f },
                    { BodyPartType.LegUpperBack, 0.64f },
                    { BodyPartType.LegUpperFront, 0.64f },
                    { BodyPartType.FootBack, 0.45f },
                    { BodyPartType.FootFront, 0.45f }
                };
                break;

            case DamageProfileMode.CS16Mode:
            default:
                health = 60f;
                armorMultiplier = 1f;
                damageMultipliers = new Dictionary<BodyPartType, float>
                {
                    { BodyPartType.Head, 4.5f },
                    { BodyPartType.Torso, 1f },
                    { BodyPartType.ArmUpperFront, 0.78f },
                    { BodyPartType.ArmUpperBack, 0.78f },
                    { BodyPartType.ArmLowerBack, 0.72f },
                    { BodyPartType.ArmLowerFront, 0.72f },
                    { BodyPartType.LegLowerBack, 0.74f },
                    { BodyPartType.LegLowerFront, 0.74f },
                    { BodyPartType.LegUpperBack, 0.82f },
                    { BodyPartType.LegUpperFront, 0.82f },
                    { BodyPartType.FootBack, 0.6f },
                    { BodyPartType.FootFront, 0.6f }
                };
                break;
        }
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
}
