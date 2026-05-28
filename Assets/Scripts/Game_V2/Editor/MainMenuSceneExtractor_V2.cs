using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace iStick2War_V2.Editor
{
    /*
     * MainMenuSceneExtractor_V2 (one-click scene split helper)
     *
     * PURPOSE:
     * Moves MainMenu-canvas + MainMenu V2 out of SampleScene into a dedicated MainMenuScene.unity,
     * rewires MainMenuNavButton_V2 references to the copied MainMenu_V2, and removes the originals
     * from SampleScene to keep gameplay scene clean.
     */
    internal static class MainMenuSceneExtractor_V2
    {
        private const string SourceScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TargetScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string MainMenuCanvasName = "MainMenu-canvas";
        private const string MainMenuRootName = "MainMenu V2";
        private const string MenuPath = "Tools/iStick2War/Extract MainMenu To Own Scene";

        [MenuItem(MenuPath)]
        private static void Extract()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            GameObject sourceCanvas = FindRootByName(sourceScene, MainMenuCanvasName);
            GameObject sourceMainMenu = FindRootByName(sourceScene, MainMenuRootName);
            if (sourceCanvas == null || sourceMainMenu == null)
            {
                Debug.LogError(
                    $"[MainMenuSceneExtractor_V2] Could not find '{MainMenuCanvasName}' and '{MainMenuRootName}' in {SourceScenePath}.");
                return;
            }

            Scene targetScene = OpenOrCreateTargetScene();
            ClearSceneRoots(targetScene);

            GameObject targetCanvas = Object.Instantiate(sourceCanvas);
            targetCanvas.name = sourceCanvas.name;
            SceneManager.MoveGameObjectToScene(targetCanvas, targetScene);

            GameObject targetMainMenuGo = Object.Instantiate(sourceMainMenu);
            targetMainMenuGo.name = sourceMainMenu.name;
            SceneManager.MoveGameObjectToScene(targetMainMenuGo, targetScene);

            EnsureMainCamera(targetScene);
            EnsureEventSystem(targetScene);
            RebindMainMenuReferences(targetScene, targetMainMenuGo, targetCanvas);

            Object.DestroyImmediate(sourceCanvas);
            Object.DestroyImmediate(sourceMainMenu);

            EditorSceneManager.SaveScene(targetScene, TargetScenePath);
            EditorSceneManager.SetActiveScene(sourceScene);
            EditorSceneManager.SaveScene(sourceScene, SourceScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[MainMenuSceneExtractor_V2] Done. Main menu moved to Assets/Scenes/MainMenuScene.unity and removed from SampleScene.");
        }

        private static Scene OpenOrCreateTargetScene()
        {
            if (File.Exists(Path.GetFullPath(TargetScenePath)))
            {
                return EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Additive);
            }

            Scene created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(created, TargetScenePath);
            return created;
        }

        private static void ClearSceneRoots(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                {
                    Object.DestroyImmediate(roots[i]);
                }
            }
        }

        private static GameObject FindRootByName(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject go = roots[i];
                if (go != null && go.name == name)
                {
                    return go;
                }
            }

            return null;
        }

        private static void EnsureMainCamera(Scene scene)
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].gameObject.scene == scene)
                {
                    return;
                }
            }

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            SceneManager.MoveGameObjectToScene(camGo, scene);
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem[] systems =
                Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && systems[i].gameObject.scene == scene)
                {
                    return;
                }
            }

            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            SceneManager.MoveGameObjectToScene(es, scene);
        }

        private static void RebindMainMenuReferences(Scene targetScene, GameObject targetMainMenuGo, GameObject targetCanvas)
        {
            MainMenu_V2 mainMenu = targetMainMenuGo != null ? targetMainMenuGo.GetComponent<MainMenu_V2>() : null;
            if (mainMenu == null)
            {
                return;
            }

            SerializedObject menuSo = new SerializedObject(mainMenu);

            SerializedProperty hideOnPlay = menuSo.FindProperty("_hideOnPlay");
            if (hideOnPlay != null)
            {
                hideOnPlay.arraySize = 1;
                hideOnPlay.GetArrayElementAtIndex(0).objectReferenceValue = targetCanvas;
            }

            SerializedProperty waveManager = menuSo.FindProperty("_waveManager");
            if (waveManager != null)
            {
                waveManager.objectReferenceValue = null;
            }

            SerializedProperty loadGameplayScene = menuSo.FindProperty("_loadGameplaySceneOnPlay");
            if (loadGameplayScene != null)
            {
                loadGameplayScene.boolValue = true;
            }

            SerializedProperty gameplaySceneName = menuSo.FindProperty("_gameplaySceneName");
            if (gameplaySceneName != null)
            {
                gameplaySceneName.stringValue = "SampleScene";
            }

            menuSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mainMenu);

            MainMenuNavButton_V2[] navButtons =
                Object.FindObjectsByType<MainMenuNavButton_V2>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < navButtons.Length; i++)
            {
                MainMenuNavButton_V2 nav = navButtons[i];
                if (nav == null || nav.gameObject.scene != targetScene)
                {
                    continue;
                }

                SerializedObject navSo = new SerializedObject(nav);
                SerializedProperty mainMenuProp = navSo.FindProperty("_mainMenu");
                if (mainMenuProp != null)
                {
                    mainMenuProp.objectReferenceValue = mainMenu;
                }

                navSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(nav);
            }
        }
    }
}
