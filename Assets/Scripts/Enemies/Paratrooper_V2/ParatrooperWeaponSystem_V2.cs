using Assets.Scripts.Components;
using Assets.Scripts.Hero_V2;
using System;
using System.Collections;
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
    private HeroModel_V2 _heroModel;
    private Hero_V2 _heroRoot;

    [Header("Shooting")]
    [SerializeField] private float _fireCooldown = 0.14f;
    [SerializeField] private float _range = 100f;
    [SerializeField] private int _baseDamage = 9;
    [SerializeField] private LayerMask _whatToHit = 0;
    [SerializeField] private Transform _firePoint;

    [Header("Line Renderer Effect")]
    [SerializeField] private LineRenderer _shotLineRenderer;
    [SerializeField] private float _lineVisibleDuration = 0.05f;

    private float _lastFireTime = -999f;
    private Coroutine _lineCoroutine;

    public void Initialize(ParatrooperModel_V2 model)
    {
        _model = model;

        if (_firePoint == null)
        {
            _firePoint = transform;
        }

        if (_whatToHit.value == 0)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                _whatToHit = 1 << playerLayer;
            }
        }

        if (_shotLineRenderer != null)
        {
            _shotLineRenderer.enabled = false;
            _shotLineRenderer.positionCount = 2;
        }

        _heroRoot = FindFirstObjectByType<Hero_V2>();
        _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindFirstObjectByType<HeroModel_V2>();
    }

    /// <summary>
    /// Called by Controller to indicate intent to shoot.
    /// </summary>
    public bool CanShoot()
    {
        if (_model == null)
            return false;

        if (Time.time < _lastFireTime + _fireCooldown)
            return false;

        if (_model.currentState == iStick2War.StickmanBodyState.Die || _model.currentState == iStick2War.StickmanBodyState.GlideDie)
            return false;

        return true;
    }

    public void TryAutoShootAtHero()
    {
        if (!CanShoot())
            return;

        _lastFireTime = Time.time;
        ShootRaycastAtHero();
    }

    private void ShootRaycastAtHero()
    {
        if (_heroModel == null)
        {
            if (_heroRoot == null)
            {
                _heroRoot = FindFirstObjectByType<Hero_V2>();
            }

            if (_heroRoot != null)
            {
                _heroModel = _heroRoot.GetComponent<HeroModel_V2>();
            }
        }

        if (_heroModel == null)
        {
            _heroModel = FindFirstObjectByType<HeroModel_V2>();
        }

        if (_heroModel == null || _heroModel.isDead)
        {
            return;
        }

        Vector2 origin = _firePoint != null ? _firePoint.position : transform.position;
        Vector2 target = _heroModel.transform.position;
        Vector2 direction = (target - origin).normalized;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, _range, _whatToHit);
        if (hit.collider == null)
        {
            // Fallback: still detect Hero even if Player layer/mask is misconfigured.
            var allHits = Physics2D.RaycastAll(origin, direction, _range);
            for (int i = 0; i < allHits.Length; i++)
            {
                var candidate = allHits[i];
                if (candidate.collider == null)
                {
                    continue;
                }

                if (candidate.collider.GetComponentInParent<Hero_V2>() != null ||
                    candidate.collider.GetComponentInParent<HeroModel_V2>() != null)
                {
                    hit = candidate;
                    Debug.LogWarning("[ParatrooperWeaponSystem_V2] Hero hit outside Player mask. Verify hero layer/mask setup.");
                    break;
                }
            }
        }

        Vector2 finalPos = hit.collider != null ? hit.point : origin + direction * _range;

        Debug.DrawLine(origin, finalPos, Color.cyan, 0.5f);
        PlayShotLine(origin, finalPos);

        if (hit.collider != null)
        {
            Hero_V2 heroRoot = hit.collider.GetComponentInParent<Hero_V2>();
            if (heroRoot != null)
            {
                heroRoot.ReceiveDamage(_baseDamage);
                return;
            }

            HeroModel_V2 hero = hit.collider.GetComponentInParent<HeroModel_V2>();
            if (hero != null)
            {
                hero.TakeDamage(_baseDamage);
            }
        }
    }

    private void PlayShotLine(Vector2 from, Vector2 to)
    {
        if (_shotLineRenderer == null)
        {
            return;
        }

        _shotLineRenderer.positionCount = 2;
        _shotLineRenderer.SetPosition(0, from);
        _shotLineRenderer.SetPosition(1, to);
        _shotLineRenderer.enabled = true;

        if (_lineCoroutine != null)
        {
            StopCoroutine(_lineCoroutine);
        }

        _lineCoroutine = StartCoroutine(HideShotLineAfterDelay());
    }

    private IEnumerator HideShotLineAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, _lineVisibleDuration));
        if (_shotLineRenderer != null)
        {
            _shotLineRenderer.enabled = false;
        }
        _lineCoroutine = null;
    }

}
