using UnityEditor;
using UnityEngine;

namespace ND.Audio.Editor
{
    [CustomEditor(typeof(UIButtonSound))]
    public sealed class UIButtonSoundEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty useDefaultSound = serializedObject.FindProperty("useDefaultSound");
            SerializedProperty soundId = serializedObject.FindProperty("soundId");

            EditorGUILayout.PropertyField(useDefaultSound, new GUIContent("Use Default UI Sound"));
            SerializedObject catalogObject = SoundCatalogEditorUtility.LoadCatalogObject();
            if (catalogObject == null)
            {
                EditorGUILayout.HelpBox("Resources/SoundCatalog asset을 찾을 수 없습니다.", MessageType.Warning);
            }
            else if (useDefaultSound.boolValue)
            {
                SerializedProperty currentDefault = catalogObject.FindProperty("uiSounds.defaultButtonSoundId");
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("Current Default", currentDefault?.stringValue ?? string.Empty);
            }
            else
            {
                SoundCatalogEditorUtility.DrawSoundIdPopup(
                    catalogObject,
                    soundId,
                    "Override Sound",
                    SoundCategory.UiSfx);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
