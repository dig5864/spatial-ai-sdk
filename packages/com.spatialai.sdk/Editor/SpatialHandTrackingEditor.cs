using SpatialAI.Unity;
using UnityEditor;
using UnityEngine;

namespace SpatialAI.Editor
{
    [CustomEditor(typeof(SpatialHandTracking))]
    [CanEditMultipleObjects]
    public sealed class SpatialHandTrackingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Global runtime settings. Changes affect the active hand tracker and are not saved to the scene.", MessageType.Info);
                EditorGUI.BeginChangeCheck();
                var backend = (SpatialHandInferenceBackend)EditorGUILayout.EnumPopup("Inference Backend", SpatialHandTracking.InferenceBackend);
                if (EditorGUI.EndChangeCheck()) SpatialHandTracking.InferenceBackend = backend;
                EditorGUI.BeginChangeCheck();
                var mode = (SpatialHandMode)EditorGUILayout.EnumPopup("Hand Mode", SpatialHandTracking.HandMode);
                if (EditorGUI.EndChangeCheck()) SpatialHandTracking.HandMode = mode;
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("initialInferenceBackend"), new GUIContent("Inference Backend"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("initialHandMode"), new GUIContent("Hand Mode"));
            }
            DrawPropertiesExcluding(serializedObject, "initialInferenceBackend", "initialHandMode");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
