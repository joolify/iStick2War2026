using iStick2War;
using Spine;
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
    private static readonly string[] ParachuteKeywords = { "parach", "chute", "canopy", "glide" };
    private SkeletonAnimation _skeletonAnimation;
    private ParticleSystem hitEffect;
    private StickmanBodyState _lastStateBeforeChange;
    private bool _deathAnimationLocked;
    private bool _suppressParachuteVisuals;

    //Add animation mapping (cleaner than switch)
    private Dictionary<StickmanBodyState, string> animationMap;

    private ParatrooperStateMachine_V2 _stateMachine;
    public Bone _crossHairBone;
    public Bone _aimPointBone;

    [SpineBone(dataField: "_skeletonAnimation")] public string aimPointBoneName = "mp40-aim";
    [SpineBone(dataField: "_skeletonAnimation")] public string crossHairBoneName = "crosshair";

    [Header("Animations")]
    public AnimationReferenceAsset _deployAnim;
    public AnimationReferenceAsset _glideAnim;
    public AnimationReferenceAsset _landAnim;
    public AnimationReferenceAsset _glideDeathAnim;
    public AnimationReferenceAsset _landFallDownBackAnim;
    public AnimationReferenceAsset _landFallDownBack2Anim;
    public AnimationReferenceAsset _landFallDownBack3Anim;

    public AnimationReferenceAsset _shootingMP40Anim;

    public void Initialize(ParatrooperStateMachine_V2 stateMachine)
    {
        _skeletonAnimation = GetComponent<SkeletonAnimation>();

        _stateMachine = stateMachine;

        _stateMachine.OnStateChanged += HandleStateChanged;
        ResolveAimBones();
    }

    private void OnDestroy()
    {
        if (_stateMachine != null)
            _stateMachine.OnStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(StickmanBodyState from, StickmanBodyState to)
    {
        _lastStateBeforeChange = from;
        PlayAnimation(to);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CheckAnimationNames();
        ResolveAimBones();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void CheckAnimationNames()
    {
        if (!_deployAnim.name.Equals("E_deploy")) Debug.LogError(nameof(_deployAnim) + " has wrong animation");
        if (!_glideAnim.name.Equals("E_glide")) Debug.LogError(nameof(_glideAnim) + " has wrong animation");
        if (!_landAnim.name.Equals("E_land")) Debug.LogError(nameof(_landAnim) + " has wrong animation");
        if (!_glideDeathAnim.name.Equals("E_glide_death")) Debug.LogError(nameof(_glideDeathAnim) + " has wrong animation");
        if (!_shootingMP40Anim.name.Equals("E_mp40_shoot")) Debug.LogError(nameof(_shootingMP40Anim) + " has wrong animation");
        if (_landFallDownBackAnim == null) Debug.LogError(nameof(_landFallDownBackAnim) + " is missing.");
        if (_landFallDownBack2Anim == null) Debug.LogError(nameof(_landFallDownBack2Anim) + " is missing.");
        if (_landFallDownBack3Anim == null) Debug.LogError(nameof(_landFallDownBack3Anim) + " is missing.");
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
        bool isDeathState = state == StickmanBodyState.Die || state == StickmanBodyState.GlideDie;
        if (!isDeathState)
        {
            _suppressParachuteVisuals = false;
        }

        if (_deathAnimationLocked && !isDeathState)
        {
            Debug.Log($"[ParatrooperView_V2] Ignored animation state {state} because death animation is locked.");
            return;
        }

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
                nextAnimation = GetRandomGroundDeathAnimation();
                loop = false;
                trackIndex = 0;
                break;
            case StickmanBodyState.GlideDie:
                // Safety for edge case: if death state came from a ground-combat state,
                // prefer one of the land-fall death animations instead of glide death.
                bool cameFromGroundCombat =
                    _lastStateBeforeChange == StickmanBodyState.Shoot ||
                    _lastStateBeforeChange == StickmanBodyState.Land ||
                    _lastStateBeforeChange == StickmanBodyState.Idle ||
                    _lastStateBeforeChange == StickmanBodyState.Run;
                nextAnimation = cameFromGroundCombat ? GetRandomGroundDeathAnimation() : _glideDeathAnim;
                loop = false;
                trackIndex = 0;
                break;
            case StickmanBodyState.Land:
                nextAnimation = _landAnim != null ? _landAnim : _glideAnim;
                loop = false;
                trackIndex = 1;
                break;
            case StickmanBodyState.Idle:
            case StickmanBodyState.Shoot:
                nextAnimation = _shootingMP40Anim != null ? _shootingMP40Anim : _glideAnim;
                loop = true;
                trackIndex = 1;
                break;
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

        if (isDeathState)
        {
            // Death must own the full pose and must not be mixed with prior layers.
            _deathAnimationLocked = true;
            _skeletonAnimation.AnimationState.ClearTracks();
            _skeletonAnimation.AnimationState.SetEmptyAnimation(1, 0f);
        }
        else
        {
            // Clear previous entry on this track to avoid stale blends.
            _skeletonAnimation.AnimationState.ClearTrack(trackIndex);
        }
        var trackEntry = _skeletonAnimation.AnimationState.SetAnimation(trackIndex, nextAnimation, loop);
        if (trackEntry != null)
        {
            trackEntry.MixDuration = 0f;
        }
        if (isDeathState)
        {
            ForceApplyAnimationFirstFrame(nextAnimation);
            if (state == StickmanBodyState.Die)
            {
                _suppressParachuteVisuals = true;
                HideParachuteVisualsForGroundDeath();
                _skeletonAnimation.LateUpdate();
            }
        }
        Debug.Log($"[ParatrooperView_V2] SetAnimation track={trackIndex}, state={state}, clip={nextAnimation.Name}, loop={loop}");
        LogActiveTracks($"after SetAnimation ({state})");
    }

    private AnimationReferenceAsset GetRandomGroundDeathAnimation()
    {
        var options = new List<AnimationReferenceAsset>(3);
        TryAddGroundDeathOption(_landFallDownBackAnim, nameof(_landFallDownBackAnim), options);
        TryAddGroundDeathOption(_landFallDownBack2Anim, nameof(_landFallDownBack2Anim), options);
        TryAddGroundDeathOption(_landFallDownBack3Anim, nameof(_landFallDownBack3Anim), options);

        if (options.Count == 0)
        {
            Debug.LogWarning("[ParatrooperView_V2] No ground death animation assigned. Falling back to glide death.");
            return _glideDeathAnim;
        }

        int randomIndex = Random.Range(0, options.Count);
        var selected = options[randomIndex];
        Debug.Log($"[ParatrooperView_V2] Selected ground death animation: {selected.name}");
        return selected;
    }

    private void TryAddGroundDeathOption(
        AnimationReferenceAsset candidate,
        string slotName,
        List<AnimationReferenceAsset> options)
    {
        if (candidate == null)
        {
            return;
        }

        bool isGlideDeathReference =
            (_glideDeathAnim != null && candidate == _glideDeathAnim) ||
            candidate.name == "E_glide_death";
        if (isGlideDeathReference)
        {
            Debug.LogWarning(
                $"[ParatrooperView_V2] {slotName} points to glide death ({candidate.name}). " +
                "It will be ignored for ground death randomization.");
            return;
        }

        options.Add(candidate);
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

    private void LogActiveTracks(string context)
    {
        if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
        {
            return;
        }

        var tracks = _skeletonAnimation.AnimationState.Tracks;
        string track0 = DescribeTrack(tracks, 0);
        string track1 = DescribeTrack(tracks, 1);
        Debug.Log($"[ParatrooperView_V2] Track dump {context}: t0={track0} | t1={track1}");
    }

    private string DescribeTrack(Spine.ExposedList<Spine.TrackEntry> tracks, int index)
    {
        if (tracks == null || index < 0 || index >= tracks.Count)
        {
            return "none";
        }

        var entry = tracks.Items[index];
        if (entry == null || entry.Animation == null)
        {
            return "none";
        }

        return $"{entry.Animation.Name}@{entry.TrackTime:0.00}s(loop={entry.Loop})";
    }

    private void ForceApplyAnimationFirstFrame(Spine.Animation animation)
    {
        if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null || animation == null)
        {
            return;
        }

        animation.Apply(
            _skeletonAnimation.Skeleton,
            0f,
            0f,
            false,
            null,
            1f,
            Spine.MixBlend.Replace,
            Spine.MixDirection.In);
        _skeletonAnimation.LateUpdate();
    }

    private void HideParachuteVisualsForGroundDeath()
    {
        if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
        {
            return;
        }

        var slots = _skeletonAnimation.Skeleton.Slots;
        if (slots == null)
        {
            return;
        }

        int hiddenCount = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots.Items[i];
            if (slot == null)
            {
                continue;
            }

            string slotName = slot.Data != null ? slot.Data.Name : string.Empty;
            string attachmentName = slot.Attachment != null ? slot.Attachment.Name : string.Empty;
            string boneName = slot.Bone != null && slot.Bone.Data != null ? slot.Bone.Data.Name : string.Empty;
            if (!ContainsParachuteKeyword(slotName) &&
                !ContainsParachuteKeyword(attachmentName) &&
                !ContainsParachuteKeyword(boneName))
            {
                continue;
            }

            if (slot.Attachment != null)
            {
                slot.Attachment = null;
            }
            // Also force slot transparent so the parachute cannot reappear if keyed by animation.
            slot.A = 0f;
            hiddenCount++;
        }

        if (hiddenCount > 0)
        {
            Debug.Log($"[ParatrooperView_V2] Ground death: cleared parachute attachments on {hiddenCount} slot(s).");
        }
    }

    private void LateUpdate()
    {
        if (!_suppressParachuteVisuals)
        {
            return;
        }

        HideParachuteVisualsForGroundDeath();
    }

    private static bool ContainsParachuteKeyword(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        string lower = value.ToLowerInvariant();
        for (int i = 0; i < ParachuteKeywords.Length; i++)
        {
            if (lower.Contains(ParachuteKeywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveAimBones()
    {
        if (_skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(aimPointBoneName))
        {
            _aimPointBone = _skeletonAnimation.Skeleton.FindBone(aimPointBoneName);
        }
        if (_aimPointBone == null)
        {
            _aimPointBone = _skeletonAnimation.Skeleton.FindBone("aimpoint");
        }

        if (!string.IsNullOrWhiteSpace(crossHairBoneName))
        {
            _crossHairBone = _skeletonAnimation.Skeleton.FindBone(crossHairBoneName);
        }
        if (_crossHairBone == null)
        {
            _crossHairBone = _skeletonAnimation.Skeleton.FindBone("crosshair");
        }
    }

    public bool TryGetAimData(out Vector2 origin, out Vector2 direction)
    {
        origin = default;
        direction = default;

        ResolveAimBones();
        if (_skeletonAnimation == null || _aimPointBone == null)
        {
            return false;
        }

        Vector2 aimPos = _skeletonAnimation.transform.TransformPoint(
            new Vector3(_aimPointBone.WorldX, _aimPointBone.WorldY, 0f));

        Vector2 dir;
        if (_crossHairBone != null)
        {
            Vector2 crossPos = _skeletonAnimation.transform.TransformPoint(
                new Vector3(_crossHairBone.WorldX, _crossHairBone.WorldY, 0f));
            dir = crossPos - aimPos;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        origin = aimPos;
        direction = dir.normalized;
        return true;
    }

}
