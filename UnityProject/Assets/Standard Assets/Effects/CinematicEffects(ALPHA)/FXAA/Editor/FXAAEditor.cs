using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityStandardAssets.CinematicEffects
{
    [CustomEditor(typeof(FXAA))]
    public class FXAAEditor : Editor
    {
        private string[] presetNames = new string[]
        {
            "Extreme performance",
            "Performance",
            "Default",
            "Quality",
            "Extreme quality"
        };

        private int selectedPreset = 2;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var fxaaTarget = (FXAA)target;

            EditorGUILayout.LabelField("Fast approximate anti-aliasing", EditorStyles.miniBoldLabel);

//figutre out the preset;

            if (fxaaTarget.preset.Equals(FXAA.Preset.extremePerformancePreset))
                selectedPreset = 0;
            else if (fxaaTarget.preset.Equals(FXAA.Preset.performancePreset))
                selectedPreset = 1;
            else if (fxaaTarget.preset.Equals(FXAA.Preset.defaultPreset))
                selectedPreset = 2;
            else if (fxaaTarget.preset.Equals(FXAA.Preset.qualityPreset))
                selectedPreset = 3;
            else if (fxaaTarget.preset.Equals(FXAA.Preset.extremeQualityPreset))
                selectedPreset = 4;

            selectedPreset = EditorGUILayout.Popup("Preset", selectedPreset, presetNames);

            if (selectedPreset < 0)
                selectedPreset = 0;
            else if (selectedPreset > 4)
                selectedPreset = 4;

            fxaaTarget.preset = FXAA.availablePresets[selectedPreset];
            serializedObject.ApplyModifiedProperties();
        }
    }
}
