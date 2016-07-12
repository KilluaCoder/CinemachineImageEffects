// Editor script for the motion blur effect

// Suppress "assigned but never used" warning
#pragma warning disable 414

// Show fancy graphs
#define SHOW_GRAPHS

// Show advanced options (not useful in most cases)
// #define ADVANCED_OPTIONS

using UnityEngine;
using UnityEditor;

namespace UnityStandardAssets.CinematicEffects
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MotionBlur))]
    public class MotionBlurEditor : Editor
    {
        MotionBlurGraphDrawer _graph;

        SerializedProperty _shutterAngle;
        SerializedProperty _sampleCount;
        SerializedProperty _maxBlurRadius;
        SerializedProperty _frameBlending;
        SerializedProperty _debugMode;

        [SerializeField] Texture2D _blendingIcon;

        static GUIContent _textStrength = new GUIContent("Strength");

        void OnEnable()
        {
            _shutterAngle = serializedObject.FindProperty("_settings.shutterAngle");
            _sampleCount = serializedObject.FindProperty("_settings.sampleCount");
            _maxBlurRadius = serializedObject.FindProperty("_settings.maxBlurRadius");
            _frameBlending = serializedObject.FindProperty("_settings.frameBlending");
            _debugMode = serializedObject.FindProperty("_debugMode");
        }

        public override void OnInspectorGUI()
        {
            if (_graph == null) _graph = new MotionBlurGraphDrawer(_blendingIcon);

            serializedObject.Update();

            EditorGUILayout.LabelField("Shutter Speed Simulation", EditorStyles.boldLabel);

            #if SHOW_GRAPHS
            _graph.DrawShutterGraph(_shutterAngle.floatValue);
            #endif

            EditorGUILayout.PropertyField(_shutterAngle);
            EditorGUILayout.PropertyField(_sampleCount);

            #if ADVANCED_OPTIONS
            EditorGUILayout.PropertyField(_maxBlurRadius);
            #endif

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Multi Frame Blending", EditorStyles.boldLabel);

            #if SHOW_GRAPHS
            _graph.DrawBlendingGraph(_frameBlending.floatValue);
            #endif

            EditorGUILayout.PropertyField(_frameBlending, _textStrength);

            #if ADVANCED_OPTIONS
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_debugMode);
            #endif

            serializedObject.ApplyModifiedProperties();
        }
    }
}
