using System;
using System.Collections;
using System.IO;
using System.Text;
using iStick2War_V2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace iStick2War.Tests.PlayMode
{
    /// <summary>
    /// Regression integration: weapon × enemy matrix via <see cref="CombatMatrixIntegrationTestRunner_V2"/>.
    /// Loads a build scene dedicated to the combat matrix (never SampleScene). The scene must be listed in
    /// File → Build Settings — e.g. <c>Assets/Scenes/CombatMatrixIntegrationTest_V2.unity</c>.
    /// </summary>
    public sealed class CombatMatrixIntegrationTest_V2
    {
        private const string MatrixSceneFileName = "CombatMatrixIntegrationTest_V2.unity";
        private const string MatrixSceneAssetPathTests = "Assets/Scenes/Tests/CombatMatrixIntegrationTest_V2.unity";
        private const string MatrixSceneAssetPathScenes = "Assets/Scenes/CombatMatrixIntegrationTest_V2.unity";

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

        /// <summary>
        /// Prefers the canonical path, then any enabled build scene whose path/filename suggests the combat matrix scene.
        /// Does not fall back to SampleScene (avoids opening the full game from Test Runner).
        /// </summary>
        private static int ResolveCombatMatrixBuildIndex()
        {
            int byPath = SceneUtility.GetBuildIndexByScenePath(MatrixSceneAssetPathTests);
            if (byPath >= 0)
            {
                return byPath;
            }

            byPath = SceneUtility.GetBuildIndexByScenePath(MatrixSceneAssetPathScenes);
            if (byPath >= 0)
            {
                return byPath;
            }

            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string normalized = path.Replace('\\', '/');
                string fileName = Path.GetFileName(normalized);
                if (string.Equals(fileName, MatrixSceneFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }

                bool inTestsFolder = normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0;
                if (inTestsFolder &&
                    fileName.IndexOf("CombatMatrix", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }

            return -1;
        }

        [UnityTest]
        [Category("CombatMatrix")]
        public IEnumerator CombatMatrix_RunnerCompletes_WhenSceneConfigured()
        {
            int buildIndex = ResolveCombatMatrixBuildIndex();
            if (buildIndex < 0)
            {
                Assert.Ignore(
                    "No combat matrix scene in Build Settings. Add and enable " +
                    MatrixSceneAssetPathScenes +
                    " (or " + MatrixSceneAssetPathTests + ") " +
                    "in File → Build Settings, then run again. " +
                    "This test does not load SampleScene.");
                yield break;
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;

            CombatMatrixIntegrationTestRunner_V2 runner =
                PlayModeSceneObjectLookup.FindAnyInLoadedScenes<CombatMatrixIntegrationTestRunner_V2>();
            if (runner == null)
            {
                Assert.Ignore(
                    "No CombatMatrixIntegrationTestRunner_V2 in the loaded scene. Add the component to your combat matrix " +
                    "test scene (or SampleScene for local runs) and assign hero, spawn point, prefabs, and cases.");
                yield break;
            }

            if (!runner.IsReadyForAutomatedRun())
            {
                Assert.Ignore(
                    "Combat matrix runner is not fully configured (hero, view, input, spawn, prefabs, cases). " +
                    "Complete inspector wiring on CombatMatrixIntegrationTestRunner_V2.");
                yield break;
            }

            yield return runner.RunMatrixAndExportRoutine();

            Assert.That(runner.LastRunAllPassed, Is.True, BuildFailureMessage(runner));
        }

        private static string BuildFailureMessage(CombatMatrixIntegrationTestRunner_V2 runner)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Combat matrix failures:");
            for (int i = 0; i < runner.LastResults.Count; i++)
            {
                CombatMatrixRowResult r = runner.LastResults[i];
                if (r.result != "PASS" && r.result != "PASS_DAMAGE_ONLY")
                {
                    sb.Append(" - ").Append(r.weapon).Append(" vs ").Append(r.enemy).Append(": ").AppendLine(r.result);
                }
            }

            return sb.ToString();
        }
    }
}
