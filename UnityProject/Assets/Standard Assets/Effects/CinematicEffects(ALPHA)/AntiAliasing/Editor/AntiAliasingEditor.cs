using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityStandardAssets.CinematicEffects
{
    [CustomEditor(typeof(AntiAliasing))]
    public class AntiAliasingEditor : Editor
    {
        private string[] methodNames = new string[]
        {
            "Subpixel Morphological Anti-aliasing",
            "Fast Approximate Anti-aliasing"
        };

        private int selectedMethod = 0;

        private SMAAEditor m_SMAAEditor = new SMAAEditor();
        private FXAAEditor m_FXAAEditor = new FXAAEditor();

        IAntiAliasingEditor antiAliasingEditor = null;

        private void OnEnable()
        {
                m_SMAAEditor.OnEnable(serializedObject, "m_SMAA");
                m_FXAAEditor.OnEnable(serializedObject, "m_FXAA");
        }

        public override void OnInspectorGUI()
        {
                serializedObject.Update();
                var antiAliasingTarget = (AntiAliasing)target;

                selectedMethod = antiAliasingTarget.method;
                selectedMethod = EditorGUILayout.Popup("Method", selectedMethod, methodNames);

                if (selectedMethod < 0)
                    selectedMethod = 0;
                else if (selectedMethod > 1)
                    selectedMethod = 1;

                if (selectedMethod == 0)
                    antiAliasingEditor = m_SMAAEditor;
                else
                    antiAliasingEditor = m_FXAAEditor;

                antiAliasingTarget.method = selectedMethod;

                antiAliasingEditor.OnInspectorGUI(antiAliasingTarget.current);
                serializedObject.ApplyModifiedProperties();
        }
    }
}
