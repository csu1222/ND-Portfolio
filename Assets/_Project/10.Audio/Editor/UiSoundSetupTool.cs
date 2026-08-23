using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ND.Audio.Editor
{
    public static class UiSoundSetupTool
    {
        private const string MenuPath = "Tools/Audio/Apply Default UI Sound To Selection";

        [MenuItem(MenuPath, true)]
        private static bool ValidateApply()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(MenuPath)]
        private static void Apply()
        {
            GameObject[] roots = Selection.gameObjects;
            CountSelection(roots, out int found, out int configured);
            int pending = found - configured;
            if (!EditorUtility.DisplayDialog(
                    "Apply Default UI Sound",
                    $"Buttons Found: {found}\nAlready Configured: {configured}\nUIButtonSound Added: {pending}",
                    "Apply",
                    "Cancel"))
                return;

            int added = ApplySelection(roots);
            Debug.Log($"[UI Sound Setup] Buttons Found: {found}, Already Configured: {configured}, UIButtonSound Added: {added}");
        }

        private static void CountSelection(GameObject[] roots, out int found, out int configured)
        {
            found = 0;
            configured = 0;
            var sceneButtons = new HashSet<EntityId>();
            var prefabPaths = new HashSet<string>();

            foreach (GameObject root in roots)
            {
                if (root == null) continue;
                string path = AssetDatabase.GetAssetPath(root);
                if (!string.IsNullOrEmpty(path))
                {
                    if (!prefabPaths.Add(path)) continue;
                    GameObject contents = PrefabUtility.LoadPrefabContents(path);
                    CountButtons(contents, null, ref found, ref configured);
                    PrefabUtility.UnloadPrefabContents(contents);
                    continue;
                }

                CountButtons(root, sceneButtons, ref found, ref configured);
            }
        }

        private static int ApplySelection(GameObject[] roots)
        {
            int added = 0;
            var sceneButtons = new HashSet<EntityId>();
            var prefabPaths = new HashSet<string>();

            foreach (GameObject root in roots)
            {
                if (root == null) continue;
                string path = AssetDatabase.GetAssetPath(root);
                if (!string.IsNullOrEmpty(path))
                {
                    if (!prefabPaths.Add(path)) continue;
                    GameObject contents = PrefabUtility.LoadPrefabContents(path);
                    added += AddMissing(contents, null, false);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    PrefabUtility.UnloadPrefabContents(contents);
                    continue;
                }

                added += AddMissing(root, sceneButtons, true);
                if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);
            }

            return added;
        }

        private static void CountButtons(GameObject root, HashSet<EntityId> seen, ref int found, ref int configured)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (seen != null && !seen.Add(button.GetEntityId())) continue;
                found++;
                if (button.GetComponent<UIButtonSound>() != null) configured++;
            }
        }

        private static int AddMissing(GameObject root, HashSet<EntityId> seen, bool recordUndo)
        {
            int added = 0;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (seen != null && !seen.Add(button.GetEntityId())) continue;
                if (button.GetComponent<UIButtonSound>() != null) continue;

                UIButtonSound sound = recordUndo
                    ? Undo.AddComponent<UIButtonSound>(button.gameObject)
                    : button.gameObject.AddComponent<UIButtonSound>();
                var serialized = new SerializedObject(sound);
                serialized.FindProperty("useDefaultSound").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sound);
                added++;
            }

            return added;
        }
    }
}
