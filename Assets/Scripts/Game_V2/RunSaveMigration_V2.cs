using UnityEngine;

namespace iStick2War_V2
{
    /*
     * RunSaveMigration_V2 (run_save.json format upgrades)
     *
     * PURPOSE:
     * Upgrades older RunSaveFile_V2 snapshots when formatVersion is below RunSaveService_V2.CurrentFormatVersion.
     * Add a new MigrateFromNToNPlus1 step whenever fields are added or semantics change — never bump
     * CurrentFormatVersion without a migration path unless breaking old saves is intentional.
     */
    public static class RunSaveMigration_V2
    {
        // Returns false when the save is from a newer build or no migration path exists.
        public static bool TryMigrateRunSave(ref RunSaveFile_V2 save)
        {
            if (save == null)
            {
                return false;
            }

            if (save.formatVersion > RunSaveService_V2.CurrentFormatVersion)
            {
                Debug.LogWarning(
                    $"[RunSaveMigration_V2] Save formatVersion {save.formatVersion} is newer than " +
                    $"supported {RunSaveService_V2.CurrentFormatVersion}.");
                return false;
            }

            while (save.formatVersion < RunSaveService_V2.CurrentFormatVersion)
            {
                int before = save.formatVersion;
                if (!TryMigrateOneStep(ref save))
                {
                    Debug.LogWarning(
                        $"[RunSaveMigration_V2] No migration from formatVersion {before} to " +
                        $"{RunSaveService_V2.CurrentFormatVersion}.");
                    return false;
                }

                if (save.formatVersion <= before)
                {
                    Debug.LogWarning(
                        "[RunSaveMigration_V2] Migration step did not advance formatVersion — aborting.");
                    return false;
                }
            }

            return true;
        }

        private static bool TryMigrateOneStep(ref RunSaveFile_V2 save)
        {
            switch (save.formatVersion)
            {
                case 0:
                    MigrateFrom0To1(ref save);
                    return true;
                // case 1:
                //     MigrateFrom1To2(ref save);
                //     return true;
                default:
                    return save.formatVersion >= RunSaveService_V2.CurrentFormatVersion;
            }
        }

        // Pre-versioned or hand-edited JSON may deserialize with formatVersion 0.
        private static void MigrateFrom0To1(ref RunSaveFile_V2 save)
        {
            if (string.IsNullOrWhiteSpace(save.gameplaySceneName))
            {
                save.gameplaySceneName = "SampleScene";
            }

            if (save.hero == null)
            {
                save.hero = new HeroSaveBlock_V2();
            }

            if (save.hero.weapons == null)
            {
                save.hero.weapons = new RunSaveWeaponEntryList_V2();
            }

            save.formatVersion = 1;
        }

        // Example stub for the next format bump — uncomment when CurrentFormatVersion becomes 2.
        // private static void MigrateFrom1To2(ref RunSaveFile_V2 save)
        // {
        //     // e.g. save.newField = defaultValue;
        //     save.formatVersion = 2;
        // }
    }
}
