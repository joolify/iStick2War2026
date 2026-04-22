using System.Collections.Generic;
using UnityEngine;

namespace iStick2War_V2
{
    /// <summary>
    /// Minimal prefab pool for long-running automation sessions.
    /// Falls back to Destroy when an instance was not spawned via this pool.
    /// </summary>
    public static class SimplePrefabPool_V2
    {
        [System.Serializable]
        public sealed class PoolPrefabStats
        {
            public string prefabName;
            public int inactiveCount;
            public int createdCount;
            public int reusedCount;
            public int despawnCount;
        }

        [System.Serializable]
        public sealed class PoolStatsSnapshot
        {
            public int prefabTypeCount;
            public int totalInactiveCount;
            public int totalCreatedCount;
            public int totalReusedCount;
            public int totalDespawnCount;
            public PoolPrefabStats[] prefabs;
        }

        private sealed class PoolTag : MonoBehaviour
        {
            public GameObject PrefabKey;
        }

        private sealed class PoolCounters
        {
            public int createdCount;
            public int reusedCount;
            public int despawnCount;
        }

        private static readonly Dictionary<GameObject, Stack<GameObject>> InactiveByPrefab =
            new Dictionary<GameObject, Stack<GameObject>>();
        private static readonly Dictionary<GameObject, PoolCounters> CountersByPrefab =
            new Dictionary<GameObject, PoolCounters>();

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Component
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject go = Spawn(prefab.gameObject, position, rotation, parent);
            return go != null ? go.GetComponent<T>() : null;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!InactiveByPrefab.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(32);
                InactiveByPrefab[prefab] = stack;
            }

            if (!CountersByPrefab.TryGetValue(prefab, out PoolCounters counters))
            {
                counters = new PoolCounters();
                CountersByPrefab[prefab] = counters;
            }

            GameObject instance = null;
            while (stack.Count > 0 && instance == null)
            {
                instance = stack.Pop();
            }

            if (instance == null)
            {
                instance = Object.Instantiate(prefab);
                PoolTag tag = instance.GetComponent<PoolTag>();
                if (tag == null)
                {
                    tag = instance.AddComponent<PoolTag>();
                }

                tag.PrefabKey = prefab;
                counters.createdCount++;
            }
            else
            {
                counters.reusedCount++;
            }

            Transform t = instance.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            PoolTag tag = instance.GetComponent<PoolTag>();
            if (tag == null || tag.PrefabKey == null)
            {
                Object.Destroy(instance);
                return;
            }

            if (!InactiveByPrefab.TryGetValue(tag.PrefabKey, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>(32);
                InactiveByPrefab[tag.PrefabKey] = stack;
            }

            if (!CountersByPrefab.TryGetValue(tag.PrefabKey, out PoolCounters counters))
            {
                counters = new PoolCounters();
                CountersByPrefab[tag.PrefabKey] = counters;
            }

            instance.SetActive(false);
            stack.Push(instance);
            counters.despawnCount++;
        }

        public static PoolStatsSnapshot GetSnapshot()
        {
            var prefabs = new List<PoolPrefabStats>(InactiveByPrefab.Count);
            int totalInactive = 0;
            int totalCreated = 0;
            int totalReused = 0;
            int totalDespawns = 0;

            foreach (KeyValuePair<GameObject, Stack<GameObject>> kv in InactiveByPrefab)
            {
                GameObject prefab = kv.Key;
                Stack<GameObject> stack = kv.Value;
                int inactiveCount = 0;
                if (stack != null)
                {
                    foreach (GameObject go in stack)
                    {
                        if (go != null)
                        {
                            inactiveCount++;
                        }
                    }
                }

                CountersByPrefab.TryGetValue(prefab, out PoolCounters counters);
                int created = counters != null ? counters.createdCount : 0;
                int reused = counters != null ? counters.reusedCount : 0;
                int despawned = counters != null ? counters.despawnCount : 0;

                totalInactive += inactiveCount;
                totalCreated += created;
                totalReused += reused;
                totalDespawns += despawned;

                prefabs.Add(new PoolPrefabStats
                {
                    prefabName = prefab != null ? prefab.name : "(null)",
                    inactiveCount = inactiveCount,
                    createdCount = created,
                    reusedCount = reused,
                    despawnCount = despawned
                });
            }

            return new PoolStatsSnapshot
            {
                prefabTypeCount = prefabs.Count,
                totalInactiveCount = totalInactive,
                totalCreatedCount = totalCreated,
                totalReusedCount = totalReused,
                totalDespawnCount = totalDespawns,
                prefabs = prefabs.ToArray()
            };
        }
    }
}
