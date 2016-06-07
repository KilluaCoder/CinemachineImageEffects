// Editor script for the motion blur effect

// Debug items are hidden by default (not very useful in most cases).
// #define SHOW_DEBUG

using UnityEngine;
using UnityEditor;

namespace UnityStandardAssets.CinematicEffects
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MotionBlur))]
    public class MotionBlurEditor : Editor
    {
        SerializedProperty _exposureMode;
        SerializedProperty _shutterSpeed;
        SerializedProperty _exposureTimeScale;
        SerializedProperty _sampleCount;
        SerializedProperty _sampleCountValue;
        SerializedProperty _maxBlurRadius;
        #if SHOW_DEBUG
        SerializedProperty _debugMode;
        #endif

        static GUIContent _textScale = new GUIContent("Scale");
        static GUIContent _textValue = new GUIContent("Value");
        static GUIContent _textTime = new GUIContent("Time = 1 /");
        static GUIContent _textMaxBlur = new GUIContent("Max Blur Radius %");

        void OnEnable()
        {
            _exposureMode = serializedObject.FindProperty("_settings.exposureMode");
            _shutterSpeed = serializedObject.FindProperty("_settings.shutterSpeed");
            _exposureTimeScale = serializedObject.FindProperty("_settings.exposureTimeScale");
            _sampleCount = serializedObject.FindProperty("_settings.sampleCount");
            _sampleCountValue = serializedObject.FindProperty("_settings.sampleCountValue");
            _maxBlurRadius = serializedObject.FindProperty("_settings.maxBlurRadius");
            #if SHOW_DEBUG
             _debugMode = serializedObject.FindProperty("_debugMode");
            #endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_exposureMode);

            if (_exposureMode.hasMultipleDifferentValues ||
                _exposureMode.enumValueIndex == (int)MotionBlur.ExposureMode.Constant)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_shutterSpeed, _textTime);
                EditorGUI.indentLevel--;
            }

            if (_exposureMode.hasMultipleDifferentValues ||
                _exposureMode.enumValueIndex == (int)MotionBlur.ExposureMode.DeltaTime)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_exposureTimeScale, _textScale);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(_sampleCount);

            if (_sampleCount.hasMultipleDifferentValues ||
                _sampleCount.enumValueIndex == (int)MotionBlur.SampleCount.Variable)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_sampleCountValue, _textValue);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(_maxBlurRadius, _textMaxBlur);

            #if SHOW_DEBUG
            EditorGUILayout.PropertyField(_debugMode);
            #endif

            serializedObject.ApplyModifiedProperties();
        }
    }
}
