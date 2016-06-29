// Editor script for the motion blur effect

// #define EDITOR_DETAIL

using UnityEngine;
using UnityEditor;

namespace UnityStandardAssets.CinematicEffects
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MotionBlur))]
    public class MotionBlurEditor : Editor
    {
        SerializedProperty _sampleCount;
        SerializedProperty _customSampleCount;
        SerializedProperty _accumulationRatio;
        #if EDITOR_DETAIL
        SerializedProperty _exposureTime;
        SerializedProperty _shutterAngle;
        SerializedProperty _shutterSpeed;
        SerializedProperty _maxBlurRadius;
        SerializedProperty _debugMode;
        #endif

        static GUIContent _textCustomValue = new GUIContent("Custom Value");
        #if !EDITOR_DETAIL
        static GUIContent _textBlendRatio = new GUIContent("Blend Ratio");
        #else
        static GUIContent _textTime = new GUIContent("Time = 1 /");
        static GUIContent _textMaxBlur = new GUIContent("Max Blur Radius %");
        #endif

        void OnEnable()
        {
            _sampleCount = serializedObject.FindProperty("_settings.sampleCount");
            _customSampleCount = serializedObject.FindProperty("_settings.customSampleCount");
            _accumulationRatio = serializedObject.FindProperty("_settings.accumulationRatio");
            #if EDITOR_DETAIL
            _exposureTime = serializedObject.FindProperty("_settings.exposureTime");
            _shutterAngle = serializedObject.FindProperty("_settings.shutterAngle");
            _shutterSpeed = serializedObject.FindProperty("_settings.shutterSpeed");
            _maxBlurRadius = serializedObject.FindProperty("_settings.maxBlurRadius");
            _debugMode = serializedObject.FindProperty("_debugMode");
            #endif
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            #if !EDITOR_DETAIL

            // Exposure time simulation options
            EditorGUILayout.LabelField("Exposure Time Simulation", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(_sampleCount);

            var showAllItems = _sampleCount.hasMultipleDifferentValues;
            var sampleCount = (MotionBlur.SampleCount)_sampleCount.enumValueIndex;

            if (showAllItems || sampleCount == MotionBlur.SampleCount.Custom)
                EditorGUILayout.PropertyField(_customSampleCount, _textCustomValue);

            EditorGUI.indentLevel--;

            // Color accumulation options
            EditorGUILayout.LabelField("Color Accumulation", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_accumulationRatio, _textBlendRatio);
            EditorGUI.indentLevel--;

            #else

            EditorGUILayout.PropertyField(_exposureTime);

            var showAllItems = _exposureTime.hasMultipleDifferentValues;
            var exposureTime = (MotionBlur.ExposureTime)_exposureTime.enumValueIndex;

            EditorGUI.indentLevel++;

            if (showAllItems || exposureTime == MotionBlur.ExposureTime.DeltaTime)
                EditorGUILayout.PropertyField(_shutterAngle);

            if (showAllItems || exposureTime == MotionBlur.ExposureTime.Constant)
                EditorGUILayout.PropertyField(_shutterSpeed, _textTime);

            EditorGUI.indentLevel--;

            // Sample count options
            EditorGUILayout.PropertyField(_sampleCount);

            showAllItems = _sampleCount.hasMultipleDifferentValues;
            var sampleCount = (MotionBlur.SampleCount)_sampleCount.enumValueIndex;

            EditorGUI.indentLevel++;

            if (showAllItems || sampleCount == MotionBlur.SampleCount.Custom)
                EditorGUILayout.PropertyField(_customSampleCount, _textCustomValue);

            EditorGUI.indentLevel--;

            // Other options
            EditorGUILayout.PropertyField(_maxBlurRadius, _textMaxBlur);
            EditorGUILayout.PropertyField(_accumulationRatio);
            EditorGUILayout.PropertyField(_debugMode);

            #endif

            serializedObject.ApplyModifiedProperties();
        }
    }
}
