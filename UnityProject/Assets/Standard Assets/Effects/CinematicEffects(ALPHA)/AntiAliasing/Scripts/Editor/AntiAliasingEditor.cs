/*
 * Copyright (c) 2015 Thomas Hourdel
 * Copyright (c) 2015 Goksel Goktas (Unity)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 *    1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 *
 *    2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 *
 *    3. This notice may not be removed or altered from any source
 *    distribution.
 */

namespace UnityStandardAssets.CinematicEffects
{
    using UnityEditor;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    [CustomEditor(typeof(AntiAliasing))]
    public class AntiAliasingEditor : Editor
    {
        private List<SerializedProperty> m_TopLevelFields = new List<SerializedProperty>();
        private Dictionary<FieldInfo, List<SerializedProperty>> m_GroupFields = new Dictionary<FieldInfo, List<SerializedProperty>>();

        private void OnEnable()
        {
            var topLevelSettings = typeof(AntiAliasing).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(x => x.GetCustomAttributes(typeof(AntiAliasing.TopLevelSettings), false).Any());
            var settingsGroups = typeof(AntiAliasing).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(x => x.GetCustomAttributes(typeof(AntiAliasing.SettingsGroup), false).Any());
            
            foreach (var group in topLevelSettings)
            {
                var searchPath = group.Name + ".";

                foreach (var setting in group.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    var property = serializedObject.FindProperty(searchPath + setting.Name);
                    if (property != null)
                        m_TopLevelFields.Add(property);
                }
            }

            foreach (var group in settingsGroups)
            {
                var searchPath = group.Name + ".";

                foreach (var setting in group.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    List<SerializedProperty> settingsGroup;
                    if (!m_GroupFields.TryGetValue(group, out settingsGroup))
                    {
                        settingsGroup = new List<SerializedProperty>();
                        m_GroupFields[group] = settingsGroup;
                    }

                    var property = serializedObject.FindProperty(searchPath + setting.Name);
                    if (property != null)
                        settingsGroup.Add(property);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            foreach (var setting in m_TopLevelFields)
                EditorGUILayout.PropertyField(setting);

            foreach (var group in m_GroupFields)
            {
                if (group.Key.FieldType == typeof(AntiAliasing.QualitySettings) && (target as AntiAliasing).settings.quality != AntiAliasing.QualityPreset.Custom)
                    continue;

                string title = group.Key.Name;
                title = char.ToUpper(title[0]) + title.Substring(1);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                var enabledField = group.Value.FirstOrDefault(x => x.propertyPath == group.Key.Name + ".enabled");
                if (enabledField != null && !enabledField.boolValue)
                {
                    EditorGUILayout.PropertyField(enabledField);
                    EditorGUI.indentLevel--;
                    continue;
                }

                foreach (var field in group.Value)
                    EditorGUILayout.PropertyField(field);

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
