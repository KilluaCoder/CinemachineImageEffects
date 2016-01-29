using UnityEngine;
using UnityEditor;

namespace UnityStandardAssets.CinematicEffects
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Bloom))]
    public class Bloomditor : Editor
    {
        SerializedProperty m_threshold;
        SerializedProperty m_exposure;
        SerializedProperty m_radius;
        SerializedProperty m_intensity;
        SerializedProperty m_quality;
        SerializedProperty m_antiFlicker;

        static GUIContent m_antiFlickerLabel = new GUIContent("Anti-Flicker");

        void OnEnable()
        {
            m_threshold = serializedObject.FindProperty("m_threshold");
            m_exposure = serializedObject.FindProperty("m_exposure");
            m_radius = serializedObject.FindProperty("m_radius");
            m_intensity = serializedObject.FindProperty("m_intensity");
            m_quality = serializedObject.FindProperty("m_quality");
            m_antiFlicker = serializedObject.FindProperty("m_antiFlicker");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_threshold);
            EditorGUILayout.PropertyField(m_exposure);
            EditorGUILayout.PropertyField(m_radius);
            EditorGUILayout.PropertyField(m_intensity);
            EditorGUILayout.PropertyField(m_quality);
            EditorGUILayout.PropertyField(m_antiFlicker, m_antiFlickerLabel);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
