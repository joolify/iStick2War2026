using UnityEngine;

namespace iStick2War.Tests.PlayMode
{
    /// <summary>
    /// Resolves scene objects for Play Mode tests. Prefer over <see cref="Object.FindFirstObjectByType{T}"/>
    /// default behaviour, which skips inactive hierarchies and can miss valid setup.
    /// </summary>
    internal static class PlayModeSceneObjectLookup
    {
        public static T FindAnyInLoadedScenes<T>()
            where T : Object
        {
            T[] found = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return found != null && found.Length > 0 ? found[0] : null;
        }
    }
}
