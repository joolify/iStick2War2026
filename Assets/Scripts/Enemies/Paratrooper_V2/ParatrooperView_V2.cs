using iStick2War;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ParatrooperView (Visual & Presentation Layer)
/// </summary>
/// <remarks>
/// The ParatrooperView is responsible for all visual and audiovisual representation
/// of the Paratrooper entity. It reacts to state changes and gameplay events,
/// but does not influence gameplay logic.
///
/// Responsibilities:
/// - Plays Spine animations based on current state
/// - Triggers visual effects (e.g., hit particles)
/// - Triggers sound effects (optionally via an AudioSystem)
/// - Visually represents hit reactions per body part
///
/// Constraints:
/// - MUST NOT contain gameplay logic
/// - MUST NOT modify Model data
/// - MUST NOT make gameplay decisions
/// - MUST only react to events from Controller, StateMachine, or DamageReceiver
///
/// Notes:
/// - This layer should be fully replaceable without affecting gameplay systems
/// - Designed to support Spine animations and VFX in a modular way
/// 
/// This design works perfectly with:

/// stateMachine.OnStateChanged += view.PlayAnimation;

/// and:

/// damageReceiver.OnHit += view.PlayHitReaction;
/// 
/// </remarks>
public class ParatrooperView_V2 : MonoBehaviour
{
    private SkeletonAnimation spine;
    private ParticleSystem hitEffect;

    //Add animation mapping (cleaner than switch)
    private Dictionary<StickmanBodyState, string> animationMap;

    private ParatrooperStateMachine_V2 _stateMachine;

    public void Initialize(ParatrooperStateMachine_V2 stateMachine)
    {
        _stateMachine = stateMachine;  
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
    /// Plays the appropriate animation for the given state.
    /// </summary>
    public void PlayAnimation(StickmanBodyState state)
    {
        // Map state → animation
    }

    /// <summary>
    /// Plays hit reaction visuals for a specific body part.
    /// </summary>
    public void PlayHitReaction(BodyPartType part)
    {
        // Trigger particle effects / animation overlays
    }
}
