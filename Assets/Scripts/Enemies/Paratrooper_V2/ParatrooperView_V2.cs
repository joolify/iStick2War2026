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
namespace iStick2War_V2
{
public class ParatrooperView_V2 : MonoBehaviour
{
    private static readonly string[] DefaultGibBoneNames =
    {
        "foot-back",
        "foot-front",
        "gunBone",
        "leg-upper-back",
        "leg-lower-back",
        "leg-upper-front",
        "leg-lower-front",
        "arm-upper-back",
        "arm-lower-back",
        "arm-upper-front",
        "arm-lower-front",
        "chest",
        "head"
    };

    [System.Serializable]
    private class GibPartPrefabEntry
    {
        public string Label;
        public string BoneName;
        public GameObject Prefab;
        public Vector3 LocalOffset = Vector3.zero;
        public float Scale = 1f;
    }

    private static readonly string[] ParachuteKeywords = { "parach", "chute", "canopy", "glide" };
    private SkeletonAnimation _skeletonAnimation;
    private ParticleSystem hitEffect;
    private StickmanBodyState _lastStateBeforeChange;
    private bool _deathAnimationLocked;
    private bool _suppressParachuteVisuals;
    private bool _groundDeathParachuteLogPrinted;
    private bool _isExploded;

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
    [SerializeField] private bool _debugAnimationLogs = false;
    [SerializeField] private bool _debugParachuteLogs = false;
    [Header("Explosive Death")]
    [SerializeField] private float _gibForce = 9.5f;
    [SerializeField] private float _gibTorque = 220f;
    [SerializeField] private float _gibLifetime = 4f;
    [SerializeField] [Range(0f, 1f)] private float _gibForceMultiplier = 0.25f;
    [SerializeField] private List<GibPartPrefabEntry> _gibPartPrefabs = new List<GibPartPrefabEntry>();
    [SerializeField] private bool _debugGibLogs = true;
    [SerializeField] private string _gibSortingLayerName = "Default";
    [SerializeField] private int _gibSortingOrder = 6000;
    [SerializeField] private float _gibWorldZ = 0f;
    [SerializeField] private string _gibPhysicsLayerName = "";

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
        if (_isExploded)
        {
            return;
        }

        LogAnimation("PlayAnimation() - State: " + state);
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
            LogAnimation($"[ParatrooperView_V2] Ignored animation state {state} because death animation is locked.");
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
                // Full-body clip on track 0 (same idea as death). Glide lives on track 1 only in V2; leaving Land on
                // track 1 can fail to replace the visible pose depending on track mix / setup pose.
                if (_landAnim != null && _landAnim.Animation != null)
                {
                    nextAnimation = _landAnim.Animation;
                    trackIndex = 0;
                }
                else
                {
                    if (_landAnim != null)
                    {
                        Debug.LogError(
                            "[ParatrooperView_V2] _landAnim did not resolve to a Spine.Animation (check ReferenceAsset skeleton + animation name, e.g. E/land). Falling back to glide.");
                    }

                    nextAnimation = _glideAnim;
                    trackIndex = 1;
                }

                loop = false;
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
        else if (state == StickmanBodyState.Land && trackIndex == 0)
        {
            _skeletonAnimation.AnimationState.ClearTracks();
        }
        else
        {
            // Land may have left a hold pose on track 0; combat states use track 1 and must not mix with it.
            if (state == StickmanBodyState.Idle || state == StickmanBodyState.Shoot)
            {
                _skeletonAnimation.AnimationState.ClearTrack(0);
            }

            // Clear previous entry on this track to avoid stale blends.
            _skeletonAnimation.AnimationState.ClearTrack(trackIndex);
        }

        var trackEntry = _skeletonAnimation.AnimationState.SetAnimation(trackIndex, nextAnimation, loop);
        if (trackEntry != null)
        {
            trackEntry.MixDuration = 0f;
            if (state == StickmanBodyState.Land && trackIndex == 0)
            {
                trackEntry.TrackTime = 0f;
            }
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
        LogAnimation($"[ParatrooperView_V2] SetAnimation track={trackIndex}, state={state}, clip={nextAnimation.Name}, loop={loop}");
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
        LogAnimation($"[ParatrooperView_V2] Selected ground death animation: {selected.name}");
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
        LogAnimation("PlayDeploy_Complete()");
        PlayGlide();
    }

