using Assets.Scripts.Components;
using iStick2War;
using UnityEngine;

/// <summary>
/// ParatrooperBodyPart (Hitbox Layer)
/// </summary>
/// <remarks>
/// Represents a single hitbox (body part) of the Paratrooper entity.
/// This component is intentionally minimal and acts only as a relay between
/// hit detection and the damage system.
///
/// Hit Flow:
/// Raycast → BodyPart → DamageReceiver
///
/// Responsibilities:
/// - Identifies which body part was hit
/// - Forwards hit data to the ParatrooperDamageReceiver
///
/// Constraints:
/// - MUST NOT contain any damage calculation logic
/// - MUST NOT modify Model data directly
/// - MUST NOT contain gameplay logic
/// - MUST remain lightweight and efficient (can exist in large numbers)
///
/// Notes:
/// - Typically attached to child GameObjects with colliders
/// - Works with raycasts or collision-based hit detection
/// </remarks>
public class ParatrooperBodyPart_V2 : MonoBehaviour
{

    public BodyPartType bodyPart;

    private ParatrooperDamageReceiver_V2 _damageReceiver;

    private ParatrooperModel_V2 _model;

    void Awake()
    {
        _damageReceiver = GetComponentInParent<ParatrooperDamageReceiver_V2>();

        gameObject.layer = LayerMask.NameToLayer("EnemyBodyPart");

        var ownCollider = GetComponent<Collider2D>();
        if (ownCollider == null)
        {
            Debug.LogWarning($"[ParatrooperBodyPart_V2] No Collider2D on '{gameObject.name}'. This body part cannot be hit by raycast.");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (_damageReceiver == null)
        {
            Debug.LogWarning($"BodyPart has no {nameof(_damageReceiver)} assigned.");
            return;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Called when this body part is hit by a raycast or collision.
    /// Forwards the damage information to the DamageReceiver.
    /// </summary>
    public void OnHit(DamageInfo info)
    {
        info.BodyPart = bodyPart;
        Debug.Log($"[ParatrooperBodyPart_V2] OnHit part={info.BodyPart}, base={info.BaseDamage}, collider={gameObject.name}");
        _damageReceiver.TakeDamage(info);
    }
}
