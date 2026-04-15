using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    public sealed class EnemySpawner_V2 : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private Paratrooper _paratrooperPrefab;

        [Header("Spawn Anchors (left/right)")]
        [SerializeField] private Transform[] _leftSpawnPoints;
        [SerializeField] private Transform[] _rightSpawnPoints;

        [Header("Fallback Random Area")]
        [SerializeField] private Vector2 _spawnXRange = new Vector2(-12f, 12f);
        [SerializeField] private Vector2 _spawnYRange = new Vector2(5f, 9f);
        [SerializeField] private bool _clampSpawnInsideCameraView = true;
        [SerializeField] private Camera _spawnCamera;
        [SerializeField] private float _cameraEdgePadding = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool _debugSpawnLogs = false;

        private readonly HashSet<ParatrooperDeathHandler_V2> _trackedDeaths = new HashSet<ParatrooperDeathHandler_V2>();
        private Coroutine _spawnRoutine;
        private Action _onEnemyKilled;
        private int _targetSpawnCount;
        private int _spawnedCount;
        private bool _spawnRoutineFinished;

        public void BeginWave(WaveConfig_V2 config, Action onEnemyKilled)
        {
            StopWave();
            if (config == null || _paratrooperPrefab == null)
            {
                return;
            }

            _onEnemyKilled = onEnemyKilled;
            _targetSpawnCount = Mathf.Max(0, config.EnemyCount);
            _spawnedCount = 0;
            _spawnRoutineFinished = false;
            _spawnRoutine = StartCoroutine(SpawnRoutine(config));
        }

        public void StopWave()
        {
            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            foreach (ParatrooperDeathHandler_V2 deathHandler in _trackedDeaths)
            {
                if (deathHandler != null)
                {
                    deathHandler.OnDeathStarted -= HandleTrackedEnemyDeath;
                }
            }
            _trackedDeaths.Clear();
            _onEnemyKilled = null;
            _targetSpawnCount = 0;
            _spawnedCount = 0;
            _spawnRoutineFinished = false;
        }

        private IEnumerator SpawnRoutine(WaveConfig_V2 config)
        {
            int toSpawn = config.EnemyCount;
            float interval = config.SpawnIntervalSeconds;
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnOne();
                if (i < toSpawn - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }

            _spawnRoutine = null;
            _spawnRoutineFinished = true;
        }

        private void SpawnOne()
        {
            Vector3 position = ResolveSpawnPosition();
            Paratrooper spawned = Instantiate(_paratrooperPrefab, position, Quaternion.identity);
            if (spawned == null)
            {
                return;
            }
            _spawnedCount++;

            ParatrooperDeathHandler_V2 deathHandler = spawned.GetComponent<ParatrooperDeathHandler_V2>();
            if (deathHandler == null)
            {
                deathHandler = spawned.GetComponentInChildren<ParatrooperDeathHandler_V2>(true);
            }
            if (deathHandler != null && _trackedDeaths.Add(deathHandler))
            {
                deathHandler.OnDeathStarted += HandleTrackedEnemyDeath;
            }

            if (_debugSpawnLogs)
            {
                Debug.Log($"[EnemySpawner_V2] Spawned Paratrooper at {position}");
            }
        }

        private void HandleTrackedEnemyDeath(ParatrooperDeathHandler_V2 deathHandler)
        {
            if (deathHandler != null)
            {
                deathHandler.OnDeathStarted -= HandleTrackedEnemyDeath;
                _trackedDeaths.Remove(deathHandler);
            }

            _onEnemyKilled?.Invoke();
        }

        public bool IsWaveCleared()
        {
            PruneInactiveTrackedDeaths();
            bool allSpawned = _spawnRoutineFinished || _spawnedCount >= _targetSpawnCount;
            return allSpawned && _trackedDeaths.Count == 0;
        }

        private void PruneInactiveTrackedDeaths()
        {
            if (_trackedDeaths.Count == 0)
            {
                return;
            }

            List<ParatrooperDeathHandler_V2> stale = null;
            foreach (ParatrooperDeathHandler_V2 deathHandler in _trackedDeaths)
            {
                if (deathHandler == null || !deathHandler.gameObject.activeInHierarchy)
                {
                    if (stale == null)
                    {
                        stale = new List<ParatrooperDeathHandler_V2>();
                    }
                    stale.Add(deathHandler);
                }
            }

            if (stale == null)
            {
                return;
            }

            for (int i = 0; i < stale.Count; i++)
            {
                ParatrooperDeathHandler_V2 deathHandler = stale[i];
                if (deathHandler != null)
                {
                    deathHandler.OnDeathStarted -= HandleTrackedEnemyDeath;
                }
                _trackedDeaths.Remove(deathHandler);
            }
        }

        private Vector3 ResolveSpawnPosition()
        {
            bool fromLeft = UnityEngine.Random.value < 0.5f;
            Transform[] candidates = fromLeft ? _leftSpawnPoints : _rightSpawnPoints;
            if (candidates != null && candidates.Length > 0)
            {
                int idx = UnityEngine.Random.Range(0, candidates.Length);
                if (candidates[idx] != null)
                {
                    return ClampToCameraView(candidates[idx].position);
                }
            }

            Vector3 fallbackPosition = new Vector3(
                UnityEngine.Random.Range(_spawnXRange.x, _spawnXRange.y),
                UnityEngine.Random.Range(_spawnYRange.x, _spawnYRange.y),
                0f);
            return ClampToCameraView(fallbackPosition);
        }

        private Vector3 ClampToCameraView(Vector3 worldPosition)
        {
            if (!_clampSpawnInsideCameraView)
            {
                return worldPosition;
            }

            Camera cam = _spawnCamera != null ? _spawnCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return worldPosition;
            }

            float pad = Mathf.Max(0f, _cameraEdgePadding);
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            float minX = camPos.x - halfWidth + pad;
            float maxX = camPos.x + halfWidth - pad;
            float minY = camPos.y - halfHeight + pad;
            float maxY = camPos.y + halfHeight - pad;

            worldPosition.x = Mathf.Clamp(worldPosition.x, minX, maxX);
            worldPosition.y = Mathf.Clamp(worldPosition.y, minY, maxY);
            worldPosition.z = 0f;
            return worldPosition;
        }
    }
}
