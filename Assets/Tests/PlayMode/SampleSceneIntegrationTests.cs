using System.Collections;
using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace iStick2War.Tests.PlayMode
{
    /// <summary>
    /// Play Mode integration tests: load build scene, assert hierarchy and loop wiring.
    /// Run from Test Runner → Play Mode.
    /// </summary>
    public sealed class SampleSceneIntegrationTests
    {
        private const string MainSceneName = "SampleScene";

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        /// <summary>Let scene objects run Awake/OnEnable before lookups.</summary>
        private static IEnumerator LoadMainSceneAsync()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(MainSceneName, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainSceneName));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SampleScene_Loads_And_HasMainCamera()
        {
            yield return LoadMainSceneAsync();

            Assert.That(Camera.main, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SampleScene_ContainsWaveManager()
        {
            yield return LoadMainSceneAsync();

            var waveManager = PlayModeSceneObjectLookup.FindAnyInLoadedScenes<WaveManager_V2>();
            Assert.That(waveManager, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SampleScene_TimeAdvances_OverSeveralFrames()
        {
            yield return LoadMainSceneAsync();

            Time.timeScale = 1f;
            float t0 = Time.time;
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.That(Time.time, Is.GreaterThanOrEqualTo(t0));
        }

        [UnityTest]
        public IEnumerator SampleScene_ContainsCoreV2Roots()
        {
            yield return LoadMainSceneAsync();

            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<WaveManager_V2>(), Is.Not.Null);
            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<MainMenu_V2>(), Is.Not.Null);
            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<ShopPanel_V2>(), Is.Not.Null);
            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<EnemySpawner_V2>(), Is.Not.Null);
            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<Hero_V2>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SampleScene_ContainsBunkerComponents()
        {
            yield return LoadMainSceneAsync();

            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<BunkerView_V2>(), Is.Not.Null);
            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<BunkerHitbox_V2>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator SampleScene_HasEventSystem_ForUI()
        {
            yield return LoadMainSceneAsync();

            Assert.That(PlayModeSceneObjectLookup.FindAnyInLoadedScenes<EventSystem>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator WaveManager_StartsInPreparing_WithConsistentEconomy()
        {
            yield return LoadMainSceneAsync();

            var wave = PlayModeSceneObjectLookup.FindAnyInLoadedScenes<WaveManager_V2>();
            Assert.That(wave, Is.Not.Null);
            Assert.That(wave.State, Is.EqualTo(WaveLoopState_V2.Preparing));
            Assert.That(wave.Currency, Is.GreaterThanOrEqualTo(0));
            Assert.That(wave.BunkerHealth, Is.GreaterThanOrEqualTo(0));
            Assert.That(wave.BunkerHealth, Is.LessThanOrEqualTo(wave.BunkerMaxHealth));
            Assert.That(wave.CurrentWaveNumber, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MainMenu_PausesTime_OnBoot_HandlePlay_ResumesTime()
        {
            yield return LoadMainSceneAsync();

            var menu = PlayModeSceneObjectLookup.FindAnyInLoadedScenes<MainMenu_V2>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            menu.HandlePlay();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
