using System;
using System.IO;
using UnityEngine;

namespace iStick2War_V2
{
    /*
     * RunSaveService_V2 (Run snapshot file I/O)
     *
     * PURPOSE:
     * Persists one active run to Application.persistentDataPath/iStick2War_V2/run_save.json so the player can
     * quit mid-run and resume from the main menu. Settings use PlayerPrefs; this file is gameplay progress only.
     *
     * formatVersion upgrades → RunSaveMigration_V2.cs
     */
    public static class RunSaveService_V2
    {
        public const int CurrentFormatVersion = 2;
        private const string SaveFolderName = "iStick2War_V2";
        private const string SaveFileName = "run_save.json";

        public static bool HasSave()
        {
            return File.Exists(GetSaveFilePath());
        }

        public static bool TryLoad(out RunSaveFile_V2 save)
        {
            save = null;
            string path = GetSaveFilePath();
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                RunSaveFile_V2 parsed = JsonUtility.FromJson<RunSaveFile_V2>(json);
                if (parsed == null)
                {
                    return false;
                }

                int loadedVersion = parsed.formatVersion;
                if (!RunSaveMigration_V2.TryMigrateRunSave(ref parsed))
                {
                    return false;
                }

                save = parsed;

                // Rewrite migrated saves so the next load skips migration.
                if (loadedVersion < CurrentFormatVersion)
                {
                    TrySave(save);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunSaveService_V2] Failed to load save: {ex.Message}");
                return false;
            }
        }

        public static bool TrySave(RunSaveFile_V2 save)
        {
            if (save == null)
            {
                return false;
            }

            save.formatVersion = CurrentFormatVersion;
            save.savedAtUtcTicks = DateTime.UtcNow.Ticks;

            try
            {
                string folder = GetSaveFolderPath();
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string json = JsonUtility.ToJson(save, prettyPrint: true);
                File.WriteAllText(GetSaveFilePath(), json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunSaveService_V2] Failed to write save: {ex.Message}");
                return false;
            }
        }

        public static void ClearSave()
        {
            try
            {
                string path = GetSaveFilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RunSaveService_V2] Failed to delete save: {ex.Message}");
            }
        }

        public static string GetSaveFolderPath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFolderName);
        }

        public static string GetSaveFilePath()
        {
            return Path.Combine(GetSaveFolderPath(), SaveFileName);
        }
    }
}
