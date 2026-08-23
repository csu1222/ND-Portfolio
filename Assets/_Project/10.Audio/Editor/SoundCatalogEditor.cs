using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ND.Audio.Editor
{
    [CustomEditor(typeof(SoundCatalog))]
    public sealed class SoundCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("definitions"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneBgmMappings"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI Defaults", EditorStyles.boldLabel);
            SoundCatalogEditorUtility.DrawSoundIdPopup(
                serializedObject,
                serializedObject.FindProperty("uiSounds.defaultButtonSoundId"),
                "Default Button Sound",
                SoundCategory.UiSfx);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Trade SFX", EditorStyles.boldLabel);
            SoundCatalogEditorUtility.DrawSoundIdPopup(serializedObject, serializedObject.FindProperty("tradeSounds.departSoundId"), "Depart", SoundCategory.Sfx);
            SoundCatalogEditorUtility.DrawSoundIdPopup(serializedObject, serializedObject.FindProperty("tradeSounds.settlementReadySoundId"), "Settlement Ready", SoundCategory.Sfx);
            SoundCatalogEditorUtility.DrawSoundIdPopup(serializedObject, serializedObject.FindProperty("tradeSounds.claimSoundId"), "Claim", SoundCategory.Sfx);
            SoundCatalogEditorUtility.DrawSoundIdPopup(serializedObject, serializedObject.FindProperty("tradeSounds.failedSoundId"), "Failed", SoundCategory.Sfx);

            serializedObject.ApplyModifiedProperties();
        }
    }

    internal static class SoundCatalogEditorUtility
    {
        public static void DrawSoundIdPopup(
            SerializedObject catalogObject,
            SerializedProperty idProperty,
            string label,
            SoundCategory category)
        {
            if (catalogObject == null || idProperty == null) return;

            List<string> ids = GetIds(catalogObject, category);
            string current = idProperty.stringValue ?? string.Empty;
            int currentIndex = string.IsNullOrEmpty(current) ? 0 : ids.IndexOf(current) + 1;
            var options = new List<string> { "(None)" };
            options.AddRange(ids);

            if (!string.IsNullOrEmpty(current) && currentIndex == 0)
            {
                options.Add($"Missing: {current}");
                currentIndex = options.Count - 1;
            }

            int selected = EditorGUILayout.Popup(label, currentIndex, options.ToArray());
            if (selected != currentIndex)
                idProperty.stringValue = selected == 0 ? string.Empty : ids[selected - 1];

            if (!string.IsNullOrEmpty(current) && !ids.Contains(current))
                EditorGUILayout.HelpBox($"Missing {category} Sound ID: {current}", MessageType.Warning);
        }

        public static SerializedObject LoadCatalogObject()
        {
            SoundCatalog catalog = Resources.Load<SoundCatalog>(SoundCatalog.ResourceName);
            return catalog != null ? new SerializedObject(catalog) : null;
        }

        private static List<string> GetIds(SerializedObject catalogObject, SoundCategory category)
        {
            var ids = new List<string>();
            SerializedProperty definitions = catalogObject.FindProperty("definitions");
            if (definitions == null || !definitions.isArray) return ids;

            for (int index = 0; index < definitions.arraySize; index++)
            {
                SerializedProperty definition = definitions.GetArrayElementAtIndex(index);
                SerializedProperty id = definition.FindPropertyRelative("id");
                SerializedProperty definitionCategory = definition.FindPropertyRelative("category");
                if (id == null || definitionCategory == null || string.IsNullOrEmpty(id.stringValue)) continue;
                if (definitionCategory.enumValueIndex == (int)category && !ids.Contains(id.stringValue))
                    ids.Add(id.stringValue);
            }

            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }
    }
}
