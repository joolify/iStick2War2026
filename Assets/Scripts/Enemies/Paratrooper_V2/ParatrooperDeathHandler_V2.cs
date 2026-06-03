using Assets.Scripts.Components;
using iStick2War;
using System.Collections;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * ParatrooperDeathHandler_V2 (Death lifecycle + despawn)
 *
 * PURPOSE:
 * Runs the death routine: Spine death clips via ParatrooperView_V2 / state machine, timed delays
 * for ground vs airborne impact, then returns the instance to SimplePrefabPool_V2. Exposes events
 * such as OnDeathStarted for external listeners.
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES
 *
 * - Die() / ForceDespawnImmediately entry points
 * - Coroutine-driven sequencing when GlideDie must wait for ground impact clip before despawn
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Compute incoming damage (ParatrooperDamageReceiver_V2)
 * - Drive routine AI (ParatrooperController_V2)
 *
 * ---------------------------------------------------------
 * NOTE
 *
 * Invoked from damage / health exhaustion paths; not exclusively bound to one state-machine transition.
 */
    public class ParatrooperDeathHandler_V2 : MonoBehaviour
    {
        [Header("Ragdoll (legacy)")]
        [Tooltip("Deprecated: keep off. Death uses Spine land_fall_down_back* clips from ParatrooperView_V2.")]
        [SerializeField] private bool _useRagdoll = false;

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

            if (_useRagdoll)
            {
                bool shouldDelayRagdollUntilImpact = startedAirborneDeath;
                if (!shouldDelayRagdollUntilImpact)
                {
                    PlayRagdollDeathIfEnabled();
                }
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

                if (_useRagdoll)
                {
                    PlayRagdollDeathIfEnabled();
                }

                yield return new WaitForSeconds(Mathf.Max(0.05f, _airborneImpactDespawnDelaySeconds));
            }
            else
            {
                if (_useRagdoll)
                {
                    PlayRagdollDeathIfEnabled();
                }

                yield return new WaitForSeconds(Mathf.Max(0.05f, _groundDeathDespawnDelaySeconds));
            }

            NotifyGameManager();
            Cleanup();
        }

        // Legacy ragdoll scatter; Spine land_fall_down_back* is driven by ParatrooperView_V2 when _useRagdoll is off.
        private void PlayRagdollDeathIfEnabled()
        {
            if (!_useRagdoll || _view == null)
            {
                return;
            }

            Vector2 inheritedVel = _rootRigidbody2D != null ? _rootRigidbody2D.linearVelocity : Vector2.zero;
            float inheritedAngVel = _rootRigidbody2D != null ? _rootRigidbody2D.angularVelocity : 0f;
            Vector2 origin = _view.transform.position;
            _view.RagdollScatterUsingSeveredPartPrefabs(
                explosionOrigin: origin,
                inheritedLinearVelocity: inheritedVel * _ragdollVelocityInheritanceMultiplier,
                inheritedAngularVelocity: inheritedAngVel,
                radialImpulseMultiplier: _ragdollRadialImpulseMultiplier,
                randomTorqueImpulseMultiplier: _ragdollRandomTorqueImpulseMultiplier,
                positionJitterRadius: 0.03f);

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
