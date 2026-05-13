using System;
using System.Collections;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * HelicopterCarrier_V2 (Paratrooper drop sequencer for one flight)
 *
 * PURPOSE:
 * Coroutine-driven sequencing: wait for camera/trigger thresholds (and optional extra world-X gates such as
 * avoiding the bunker footprint), invoke configured drop callbacks with world positions, cancel remaining drops
 * when aborted. EnemySpawner_V2 wires Func delegates when mounting paratroopers.
 * Pooled aircraft despawn via SetActive(false) (no OnDestroy): cleanup runs in OnDisable so pending spawner drops
 * are released and the carrier can run again on the next Spawn().
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Pilot helicopter movement (Helicopter_V2 / AircraftFlyAcrossScreen_V2).
 * - Resolve hero damage (Paratrooper / weapon systems).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2 + helicopter)
 *
 * Wires drops → EnemySpawner_V2.cs | Helicopter presentation / state → Enemies/Helicopter_V2/*
 * Simple horizontal motion helper → AircraftFlyAcrossScreen_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Stateless-ish behaviour object parameterized by lambdas so the same component works for left/right flights.
 */
    [DisallowMultipleComponent]
    public sealed class HelicopterCarrier_V2 : MonoBehaviour
    {
        private bool _fromLeft;
        private int _dropCount;
        private float _dropIntervalSeconds;
        private float _dropDelayAfterTriggerSeconds;
        private float _maxWaitForTriggerSeconds;
        private bool _useNamedTriggerPoints;
        private string _leftTriggerName;
        private string _rightTriggerName;
        private float _dropTriggerLeadDistanceWorld;
        private bool _waitOneFrameBeforeFirstDrop;
        private Camera _spawnCamera;
        private float _cameraVisiblePaddingWorld;
        private Func<Vector3, bool> _performOneDrop;
        private Func<Vector3> _getDropWorldPosition;
        // When non-null, drop X must satisfy this before the first trigger snapshot and each spawn sample (e.g. clear of bunker bounds).
        private Func<float, bool> _isDropWorldXAcceptableForSpawn;
        // Upper bound on spinning while waiting for camera + optional X gate; 0 falls back to maxWaitForTriggerSeconds when gate is set.
        private float _maxSecondsWaitForAcceptableDropWorldX;
        private Action<int, string> _cancelRemainingDrops;
        private Action<string> _debugLog;
        private bool _initialized;
        private int _droppedSoFar;
        private bool _isRoutineRunning;
        private bool _loggedTriggerReached;
        private bool _hasTriggerDropPositionSnapshot;
        private Vector3 _triggerDropPositionSnapshot;

        public void Initialize(
            bool fromLeft,
            int dropCount,
            float dropIntervalSeconds,
            float dropDelayAfterTriggerSeconds,
            float maxWaitForTriggerSeconds,
            bool useNamedTriggerPoints,
            string leftTriggerName,
            string rightTriggerName,
            float dropTriggerLeadDistanceWorld,
            bool waitOneFrameBeforeFirstDrop,
            Camera spawnCamera,
            float cameraVisiblePaddingWorld,
            Func<Vector3, bool> performOneDrop,
            Func<Vector3> getDropWorldPosition,
            Func<float, bool> isDropWorldXAcceptableForSpawn,
            float maxSecondsWaitForAcceptableDropWorldX,
            Action<int, string> cancelRemainingDrops,
            Action<string> debugLog)
        {
            _fromLeft = fromLeft;
            _dropCount = Mathf.Max(1, dropCount);
            _dropIntervalSeconds = Mathf.Max(0f, dropIntervalSeconds);
            _dropDelayAfterTriggerSeconds = Mathf.Max(0f, dropDelayAfterTriggerSeconds);
            _maxWaitForTriggerSeconds = Mathf.Max(0.1f, maxWaitForTriggerSeconds);
            _useNamedTriggerPoints = useNamedTriggerPoints;
            _leftTriggerName = leftTriggerName;
            _rightTriggerName = rightTriggerName;
            _dropTriggerLeadDistanceWorld = Mathf.Max(0f, dropTriggerLeadDistanceWorld);
            _waitOneFrameBeforeFirstDrop = waitOneFrameBeforeFirstDrop;
            _spawnCamera = spawnCamera;
            _cameraVisiblePaddingWorld = Mathf.Max(0f, cameraVisiblePaddingWorld);
            _performOneDrop = performOneDrop;
            _getDropWorldPosition = getDropWorldPosition;
            _isDropWorldXAcceptableForSpawn = isDropWorldXAcceptableForSpawn;
            _maxSecondsWaitForAcceptableDropWorldX = Mathf.Max(0f, maxSecondsWaitForAcceptableDropWorldX);
            _cancelRemainingDrops = cancelRemainingDrops;
            _debugLog = debugLog;
            _initialized = true;
            _isRoutineRunning = false;
            _droppedSoFar = 0;
            _loggedTriggerReached = false;
            _hasTriggerDropPositionSnapshot = false;
            _triggerDropPositionSnapshot = Vector3.zero;
        }

        public void BeginDropSequence()
        {
            if (!_initialized || _isRoutineRunning)
            {
                return;
            }

            _isRoutineRunning = true;
            StartCoroutine(RunDropSequence());
        }

        private IEnumerator RunDropSequence()
        {
            try
            {
                float timeoutAt = Time.time + _maxWaitForTriggerSeconds;
                while (Time.time < timeoutAt)
                {
                    if (HasReachedDropEntryTrigger())
                    {
                        break;
                    }

                    yield return null;
                }

                if (_dropDelayAfterTriggerSeconds > 0f)
                {
                    yield return new WaitForSeconds(_dropDelayAfterTriggerSeconds);
                }

                if (_waitOneFrameBeforeFirstDrop)
                {
                    // Let Spine/Bone transforms settle before first spawn sample.
                    yield return null;
                }

                while (_droppedSoFar < _dropCount)
                {
                    Vector3 dropPosForThisAttempt = (_droppedSoFar == 0 && _hasTriggerDropPositionSnapshot)
                        ? _triggerDropPositionSnapshot
                        : GetDropWorldPosition();

                    // Guard each individual drop so delayed bursts cannot spawn offscreen
                    // after the helicopter has already moved past the visible area.
                    float waitAcceptableStarted = Time.time;
                    float acceptTimeout = ResolveAcceptableDropWorldXWaitSeconds();
                    while (!IsDropPointInsideCameraX(dropPosForThisAttempt.x) || !IsDropWorldXAcceptable(dropPosForThisAttempt.x))
                    {
                        if (acceptTimeout > 0f && Time.time - waitAcceptableStarted >= acceptTimeout)
                        {
                            _debugLog?.Invoke(
                                $"[HelicopterCarrier_V2] Drop world-X gate timed out after {acceptTimeout:0.###}s " +
                                $"(cameraInside={IsDropPointInsideCameraX(dropPosForThisAttempt.x)} " +
                                $"xAcceptable={IsDropWorldXAcceptable(dropPosForThisAttempt.x)} dropX={dropPosForThisAttempt.x:0.###}). " +
                                "Spawning at current sample.");
                            break;
                        }

                        yield return null;
                        dropPosForThisAttempt = GetDropWorldPosition();
                    }

                    if (_performOneDrop == null || !_performOneDrop.Invoke(dropPosForThisAttempt))
                    {
                        CancelRemaining("carrier-drop-sequence-stopped");
                        yield break;
                    }

                    _droppedSoFar++;
                    if (_droppedSoFar < _dropCount && _dropIntervalSeconds > 0f)
                    {
                        yield return new WaitForSeconds(_dropIntervalSeconds);
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }
            finally
            {
                _isRoutineRunning = false;
            }
        }

        private bool HasReachedDropEntryTrigger()
        {
            if (_useNamedTriggerPoints && TryGetNamedTriggerX(out float triggerX))
            {
                float adjustedTriggerX = _fromLeft
                    ? triggerX - _dropTriggerLeadDistanceWorld
                    : triggerX + _dropTriggerLeadDistanceWorld;
                Vector3 helicopterPos = transform.position;
                Vector3 dropPos = GetDropWorldPosition();
                bool reachedTrigger = _fromLeft ? dropPos.x >= adjustedTriggerX : dropPos.x <= adjustedTriggerX;
                bool dropPointInsideCameraX = IsDropPointInsideCameraX(dropPos.x);
                bool reached = reachedTrigger && dropPointInsideCameraX && IsDropWorldXAcceptable(dropPos.x);
                if (reached && !_loggedTriggerReached)
                {
                    _loggedTriggerReached = true;
                    _hasTriggerDropPositionSnapshot = true;
                    _triggerDropPositionSnapshot = dropPos;
                    _debugLog?.Invoke(
                        $"[HelicopterCarrier_V2] Drop trigger reached side={(_fromLeft ? "LEFT" : "RIGHT")} " +
                        $"triggerX={adjustedTriggerX:0.###} dropBoneX={dropPos.x:0.###} deltaX={(dropPos.x - adjustedTriggerX):0.###} " +
                        $"helicopterX={helicopterPos.x:0.###} helicopterY={helicopterPos.y:0.###}");
                }

                return reached;
            }

            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return true;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float inset = Mathf.Min(Mathf.Max(0f, _cameraVisiblePaddingWorld), Mathf.Max(0f, Mathf.Min(halfWidth, halfHeight) - 0.01f));
            float minX = cam.transform.position.x - halfWidth + inset;
            float maxX = cam.transform.position.x + halfWidth - inset;
            float xPos = GetDropWorldPosition().x;
            bool inside = xPos >= minX && xPos <= maxX && IsDropWorldXAcceptable(xPos);
            if (inside && !_loggedTriggerReached)
            {
                _loggedTriggerReached = true;
                Vector3 helicopterPos = transform.position;
                _debugLog?.Invoke(
                    $"[HelicopterCarrier_V2] Camera-trigger reached minX={minX:0.###} maxX={maxX:0.###} dropBoneX={xPos:0.###} " +
                    $"helicopterX={helicopterPos.x:0.###} helicopterY={helicopterPos.y:0.###}");
            }

            return inside;
        }

        private float ResolveAcceptableDropWorldXWaitSeconds()
        {
            if (_isDropWorldXAcceptableForSpawn == null)
            {
                return 0f;
            }

            if (_maxSecondsWaitForAcceptableDropWorldX > 0f)
            {
                return _maxSecondsWaitForAcceptableDropWorldX;
            }

            return _maxWaitForTriggerSeconds;
        }

        private bool IsDropWorldXAcceptable(float worldX)
        {
            return _isDropWorldXAcceptableForSpawn == null || _isDropWorldXAcceptableForSpawn.Invoke(worldX);
        }

        private bool IsDropPointInsideCameraX(float dropX)
        {
            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return true;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float inset = Mathf.Min(Mathf.Max(0f, _cameraVisiblePaddingWorld), Mathf.Max(0f, Mathf.Min(halfWidth, halfHeight) - 0.01f));
            float minX = cam.transform.position.x - halfWidth + inset;
            float maxX = cam.transform.position.x + halfWidth - inset;
            return dropX >= minX && dropX <= maxX;
        }

        private Vector3 GetDropWorldPosition()
        {
            if (_getDropWorldPosition != null)
            {
                return _getDropWorldPosition.Invoke();
            }

            return transform.position;
        }

        private bool TryGetNamedTriggerX(out float triggerX)
        {
            triggerX = 0f;
            string triggerName = _fromLeft ? _leftTriggerName : _rightTriggerName;
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return false;
            }

            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude);
            Transform best = null;
            for (int i = 0; i < all.Length; i++)
            {
                Transform tr = all[i];
                if (tr == null || !tr.gameObject.name.Equals(triggerName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (best == null)
                {
                    best = tr;
                    continue;
                }

                if ((_fromLeft && tr.position.x < best.position.x) ||
                    (!_fromLeft && tr.position.x > best.position.x))
                {
                    best = tr;
                }
            }

            if (best == null)
            {
                return false;
            }

            triggerX = best.position.x;
            return true;
        }

        private void OnDisable()
        {
            // Pool despawn uses SetActive(false); OnDestroy is not called. Release planned drops so EnemySpawner_V2
            // pending counters and spawn targets stay consistent (see RegisterCancelledPlannedParatrooperDrop).
            CancelRemaining("aircraft-inactive-before-carrier-drop");
            _isRoutineRunning = false;
        }

        private void CancelRemaining(string reason)
        {
            if (!_initialized || _cancelRemainingDrops == null)
            {
                return;
            }

            int remaining = Mathf.Max(0, _dropCount - _droppedSoFar);
            if (remaining <= 0)
            {
                return;
            }

            _cancelRemainingDrops.Invoke(remaining, reason);
            _droppedSoFar = _dropCount;
        }
    }
}
