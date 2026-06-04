#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace iStick2War_V2.Editor
{
    /*
     * Editor menu: rebuild hero Spine bounding-box hitboxes (same Infantry slots as paratrooper).
     */
    public static class HeroBodyPartsEditor_V2
    {
        [MenuItem("iStick2War/Hero/Setup Hero Body Parts (Bounding Boxes)")]
        public static void SetupSelectedHeroBodyParts()
        {
            Hero_V2 hero = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<Hero_V2>()
                : null;

            if (hero == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab HERO V2");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    hero = prefabRoot.GetComponent<Hero_V2>();
                    if (hero != null && HeroBodyPartsFactory_V2.EnsureBodyPartsOnHero(hero, logWhenSkipped: true))
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    }

                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[HeroBodyPartsEditor_V2] Updated prefab at {path}.");
                    return;
                }

                EditorUtility.DisplayDialog(
                    "Hero body parts",
                    "Select a Hero_V2 in the scene or ensure Assets/Prefabs/Hero/HERO V2.prefab exists.",
                    "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(hero.gameObject, "Setup Hero Body Parts");
            HeroBodyPartsFactory_V2.EnsureBodyPartsOnHero(hero, logWhenSkipped: true);
            EditorUtility.SetDirty(hero);
        }
    }
}
#endif
