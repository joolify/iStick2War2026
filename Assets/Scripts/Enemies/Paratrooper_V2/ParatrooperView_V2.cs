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
    private SkeletonAnimation _skeletonAnimation;
    private ParticleSystem hitEffect;

    //Add animation mapping (cleaner than switch)
    private Dictionary<StickmanBodyState, string> animationMap;

    private ParatrooperStateMachine_V2 _stateMachine;

    [Header("Animations")]
    public AnimationReferenceAsset _deployAnim;
    public AnimationReferenceAsset _glideAnim;
    public AnimationReferenceAsset _glideDeathAnim;
    public AnimationReferenceAsset _landFallDownBackAnim;

    public void Initialize(ParatrooperStateMachine_V2 stateMachine)
    {
        _skeletonAnimation = GetComponent<SkeletonAnimation>();

        _stateMachine = stateMachine;

        _stateMachine.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (_stateMachine != null)
            _stateMachine.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(StickmanBodyState from, StickmanBodyState to)
    {
        PlayAnimation(to);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CheckAnimationNames();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void CheckAnimationNames()
    {
        if (!_deployAnim.name.Equals("E_deploy")) Debug.LogError(nameof(_deployAnim) + " has wrong animation");
        if (!_glideAnim.name.Equals("E_glide")) Debug.LogError(nameof(_glideAnim) + " has wrong animation");
        if (!_glideDeathAnim.name.Equals("E_glide_death")) Debug.LogError(nameof(_glideDeathAnim) + " has wrong animation");
        if (!_landFallDownBackAnim.name.Equals("E_glide_death")) Debug.LogError(nameof(_landFallDownBackAnim) + " has wrong animation");
    }

    /// <summary>
    /// Plays the appropriate animation for the given state.
    /// </summary>
    public void PlayAnimation(StickmanBodyState state)
    {
        Debug.Log("PlayAnimation() - State: " + state);
        Spine.Animation nextAnimation = null;
        int trackIndex = 0;
        bool loop = false;

        // Map state → animation

        switch (state)
        {
            case StickmanBodyState.Deploy:
                nextAnimation = _deployAnim;
                loop = false;
                trackIndex = 1;
                break;
            case StickmanBodyState.Glide:
                nextAnimation = _glideAnim;
                loop = true;
                trackIndex = 1;
                break;
            case StickmanBodyState.Die:
                nextAnimation = _landFallDownBackAnim;
                loop = false;
                trackIndex = 1;
                break;
            case StickmanBodyState.GlideDie:
                nextAnimation = _glideDeathAnim;
                loop = false;
                trackIndex = 1;
                break;
            case StickmanBodyState.Land:
            case StickmanBodyState.Idle:
            case StickmanBodyState.Shoot:
            case StickmanBodyState.Run:
                // Temporary fallback while dedicated clips for these states are migrated.
                nextAnimation = _glideAnim;
                loop = true;
                trackIndex = 1;
                break;
        }

        if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
        {
            Debug.LogWarning("[ParatrooperView_V2] PlayAnimation skipped: SkeletonAnimation is not ready.");
            return;
        }

        if (nextAnimation == null)
        {
            Debug.LogWarning($"[ParatrooperView_V2] PlayAnimation skipped: no animation mapped for state {state}.");
            return;
        }

        _skeletonAnimation.AnimationState.SetAnimation(trackIndex, nextAnimation, loop);
    }

    /// <summary>
    /// Plays hit reaction visuals for a specific body part.
    /// </summary>
    public void PlayHitReaction(BodyPartType part)
    {
        // Trigger particle effects / animation overlays
    }

    private void PlayDeploy_Complete(Spine.TrackEntry trackEntry)
    {
        Debug.Log("PlayDeploy_Complete()");
        PlayGlide();
    }

    public void PlayGlide()
    {
        Debug.Log("PlayGlide()");
        // Play the shoot animation on track 1.
        var track = _skeletonAnimation.AnimationState.SetAnimation(1, _glideAnim, true);
        //track.AttachmentThreshold = 1f;
        track.MixDuration = 0f;

        //TODO Add sound
    }

}