    public void PlayGlide()
    {
        LogAnimation("PlayGlide()");
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
        LogAnimation($"[ParatrooperView_V2] Track dump {context}: t0={track0} | t1={track1}");
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
            if (_debugParachuteLogs && !_groundDeathParachuteLogPrinted)
            {
                Debug.Log($"[ParatrooperView_V2] Ground death: cleared parachute attachments on {hiddenCount} slot(s).");
                _groundDeathParachuteLogPrinted = true;
            }
        }
    }

    private void LateUpdate()
    {
        if (_isExploded)
        {
            return;
        }

        if (!_suppressParachuteVisuals)
        {
            _groundDeathParachuteLogPrinted = false;
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

    private void Reset()
    {
        if (_gibPartPrefabs != null && _gibPartPrefabs.Count > 0)
        {
            return;
        }

        _gibPartPrefabs = new List<GibPartPrefabEntry>(DefaultGibBoneNames.Length);
        for (int i = 0; i < DefaultGibBoneNames.Length; i++)
        {
            _gibPartPrefabs.Add(new GibPartPrefabEntry
            {
                Label = DefaultGibBoneNames[i],
                BoneName = DefaultGibBoneNames[i]
            });
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

    private void LogAnimation(string message)
    {
        if (_debugAnimationLogs)
        {
            Debug.Log(message);
        }
    }

    public void ExplodeIntoPieces(Vector2 explosionOrigin, float forceMultiplier)
    {
        if (_isExploded)
        {
            return;
        }

        _isExploded = true;
        _deathAnimationLocked = true;
        _suppressParachuteVisuals = true;

        if (_skeletonAnimation != null)
        {
            if (_skeletonAnimation.AnimationState != null)
            {
                _skeletonAnimation.AnimationState.ClearTracks();
            }

            SkeletonRenderer renderer = _skeletonAnimation.GetComponent<SkeletonRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            _skeletonAnimation.enabled = false;
        }

        DisableOriginalCharacterRenderers();

        float launchForce = Mathf.Max(0.1f, _gibForce * Mathf.Max(0.3f, forceMultiplier) * Mathf.Clamp01(_gibForceMultiplier));
        float life = Mathf.Max(0.25f, _gibLifetime);
        bool spawnedVisualGibs = SpawnConfiguredGibPrefabs(explosionOrigin, launchForce, life);

        if (spawnedVisualGibs)
        {
            DisableBodyPartCollidersOnly();
            return;
        }

        ParatrooperBodyPart_V2[] parts = GetComponentsInChildren<ParatrooperBodyPart_V2>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            ParatrooperBodyPart_V2 part = parts[i];
            if (part == null)
            {
                continue;
            }

            Transform partTransform = part.transform;
            Vector2 partPosition = partTransform.position;
            Vector2 away = partPosition - explosionOrigin;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Random.insideUnitCircle;
            }
            away.Normalize();
            away.y = Mathf.Max(0.12f, away.y);

            part.enabled = false;
            partTransform.SetParent(null, true);

            Rigidbody2D rb = part.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = part.gameObject.AddComponent<Rigidbody2D>();
            }

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.gravityScale = 1f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.12f;
            rb.freezeRotation = false;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.AddForce(away * launchForce, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-_gibTorque, _gibTorque), ForceMode2D.Impulse);

            Destroy(part.gameObject, life);
        }
    }

    private bool SpawnConfiguredGibPrefabs(Vector2 explosionOrigin, float launchForce, float life)
    {
        if (_gibPartPrefabs == null || _gibPartPrefabs.Count == 0 || _skeletonAnimation == null || _skeletonAnimation.Skeleton == null)
        {
            return false;
        }

        bool spawnedAny = false;
        int spawnedCount = 0;
        for (int i = 0; i < _gibPartPrefabs.Count; i++)
        {
            GibPartPrefabEntry entry = _gibPartPrefabs[i];
            if (entry == null || entry.Prefab == null || string.IsNullOrWhiteSpace(entry.BoneName))
            {
                continue;
            }

            Bone bone = _skeletonAnimation.Skeleton.FindBone(entry.BoneName);
            if (bone == null)
            {
                continue;
            }

            Vector3 boneWorld = _skeletonAnimation.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            Vector3 spawnPos = boneWorld + entry.LocalOffset;
            spawnPos.z = _gibWorldZ;
            Vector2 away = ((Vector2)spawnPos - explosionOrigin);
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Random.insideUnitCircle;
            }
            away.Normalize();
            away.y = Mathf.Max(0.12f, away.y);

            float angle = Mathf.Atan2(away.y, away.x) * Mathf.Rad2Deg + Random.Range(-20f, 20f);
            GameObject gib = Instantiate(entry.Prefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
            float scaleMultiplier = entry.Scale > 0.001f ? entry.Scale : 1f;
            if (_debugGibLogs && entry.Scale <= 0.001f)
            {
                Debug.LogWarning($"[ParatrooperView_V2] Gib '{entry.Label}' had Scale={entry.Scale}. Using fallback 1.0 so it remains visible.");
            }
            if (gib.transform.localScale.sqrMagnitude <= 0.000001f)
            {
                gib.transform.localScale = Vector3.one;
            }
            gib.transform.localScale *= scaleMultiplier;
            ForceGibVisible(gib);

            Rigidbody2D rb = gib.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gib.AddComponent<Rigidbody2D>();
            }

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.gravityScale = 1f;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.12f;
            rb.freezeRotation = false;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.AddForce(away * launchForce, ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-_gibTorque, _gibTorque), ForceMode2D.Impulse);

