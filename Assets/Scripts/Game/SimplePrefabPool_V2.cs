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
        private sealed class PoolTag : MonoBehaviour
        {
            public GameObject PrefabKey;
        }

        private static readonly Dictionary<GameObject, Stack<GameObject>> InactiveByPrefab =
            new Dictionary<GameObject, Stack<GameObject>>();

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

            instance.SetActive(false);
            stack.Push(instance);
        }
    }
}
