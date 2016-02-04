using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityStandardAssets.CinematicEffects
{
    public class FXAAEditor : IAntiAliasingEditor
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

        public void OnEnable(SerializedObject serializedObject, string path)
        {
        }

        public void OnInspectorGUI(IAntiAliasing target)
        {
            var fxaaTarget = (FXAA)target;

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
        }
    }
}
