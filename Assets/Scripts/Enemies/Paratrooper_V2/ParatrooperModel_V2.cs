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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
