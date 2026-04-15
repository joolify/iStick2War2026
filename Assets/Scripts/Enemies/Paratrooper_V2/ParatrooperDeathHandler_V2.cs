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
    bool useRagdoll;
    public int scoreValue;

    private ParatrooperStateMachine_V2 _stateMachine;
    private bool _isDying;

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
        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        PlayRagdollOrSpineDeath();
        yield return new WaitForSeconds(2f);
        NotifyGameManager();
        Cleanup();
    }

    /// <summary>
    /// Plays the appropriate death animation or activates ragdoll physics.
    /// </summary>
    private void PlayRagdollOrSpineDeath()
    {
        // Decide between ragdoll or animation
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
        gameObject.SetActive(false);
    }

}
}
