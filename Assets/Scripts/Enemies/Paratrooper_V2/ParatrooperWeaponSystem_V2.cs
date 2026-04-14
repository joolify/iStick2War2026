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
    [SerializeField] private Transform _shotTrailPrefab;
    [SerializeField] private float _lineVisibleDuration = 0.12f;
    [SerializeField] private bool _debugDrawShotRay = true;
    [SerializeField] private bool _debugShotLineLogs = true;
    [SerializeField] private float _lineWidth = 0.06f;
    [SerializeField] private Color _lineColor = new Color(1f, 0.95f, 0.5f, 1f);
    [SerializeField] private int _lineSortingOrder = 500;

    private float _lastFireTime = -999f;
    private Coroutine _lineCoroutine;
    private int _lineSortingLayerId = -1;
    private Collider2D _cachedHeroCollider;

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

        // If a prefab was assigned to _shotLineRenderer in inspector, treat it as a trail prefab.
        if (_shotTrailPrefab == null && _shotLineRenderer != null && !_shotLineRenderer.gameObject.scene.IsValid())
        {
            _shotTrailPrefab = _shotLineRenderer.transform;
            _shotLineRenderer = null;
        }

        EnsureShotLineRenderer();
        CacheLineSortingLayer();

        _heroRoot = FindFirstObjectByType<Hero_V2>();
        _heroModel = _heroRoot != null ? _heroRoot.GetComponent<HeroModel_V2>() : FindFirstObjectByType<HeroModel_V2>();
        CacheHeroCollider();
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
        Debug.Log("[ParatrooperWeaponSystem_V2] Auto-shoot triggered.");
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
        if (_heroRoot == null && _heroModel != null)
        {
            _heroRoot = _heroModel.GetComponentInParent<Hero_V2>();
        }
        CacheHeroCollider();

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

        // Prefer physical hit point; if masked miss, snap to nearest visible Hero collider point.
        Vector2 finalPos = hit.collider != null ? hit.point : origin + direction * _range;
        if (hit.collider == null)
        {
            finalPos = ResolveHeroVisualTarget(origin, finalPos);
        }

        if (_debugDrawShotRay)
        {
            Debug.DrawLine(origin, finalPos, Color.green, 0.5f);
        }
        PlayShotLine(origin, finalPos);

        bool didApplyDamage = false;
        if (hit.collider != null)
        {
            Hero_V2 heroRoot = hit.collider.GetComponentInParent<Hero_V2>();
            if (heroRoot != null)
            {
                Debug.Log($"[ParatrooperWeaponSystem_V2] Hit Hero_V2 for {_baseDamage} damage.");
                heroRoot.ReceiveDamage(_baseDamage);
                didApplyDamage = true;
            }
            else
            {
                HeroModel_V2 hero = hit.collider.GetComponentInParent<HeroModel_V2>();
                if (hero != null)
                {
                    Debug.Log($"[ParatrooperWeaponSystem_V2] Hit HeroModel_V2 for {_baseDamage} damage.");
                    hero.TakeDamage(_baseDamage);
                    didApplyDamage = true;
                }
            }
        }

        // Last-resort fallback: still allow enemy bullets to hurt Hero if collider/layer setup is incomplete.
        if (!didApplyDamage && _heroModel != null)
        {
            float distanceToHero = Vector2.Distance(origin, _heroModel.transform.position);
            if (distanceToHero <= _range)
            {
                if (_heroRoot != null)
                {
                    _heroRoot.ReceiveDamage(_baseDamage);
                }
                else
                {
                    _heroModel.TakeDamage(_baseDamage);
                }

                Debug.LogWarning("[ParatrooperWeaponSystem_V2] Applied fallback hero damage. Verify Hero collider/layer setup.");
            }
        }

        // No second line pass here; visual endpoint is already stabilized above.
    }

    private void PlayShotLine(Vector2 from, Vector2 to)
    {
        if (_shotTrailPrefab != null)
        {
            Transform spawnedTrail = Instantiate(_shotTrailPrefab, from, Quaternion.identity);
            spawnedTrail.localScale = Vector3.one; // Ignore tiny prefab scale that can hide the line.
            LineRenderer spawnedLine = spawnedTrail.GetComponent<LineRenderer>();
            if (spawnedLine == null)
            {
                spawnedLine = spawnedTrail.GetComponentInChildren<LineRenderer>(true);
            }
            if (spawnedLine != null)
            {
                ConfigureAndRenderLine(spawnedLine, from, to);
                if (_debugShotLineLogs)
                {
                    Debug.Log($"[ParatrooperWeaponSystem_V2] Shot line rendered from TRAIL prefab. from={from}, to={to}, width={spawnedLine.widthMultiplier:0.000}, sortingOrder={spawnedLine.sortingOrder}, scale={spawnedLine.transform.lossyScale}");
                }
            }
            else
            {
                Debug.LogWarning("[ParatrooperWeaponSystem_V2] Shot trail prefab has no LineRenderer (root/children).");
            }
            Destroy(spawnedTrail.gameObject, Mathf.Max(0.01f, _lineVisibleDuration));
            return;
        }

        // If _shotLineRenderer points to a prefab asset, spawn an instance and render there.
        if (_shotLineRenderer != null && !_shotLineRenderer.gameObject.scene.IsValid())
        {
            LineRenderer spawnedLine = Instantiate(_shotLineRenderer, from, Quaternion.identity);
            ConfigureAndRenderLine(spawnedLine, from, to);
            if (_debugShotLineLogs)
            {
                Debug.Log($"[ParatrooperWeaponSystem_V2] Shot line rendered from LINE prefab. from={from}, to={to}, width={spawnedLine.widthMultiplier:0.000}, sortingOrder={spawnedLine.sortingOrder}");
            }
            Destroy(spawnedLine.gameObject, Mathf.Max(0.01f, _lineVisibleDuration));
            return;
        }

        if (_shotLineRenderer == null)
        {
            EnsureShotLineRenderer();
        }

        if (_shotLineRenderer == null)
        {
            return;
        }

        _shotLineRenderer.positionCount = 2;
        ConfigureAndRenderLine(_shotLineRenderer, from, to);
        if (_debugShotLineLogs)
        {
            Debug.Log($"[ParatrooperWeaponSystem_V2] Shot line rendered from SCENE line. from={from}, to={to}, width={_shotLineRenderer.widthMultiplier:0.000}, sortingOrder={_shotLineRenderer.sortingOrder}");
        }

        if (_lineCoroutine != null)
        {
            StopCoroutine(_lineCoroutine);
        }

        _lineCoroutine = StartCoroutine(HideShotLineAfterDelay());
    }

    private void EnsureShotLineRenderer()
    {
        // Prefab-based trail mode: no persistent scene LineRenderer needed.
        if (_shotTrailPrefab != null)
        {
            return;
        }

        // If this is still a prefab asset reference, skip runtime mutation.
        if (_shotLineRenderer != null && !_shotLineRenderer.gameObject.scene.IsValid())
        {
            return;
        }

        if (_shotLineRenderer == null)
        {
            var child = transform.Find("ParatrooperShotLine");
            if (child == null)
            {
                GameObject lineGo = new GameObject("ParatrooperShotLine");
                lineGo.transform.SetParent(transform, false);
                child = lineGo.transform;
            }

            _shotLineRenderer = child.GetComponent<LineRenderer>();
            if (_shotLineRenderer == null)
            {
                _shotLineRenderer = child.gameObject.AddComponent<LineRenderer>();
            }
        }

        _shotLineRenderer.useWorldSpace = true;
        _shotLineRenderer.enabled = false;
        _shotLineRenderer.positionCount = 2;
        _shotLineRenderer.widthMultiplier = _lineWidth;
        _shotLineRenderer.numCapVertices = 2;
        _shotLineRenderer.numCornerVertices = 0;
        _shotLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _shotLineRenderer.receiveShadows = false;
        _shotLineRenderer.textureMode = LineTextureMode.Stretch;
        _shotLineRenderer.alignment = LineAlignment.View;
        _shotLineRenderer.startColor = _lineColor;
        _shotLineRenderer.endColor = _lineColor;

        if (_shotLineRenderer.sharedMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                _shotLineRenderer.sharedMaterial = new Material(spriteShader);
            }
        }
    }

    private void CacheLineSortingLayer()
    {
        if (_lineSortingLayerId >= 0)
        {
            return;
        }

        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            _lineSortingLayerId = sr.sortingLayerID;
        }
        else
        {
            _lineSortingLayerId = SortingLayer.NameToID("Default");
        }
    }

    private void ConfigureAndRenderLine(LineRenderer line, Vector2 from, Vector2 to)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = false;
        line.transform.localScale = Vector3.one;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startColor = _lineColor;
        line.endColor = _lineColor;
        float runtimeWidth = Mathf.Max(0.08f, _lineWidth);
        line.widthMultiplier = runtimeWidth;
        line.startWidth = line.widthMultiplier;
        line.endWidth = line.widthMultiplier;
        line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        line.numCapVertices = 2;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.sortingOrder = Mathf.Max(500, _lineSortingOrder);
        line.sortingLayerID = _lineSortingLayerId;
        line.startColor = new Color(_lineColor.r, _lineColor.g, _lineColor.b, 1f);
        line.endColor = new Color(_lineColor.r, _lineColor.g, _lineColor.b, 1f);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(_lineColor, 0f),
                new GradientColorKey(_lineColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        line.colorGradient = gradient;

        if (line.sharedMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader != null)
            {
                line.sharedMaterial = new Material(spriteShader);
            }
        }

        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.enabled = true;
    }

    private void CacheHeroCollider()
    {
        if (_cachedHeroCollider != null)
        {
            return;
        }

        if (_heroRoot != null)
        {
            _cachedHeroCollider = _heroRoot.GetComponentInChildren<Collider2D>();
            if (_cachedHeroCollider != null)
            {
                return;
            }
        }

        if (_heroModel != null)
        {
            _cachedHeroCollider = _heroModel.GetComponentInChildren<Collider2D>();
        }

        if (_debugShotLineLogs && _cachedHeroCollider != null)
        {
            Bounds b = _cachedHeroCollider.bounds;
            Debug.Log($"[ParatrooperWeaponSystem_V2] Cached Hero collider='{_cachedHeroCollider.name}', center={b.center}, size={b.size}");
        }
    }

    private Vector2 ResolveHeroVisualTarget(Vector2 origin, Vector2 fallback)
    {
        CacheHeroCollider();
        if (_cachedHeroCollider == null)
        {
            if (_debugShotLineLogs)
            {
                Debug.LogWarning($"[ParatrooperWeaponSystem_V2] No Hero collider cached. Using fallback target={fallback}");
            }
            return fallback;
        }

        Bounds b = _cachedHeroCollider.bounds;
        float targetY = Mathf.Lerp(b.min.y, b.max.y, 0.78f); // upper torso/head area
        Vector2 target = new Vector2(b.center.x, targetY);
        if (_debugShotLineLogs)
        {
            Debug.Log($"[ParatrooperWeaponSystem_V2] ResolveHeroVisualTarget origin={origin}, target={target}, collider='{_cachedHeroCollider.name}', boundsMin={b.min}, boundsMax={b.max}");
        }
        return target;
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
