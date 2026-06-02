#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace iStick2War_V2.Editor
{
    /*
     * Editor helper: builds Resources/iStick2War/AudioManagerClipDefaults_V2.asset
     * so runtime builds can load clips without AssetDatabase.
     */
    [InitializeOnLoad]
    public static class AudioManagerClipDefaultsEditor_V2
    {
        private const string DefaultsAssetPath = "Assets/Resources/iStick2War/AudioManagerClipDefaults_V2.asset";

        static AudioManagerClipDefaultsEditor_V2()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                RefreshDefaultsAsset(silent: true);
            }
        }

        [MenuItem("iStick2War/Audio/Refresh Audio Clip Defaults Asset")]
        public static void RefreshDefaultsAssetMenu()
        {
            RefreshDefaultsAsset(silent: false);
        }

        private static void RefreshDefaultsAsset(bool silent)
        {
            EnsureResourcesFolderExists();

            AudioManagerClipDefaults_V2 defaults =
                AssetDatabase.LoadAssetAtPath<AudioManagerClipDefaults_V2>(DefaultsAssetPath);
            if (defaults == null)
            {
                defaults = ScriptableObject.CreateInstance<AudioManagerClipDefaults_V2>();
                AssetDatabase.CreateAsset(defaults, DefaultsAssetPath);
            }

            defaults.bombPlaneLoop = LoadClip("Aircraft/bombplane.mp3");
            defaults.droneLoop = LoadClip("Aircraft/drone.wav");
            defaults.helicopterLoop = LoadClip("Aircraft/helicopter.mp3");

            defaults.grenadeExplosion = LoadClip("Explosions/grenadeExplosion.mp3");
            defaults.missileExplosion = LoadClip("Explosions/missileExplosion.wav");

            defaults.bulletHitDirt = LoadClip("Impact/bulletHitDirt.mp3");
            defaults.bulletHitFlesh = LoadClip("Impact/bulletHitFlesh.mp3");
            defaults.bulletHitMetal = LoadClip("Impact/bulletHitMetal2.mp3", "Impact/bulletHitMetal.mp3");

            defaults.clickMenu = LoadClip("Menu/clickMenu.wav");
            defaults.failure = LoadClip("Menu/failure.mp3");
            defaults.levelDone = LoadClip("Menu/levelDone.mp3");
            defaults.purchaseSuccess = LoadClip("Menu/purchaseSuccess2.mp3", "Menu/purchaseSuccess.mp3");
            defaults.settingsSuccess = LoadClip("Menu/settingsSuccess.mp3");

            defaults.bossMusic = LoadClip("Music/bossMusic2.mp3", "Music/bossMusic.mp3");
            defaults.gameMusic = LoadClip("Music/gameMusic.mp3");
            defaults.menuMusic = LoadClip("Music/menuMusic.mp3");

            defaults.bazookaShot = LoadClip("Weapons/bazooka.wav");
            defaults.flamethrowerShot = LoadClip("Weapons/flamethrower.wav");
            defaults.machineGunShot = LoadClip("Weapons/machineGun.wav");
            defaults.pistolShot = LoadClip("Weapons/pistol.wav");
            defaults.reloadGun = LoadClip("Weapons/reloadGun.mp3");
            defaults.reloadMachineGun = LoadClip("Weapons/reloadMachineGun.mp3");
            defaults.teslaShot = LoadClip("Weapons/teslaGun.mp3");

            EditorUtility.SetDirty(defaults);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                Debug.Log($"[AudioManagerClipDefaultsEditor_V2] Updated '{DefaultsAssetPath}'.");
            }
        }

        private static void EnsureResourcesFolderExists()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources/iStick2War"))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "iStick2War");
            }
        }

        private static AudioClip LoadClip(string primaryRelativePath, string alternateRelativePath = null)
        {
            AudioClip clip = LoadClipAtRelativePath(primaryRelativePath);
            if (clip != null || string.IsNullOrEmpty(alternateRelativePath))
            {
                return clip;
            }

            return LoadClipAtRelativePath(alternateRelativePath);
        }

        private static AudioClip LoadClipAtRelativePath(string relativePath)
        {
            string soundsPath = $"Assets/Sounds/Audio/{relativePath}";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(soundsPath);
            if (clip != null)
            {
                return clip;
            }

            string audioPath = $"Assets/Audio/{relativePath}";
            return AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
        }
    }
}
#endif
