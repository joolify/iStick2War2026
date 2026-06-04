using UnityEngine;

namespace iStick2War_V2
{
    /*
     * AudioManagerClipDefaults_V2
     *
     * Serialized clip bundle loaded from Resources at runtime (works in builds).
     * Editor menu "iStick2War/Audio/Refresh Audio Clip Defaults Asset" populates this asset.
     */
    [CreateAssetMenu(fileName = "AudioManagerClipDefaults_V2", menuName = "iStick2War/Audio Manager Clip Defaults V2")]
    public sealed class AudioManagerClipDefaults_V2 : ScriptableObject
    {
        public const string ResourcesPath = "iStick2War/AudioManagerClipDefaults_V2";

        [Header("Aircraft")]
        public AudioClip bombPlaneLoop;
        public AudioClip droneLoop;
        public AudioClip helicopterLoop;

        [Header("Explosions")]
        public AudioClip grenadeExplosion;
        public AudioClip missileExplosion;

        [Header("Impacts")]
        public AudioClip bulletHitDirt;
        public AudioClip bulletHitFlesh;
        public AudioClip bulletHitMetal;

        [Header("Menu")]
        public AudioClip clickMenu;
        public AudioClip failure;
        public AudioClip levelDone;
        public AudioClip purchaseSuccess;
        public AudioClip settingsSuccess;

        [Header("Music")]
        public AudioClip bossMusic;
        public AudioClip gameMusic;
        public AudioClip menuMusic;

        [Header("Weapons")]
        public AudioClip bazookaShot;
        public AudioClip flamethrowerShot;
        public AudioClip machineGunShot;
        public AudioClip pistolShot;
        public AudioClip reloadGun;
        public AudioClip reloadMachineGun;
        public AudioClip teslaShot;
        public AudioClip outOfAmmo;
    }
}
