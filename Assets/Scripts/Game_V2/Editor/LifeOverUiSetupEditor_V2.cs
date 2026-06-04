#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace iStick2War_V2.Editor
{
    /*
     * Editor menu: create LifeOver-canvas + txt_lifeOver_* under LifeOver V2 (matches runtime factory).
     */
    public static class LifeOverUiSetupEditor_V2
    {
        [MenuItem("iStick2War/Game/Setup LifeOver UI Texts")]
        public static void SetupLifeOverUiTexts()
        {
            WaveManager_V2 waveManager = Object.FindFirstObjectByType<WaveManager_V2>(FindObjectsInactive.Include);
            string infoMessage = "You died. Press \"Start Game\" to try the wave again";
            if (waveManager != null)
            {
                SerializedObject so = new SerializedObject(waveManager);
                SerializedProperty messageProp = so.FindProperty("_lifeOverInfoMessage");
                if (messageProp != null && !string.IsNullOrWhiteSpace(messageProp.stringValue))
                {
                    infoMessage = messageProp.stringValue;
                }
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            LifeOverUiFactory_V2.EnsureLabelsExist(infoMessage, logWhenChanged: true);
            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.DisplayDialog(
                "LifeOver UI",
                "LifeOver-canvas + txt_lifeOver_info + txt_lifeOver_startNewGame ensured under LifeOver V2.\n\n" +
                "Save the scene and assign Life Over Root / text fields on WaveManager if needed.",
                "OK");
        }
    }
}
#endif
