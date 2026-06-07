#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace iStick2War_V2.Editor
{
    /*
     * Editor menu: assign WaveManager_V2._waves from Assets/Data/WaveManager/Waves01to15 (Wave01..Wave15 assets).
     */
    public static class WaveManagerWaveListSyncEditor_V2
    {
        private const string WavesFolder = "Assets/Data/WaveManager/Waves01to15";

        [MenuItem("iStick2War/Game/Sync WaveManager Wave List (15 waves)")]
        public static void SyncWaveManagerWaveList()
        {
            WaveConfig_V2[] waveAssets = LoadSortedWaveAssets();
            if (waveAssets.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Sync Wave List",
                    $"No WaveConfig_V2 assets found under '{WavesFolder}'.",
                    "OK");
                return;
            }

            WaveManager_V2 waveManager = UnityEngine.Object.FindFirstObjectByType<WaveManager_V2>(FindObjectsInactive.Include);
            if (waveManager == null)
            {
                EditorUtility.DisplayDialog(
                    "Sync Wave List",
                    "WaveManager_V2 not found in loaded scenes.",
                    "OK");
                return;
            }

            SerializedObject so = new SerializedObject(waveManager);
            SerializedProperty wavesProp = so.FindProperty("_waves");
            if (wavesProp == null || !wavesProp.isArray)
            {
                EditorUtility.DisplayDialog(
                    "Sync Wave List",
                    "WaveManager_V2._waves serialized field not found.",
                    "OK");
                return;
            }

            Undo.RecordObject(waveManager, "Sync WaveManager wave list");
            wavesProp.arraySize = waveAssets.Length;
            for (int i = 0; i < waveAssets.Length; i++)
            {
                wavesProp.GetArrayElementAtIndex(i).objectReferenceValue = waveAssets[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(waveManager);
            MarkActiveSceneDirty();

            EditorUtility.DisplayDialog(
                "Sync Wave List",
                $"Assigned {waveAssets.Length} wave configs to WaveManager_V2:\n" +
                string.Join("\n", BuildWaveSummaryLines(waveAssets)) +
                "\n\nSave the scene to persist.",
                "OK");
        }

        private static WaveConfig_V2[] LoadSortedWaveAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:WaveConfig_V2", new[] { WavesFolder });
            var waves = new List<(int number, WaveConfig_V2 config)>(guids.Length);
            Regex waveNumberPattern = new Regex(@"^Wave(\d+)_", RegexOptions.CultureInvariant);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                WaveConfig_V2 config = AssetDatabase.LoadAssetAtPath<WaveConfig_V2>(path);
                if (config == null)
                {
                    continue;
                }

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                Match match = waveNumberPattern.Match(fileName);
                if (!match.Success ||
                    !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int waveNumber))
                {
                    Debug.LogWarning($"[WaveManagerWaveListSyncEditor_V2] Skipping '{path}' (expected name like Wave01_EarlyAir).");
                    continue;
                }

                waves.Add((waveNumber, config));
            }

            waves.Sort((a, b) => a.number.CompareTo(b.number));
            var result = new WaveConfig_V2[waves.Count];
            for (int i = 0; i < waves.Count; i++)
            {
                result[i] = waves[i].config;
            }

            return result;
        }

        private static string[] BuildWaveSummaryLines(WaveConfig_V2[] waveAssets)
        {
            var lines = new string[waveAssets.Length];
            for (int i = 0; i < waveAssets.Length; i++)
            {
                WaveConfig_V2 wave = waveAssets[i];
                lines[i] = $"  {(i + 1).ToString(CultureInfo.InvariantCulture)}. {wave.name}";
            }

            return lines;
        }

        private static void MarkActiveSceneDirty()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
#endif
