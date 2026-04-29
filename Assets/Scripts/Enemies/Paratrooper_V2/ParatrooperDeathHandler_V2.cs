using Assets.Scripts.Components;
using iStick2War;
using System.Collections;
using UnityEngine;


/// <summary>
/// ParatrooperDeathHandler (Death Handling Layer)
/// </summary>
/// <remarks>
/// Handles the full death lifecycle of the Paratrooper entity.
/// Responsible for cleanup, visual death representation, and notifying external systems.
///
/// Responsibilities:
/// - Executes death sequence when triggered
/// - Plays death animation (Spine) or activates ragdoll
/// - Handles cleanup of components and subscriptions
/// - Returns entity to object pool (if pooling is used)
/// - Notifies game systems (e.g., score, GameManager)
///
/// Constraints:
/// - MUST NOT calculate damage (handled by DamageReceiver)
/// - MUST NOT make gameplay decisions (handled by Controller)
/// - SHOULD be triggered by StateMachine entering Dead state
///
/// Notes:
/// - Designed to be the final step in the entity lifecycle
/// - Can be extended with loot drops, sound, or slow-motion effects
/// </remarks>
namespace iStick2War_V2
{
public class ParatrooperDeathHandler_V2 : MonoBehaviour
{
    [Header("Ragdoll")]
    [Tooltip("If enabled, convert the Paratrooper death into physics pieces (gibs) instead of playing Spine death visuals.")]
    [SerializeField] private bool _useRagdoll = true;

    [Tooltip("How much of the Paratrooper root velocity is inherited by each body-part rigidbody.")]
    [SerializeField] private float _ragdollVelocityInheritanceMultiplier = 1f;

    [Tooltip(
        "Radial impulse applied on top of inherited velocity.\n" +
        "Set to 0 to let bounding box geometry + ground collisions drive the scatter direction.")]
    [SerializeField] private float _ragdollRadialImpulseMultiplier = 0f;

    [Tooltip("Small random torque to help pieces keep moving. Keep this modest (e.g. 0.2-0.5).")]
    [SerializeField] private float _ragdollRandomTorqueImpulseMultiplier = 0.35f;

    private ParatrooperView_V2 _view;

    private Rigidbody2D _rootRigidbody2D;
    private Collider2D _rootCollider2D;
    public int scoreValue;
    [Header("Despawn timing")]
    [Tooltip("Delay before despawn for normal (ground) deaths.")]
    [SerializeField] private float _groundDeathDespawnDelaySeconds = 2f;
    [Tooltip("Extra delay after airborne death has reached land/impact state.")]
    [SerializeField] private float _airborneImpactDespawnDelaySeconds = 1.6f;
    [Tooltip("Safety cap: max time to wait for GlideDie to reach ground/land before forced cleanup.")]
    [SerializeField] private float _maxWaitForAirborneGroundImpactSeconds = 12f;

    private ParatrooperStateMachine_V2 _stateMachine;
    private bool _isDying;
    public event System.Action<ParatrooperDeathHandler_V2> OnDeathStarted;

    private void Awake()
    {
        _view = GetComponentInChildren<ParatrooperView_V2>(true);
        _rootRigidbody2D = GetComponent<Rigidbody2D>();
        _rootCollider2D = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        _isDying = false;
        StopAllCoroutines();
    }

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
    /// Entry point for the death sequence.
    /// </summary>
    public void Die()
    {
        if (_isDying)
        {
            return;
        }

        _isDying = true;
        OnDeathStarted?.Invoke(this);
        StartCoroutine(DeathRoutine());
    }
    
    /// <summary>
    /// Immediately despawns this instance (pool-safe), bypassing death delay.
    /// </summary>
    public void ForceDespawnImmediately(string reason = null)
    {
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Debug.LogWarning($"[ParatrooperDeathHandler_V2] ForceDespawnImmediately: {reason}");
        }

        _isDying = true;
        StopAllCoroutines();
        OnDeathStarted?.Invoke(this);
        SimplePrefabPool_V2.Despawn(gameObject);
    }

    IEnumerator DeathRoutine()
    {
        bool startedAirborneDeath = _stateMachine != null && _stateMachine.CurrentState == StickmanBodyState.GlideDie;
        bool shouldDelayRagdollUntilImpact = _useRagdoll && startedAirborneDeath;

        if (!shouldDelayRagdollUntilImpact)
        {
            PlayRagdollOrSpineDeath();
        }

        if (startedAirborneDeath)
        {
            float maxWait = Mathf.Max(0.5f, _maxWaitForAirborneGroundImpactSeconds);
            float startedAt = Time.unscaledTime;
            while (_stateMachine != null &&
                   _stateMachine.CurrentState == StickmanBodyState.GlideDie &&
                   Time.unscaledTime - startedAt < maxWait)
            {
                yield return null;
            }

            // StateMachine has left GlideDie (typically Land/Die): now convert to physics pieces.
            if (shouldDelayRagdollUntilImpact)
            {
                PlayRagdollOrSpineDeath();
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, _airborneImpactDespawnDelaySeconds));
        }
        else
        {
            yield return new WaitForSeconds(Mathf.Max(0.05f, _groundDeathDespawnDelaySeconds));
        }

        NotifyGameManager();
        Cleanup();
    }

    /// <summary>
    /// Plays the appropriate death animation or activates ragdoll physics.
    /// </summary>
    private void PlayRagdollOrSpineDeath()
    {
        if (_useRagdoll)
        {
            if (_view != null)
            {
                Vector2 inheritedVel = _rootRigidbody2D != null ? _rootRigidbody2D.linearVelocity : Vector2.zero;
                float inheritedAngVel = _rootRigidbody2D != null ? _rootRigidbody2D.angularVelocity : 0f;

                Vector2 origin = _view.transform.position;
                // Spawn visible physics pieces (severed-part prefabs) so the paratrooper
                // doesn't "disappear" due to hitbox-only renderer absence.
                _view.RagdollScatterUsingSeveredPartPrefabs(
                    explosionOrigin: origin,
                    inheritedLinearVelocity: inheritedVel * _ragdollVelocityInheritanceMultiplier,
                    inheritedAngularVelocity: inheritedAngVel,
                    radialImpulseMultiplier: _ragdollRadialImpulseMultiplier,
                    randomTorqueImpulseMultiplier: _ragdollRandomTorqueImpulseMultiplier,
                    positionJitterRadius: 0.03f);
            }

            // Stop root physics so the remaining (severed/exploded) pieces fully drive the look.
            if (_rootRigidbody2D != null)
            {
                _rootRigidbody2D.linearVelocity = Vector2.zero;
                _rootRigidbody2D.angularVelocity = 0f;
                _rootRigidbody2D.simulated = false;
            }

            if (_rootCollider2D != null)
            {
                _rootCollider2D.enabled = false;
            }
            return;
        }

        // Spine death visuals are handled by ParatrooperView_V2 via state-machine events.
        // Leaving this empty keeps the default "play animation" behavior.
    }

    /// <summary>
    /// Notifies external systems such as score tracking or game state managers.
    /// </summary>
    private void NotifyGameManager()
    {
        // e.g., GameManager.AddScore(...)
    }

    /// <summary>
    /// Cleans up the entity and prepares it for pooling or destruction.
    /// </summary>
    private void Cleanup()
    {
        // Disable components, unsubscribe events, return to pool, etc.
        SimplePrefabPool_V2.Despawn(gameObject);
    }

}
}
