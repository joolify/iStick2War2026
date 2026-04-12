using Assets.Scripts.Components;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// ParatrooperWeaponSystem (Combat Execution Layer)
/// </summary>
/// <remarks>
/// Handles all weapon-related logic for the Paratrooper entity.
///
/// Input Sources:
/// - Controller (AI intent: wants to shoot)
/// - Spine events (animation timing: when to shoot)
///
/// Responsibilities:
/// - Executes shooting logic (raycast or projectile)
/// - Handles cooldowns
/// - Validates if shooting is allowed
///
/// Constraints:
/// - MUST NOT decide when to shoot (Controller does that)
/// - MUST NOT depend on animation logic directly
/// - MUST remain deterministic
/// </remarks>
public class ParatrooperWeaponSystem_V2 : MonoBehaviour
{
    private ParatrooperModel_V2 _model;

    private float fireCooldown = 0.5f;
    private float lastFireTime;

    private Transform _firePoint;
    private LayerMask _hitMask;

    /// <summary>
    /// Initialize weapon system.
    /// </summary>
    public void Initialize(ParatrooperModel_V2 model, Transform firePoint, LayerMask hitMask)
    {
        _model = model;
        _firePoint = firePoint;
        _hitMask = hitMask;
    }

    /// <summary>
    /// Called by Controller to indicate intent to shoot.
    /// </summary>
    public bool CanShoot()
    {
        if (Time.time < lastFireTime + fireCooldown)
            return false;

        if (_model.currentState == iStick2War.StickmanBodyState.Die)
            return false;

        return true;
    }

    /// <summary>
    /// Called by Spine event ("shoot") to actually fire.
    /// </summary>
    public void Fire()
    {
        if (!CanShoot())
            return;

        lastFireTime = Time.time;

        ShootRaycast();
    }

    private void ShootRaycast()
    {
        Vector2 origin = _firePoint.position;
        Vector2 direction = _firePoint.right; // assuming right = forward

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, 100f, _hitMask);

        Debug.DrawLine(origin, origin + direction * 100f, Color.red, 0.5f);

        if (hit.collider != null)
        {
            var bodyPart = hit.collider.GetComponent<ParatrooperBodyPart_V2>();

            if (bodyPart != null)
            {
                DamageInfo info = new DamageInfo
                {
                    BaseDamage = 10f,
                    BodyPart = bodyPart.bodyPart,
                    HitPoint = hit.point
                };

                bodyPart.OnHit(info);
            }
        }
    }

    internal void Shoot()
    {
        throw new NotImplementedException();
    }

    internal void Reload()
    {
        throw new NotImplementedException();
    }

    internal void Grenade()
    {
        throw new NotImplementedException();
    }
}
