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
        Debug.Log("ParatrooperBodyPart_V2.OnHit: " + info.BaseDamage + "HP on " + info.BodyPart);
        info.BodyPart = bodyPart;
        _damageReceiver.TakeDamage(info);
    }
}
