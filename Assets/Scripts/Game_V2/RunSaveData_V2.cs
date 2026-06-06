using System;

namespace iStick2War_V2
{
    /*
     * RunSaveData_V2 (Mid-run save snapshot DTO)
     *
     * PURPOSE:
     * JsonUtility-friendly snapshot of an in-progress run (wave index, economy, bunker, lives, loop phase,
     * shop carousel index, hero loadout). Written by RunSaveService_V2; restored by WaveManager_V2 on Continue.
     *
     * Mid-combat (InWave) saves restart the same wave with fresh spawns — enemy positions are not stored.
     *
     * formatVersion: bump when adding fields; add migration in RunSaveMigration_V2.cs.
     */
    [Serializable]
    public sealed class RunSaveFile_V2
    {
        public int formatVersion = RunSaveService_V2.CurrentFormatVersion;
        public string gameplaySceneName = "SampleScene";
        public int waveIndex;
        public int loopState;
        public int currency;
        public int bunkerHealth;
        public int bunkerMaxHealth;
        public int livesRemaining;
        public int healthPurchasesThisRun;
        public int bunkerRepairsThisRun;
        public int bunkerMaxUpgradesThisRun;
        public bool shopExitRetriesSameWave;
        public float continueEnemyPressureMultiplier;
        public int shopOfferIndex;
        public float restartRunPermanentDamageBonus01;
        public HeroSaveBlock_V2 hero = new HeroSaveBlock_V2();
        public long savedAtUtcTicks;
    }

    [Serializable]
    public sealed class HeroSaveBlock_V2
    {
        public int maxHealth;
        public int currentHealth;
        public bool isDead;
        public int activeWeaponType;
        public RunSaveWeaponEntryList_V2 weapons = new RunSaveWeaponEntryList_V2();
    }

    [Serializable]
    public sealed class RunSaveWeaponEntry_V2
    {
        public int weaponType;
        public int currentAmmo;
        public int currentReserveAmmo;
    }

    // JsonUtility cannot serialize List<T> at the root; use a wrapper array.
    [Serializable]
    public sealed class RunSaveWeaponEntryList_V2
    {
        public RunSaveWeaponEntry_V2[] items = Array.Empty<RunSaveWeaponEntry_V2>();
    }
}