            Destroy(gib, life);
            spawnedAny = true;
            spawnedCount++;
        }

        if (_debugGibLogs)
        {
            Debug.Log($"[ParatrooperView_V2] Gib spawn result: spawned={spawnedCount}, configured={_gibPartPrefabs.Count}");
        }

        return spawnedAny;
    }

    private void ForceGibVisible(GameObject gib)
    {
        if (gib == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_gibPhysicsLayerName))
        {
            int physicsLayer = LayerMask.NameToLayer(_gibPhysicsLayerName);
            if (physicsLayer >= 0)
            {
                SetLayerRecursively(gib, physicsLayer);
            }
            else if (_debugGibLogs)
            {
                Debug.LogWarning($"[ParatrooperView_V2] Gib physics layer '{_gibPhysicsLayerName}' was not found. Keeping prefab layer.");
            }
        }
        ActivateHierarchy(gib.transform);
        EnsureNonZeroScale(gib.transform);

        int sortingLayerId = SortingLayer.NameToID(_gibSortingLayerName);
        if (sortingLayerId == 0 && _gibSortingLayerName != "Default")
        {
            sortingLayerId = SortingLayer.NameToID("Default");
        }

        Renderer[] renderers = gib.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = true;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = _gibSortingOrder;

            Material material = renderer.material;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                Color c = material.GetColor("_BaseColor");
                c.a = 1f;
                material.SetColor("_BaseColor", c);
            }
            if (material.HasProperty("_Color"))
            {
                Color c = material.GetColor("_Color");
                c.a = 1f;
                material.SetColor("_Color", c);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", material.GetTexture("_BaseMap") ?? Texture2D.whiteTexture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", material.GetTexture("_MainTex") ?? Texture2D.whiteTexture);
            }
        }

        SpriteRenderer[] sprites = gib.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null)
            {
                continue;
            }

            sr.enabled = true;
            sr.sortingLayerID = sortingLayerId;
            sr.sortingOrder = _gibSortingOrder;
            Color c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null)
            {
                all[i].gameObject.layer = layer;
            }
        }
    }

    private static void ActivateHierarchy(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].gameObject.activeSelf)
            {
                all[i].gameObject.SetActive(true);
            }
        }
    }

    private static void EnsureNonZeroScale(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null)
            {
                continue;
            }

            Vector3 s = t.localScale;
            if (Mathf.Abs(s.x) <= 0.0001f) s.x = 1f;
            if (Mathf.Abs(s.y) <= 0.0001f) s.y = 1f;
            if (Mathf.Abs(s.z) <= 0.0001f) s.z = 1f;
            t.localScale = s;
        }
    }

    private void DisableOriginalCharacterRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            // Do not touch already spawned detached gibs (they are no longer children).
            renderer.enabled = false;
        }
    }

    private void DisableBodyPartCollidersOnly()
    {
        ParatrooperBodyPart_V2[] parts = GetComponentsInChildren<ParatrooperBodyPart_V2>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            ParatrooperBodyPart_V2 part = parts[i];
            if (part == null)
            {
                continue;
            }

            part.enabled = false;
            Collider2D[] colliders = part.GetComponents<Collider2D>();
            for (int c = 0; c < colliders.Length; c++)
            {
                if (colliders[c] != null)
                {
                    colliders[c].enabled = false;
                }
            }
        }
    }
}
}
