using System;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * SwedishPlaneSurvivalCoordinator_V2 — spawns the neutral supply plane between Survival waves.
     * Replaces the campaign shop intermission (no ShopPanel in Survival).
     */
    public sealed class SwedishPlaneSurvivalCoordinator_V2 : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private SwedishPlane_V2 _planePrefab;
        [SerializeField] private SwedishPlanePowerUp_V2 _powerUpPrefab;
        [SerializeField] private SurvivalPowerUpCatalog_V2 _catalog;

        [Header("Spawn")]
        [SerializeField] private Camera _gameplayCamera;
        [SerializeField] private float _offscreenBeyondFrustumHorizontalWorld = 4f;
        [Tooltip("Fallback lane height inside the camera frustum when ground probe fails (0=bottom, 1=top).")]
        [SerializeField] private float _flyLaneVerticalNormalized01 = 0.58f;
        [SerializeField] private float _orthographicFrustumInsetPadding = 0.35f;
        [Tooltip("When enabled, fly lane Y is clamped to ground surface + height offset (same idea as EnemySpawner_V2).")]
        [SerializeField] private bool _alignFlyLaneYToGroundSurface = true;
        [SerializeField] private float _flyLaneHeightAboveGroundWorld = 4.25f;
        [SerializeField] private float _groundProbeMaxDistanceWorld = 80f;
        [SerializeField] private int _dropsPerPass = 1;
        [SerializeField] private float _graceSecondsAfterPassBeforeNextWave = 6f;

        [Header("References")]
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private Hero_V2 _hero;

        private Action _pendingOnSupplyComplete;
        private float _graceEndsAt;
        private bool _passComplete;
        private bool _waitingForGrace;

        private void Awake()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Include);
            }

            if (_hero == null)
            {
                _hero = FindAnyObjectByType<Hero_V2>(FindObjectsInactive.Include);
            }

            if (_gameplayCamera == null)
            {
                _gameplayCamera = Camera.main;
            }
        }

        public void BeginSupplyPassAfterWave(Action onSupplyComplete)
        {
            _pendingOnSupplyComplete = onSupplyComplete;
            _passComplete = false;
            _waitingForGrace = false;
            _graceEndsAt = 0f;

            if (_planePrefab == null || _powerUpPrefab == null || _catalog == null)
            {
                CompleteSupplyIntermissionImmediately();
                return;
            }

            if (!TryComputeOffscreenSpawn(out bool fromLeft, out Vector3 spawnPos))
            {
                CompleteSupplyIntermissionImmediately();
                return;
            }

            SwedishPlane_V2 plane = SimplePrefabPool_V2.Spawn(_planePrefab, spawnPos, Quaternion.identity);
            if (plane == null)
            {
                CompleteSupplyIntermissionImmediately();
                return;
            }

            plane.InitializeForSpawn();

            var config = new SwedishPlaneRunConfig_V2
            {
                powerUpPrefab = _powerUpPrefab,
                catalog = _catalog,
                spawnedFromLeft = fromLeft,
                gameplayCamera = _gameplayCamera != null ? _gameplayCamera : Camera.main,
                dropsThisPass = Mathf.Max(1, _dropsPerPass),
                onPassComplete = HandlePlanePassComplete,
                survivalCoordinator = this
            };

            plane.BeginSupplyRun(config);
        }

        private void Update()
        {
            if (!_waitingForGrace)
            {
                return;
            }

            if (Time.time < _graceEndsAt)
            {
                return;
            }

            CompleteSupplyIntermissionImmediately();
        }

        private void HandlePlanePassComplete()
        {
            _passComplete = true;
            _waitingForGrace = true;
            _graceEndsAt = Time.time + Mathf.Max(0f, _graceSecondsAfterPassBeforeNextWave);
        }

        private void CompleteSupplyIntermissionImmediately()
        {
            _waitingForGrace = false;
            Action callback = _pendingOnSupplyComplete;
            _pendingOnSupplyComplete = null;
            callback?.Invoke();
        }

        private bool TryComputeOffscreenSpawn(out bool fromLeft, out Vector3 spawnPos)
        {
            fromLeft = UnityEngine.Random.value < 0.5f;
            spawnPos = Vector3.zero;

            Camera cam = _gameplayCamera != null ? _gameplayCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return false;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float pad = Mathf.Clamp(_orthographicFrustumInsetPadding, 0f, halfHeight * 0.45f);
            Vector3 camPos = cam.transform.position;
            float margin = Mathf.Max(0f, _offscreenBeyondFrustumHorizontalWorld);
            float visibleMinX = camPos.x - halfWidth;
            float visibleMaxX = camPos.x + halfWidth;
            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;
            float yFromFrustum = Mathf.Lerp(minY, maxY, Mathf.Clamp01(_flyLaneVerticalNormalized01));
            float y = ResolveFlyLaneWorldY(cam, camPos, minY, maxY, yFromFrustum);
            float x = fromLeft ? visibleMinX - margin : visibleMaxX + margin;
            spawnPos = new Vector3(x, y, 0f);
            return true;
        }

        private float ResolveFlyLaneWorldY(Camera cam, Vector3 camPos, float minY, float maxY, float yFromFrustum)
        {
            if (!_alignFlyLaneYToGroundSurface || cam == null)
            {
                return yFromFrustum;
            }

            int mask = ResolveGroundLayerMask();
            if (mask == 0)
            {
                return yFromFrustum;
            }

            if (!TryProbeGroundSurfaceY(cam, camPos, mask, out float groundY))
            {
                return yFromFrustum;
            }

            float candidate = groundY + Mathf.Max(0f, _flyLaneHeightAboveGroundWorld);
            return Mathf.Clamp(candidate, minY, maxY);
        }

        private bool TryProbeGroundSurfaceY(Camera cam, Vector3 camPos, int mask, out float groundY)
        {
            groundY = 0f;
            float halfHeight = cam.orthographicSize;
            float pad = Mathf.Clamp(_orthographicFrustumInsetPadding, 0f, halfHeight * 0.45f);
            float topY = camPos.y + halfHeight - pad;
            Vector2 origin = new Vector2(camPos.x, topY);
            float dist = Mathf.Max(1f, _groundProbeMaxDistanceWorld);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, dist, mask);
            if (hit.collider == null)
            {
                return false;
            }

            groundY = hit.point.y;
            return true;
        }

        private static int ResolveGroundLayerMask()
        {
            int ground = LayerMask.NameToLayer("Ground");
            return ground >= 0 ? 1 << ground : 0;
        }

        // Called from SwedishPlaneController when spawning a powerup so pickup can resolve hero/wave.
        public void BindRuntimeContextToPowerUp(SwedishPlanePowerUp_V2 powerUp, SurvivalPowerUpOffer_V2 offer)
        {
            if (powerUp == null)
            {
                return;
            }

            powerUp.BeginDrop(offer, _waveManager, _hero);
        }
    }
}
