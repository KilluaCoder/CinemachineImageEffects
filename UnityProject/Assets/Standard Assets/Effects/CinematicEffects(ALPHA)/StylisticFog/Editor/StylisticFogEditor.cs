using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.AnimatedValues;


namespace UnityStandardAssets.CinematicEffects
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(StylisticFog))]
	public class StylisticFogEditor : Editor
	{

		SerializedProperty _useDistance;
		SerializedProperty _useHeight;
		SerializedProperty _fogOpacityCurve;
		SerializedProperty _fogColorRCurve;
		SerializedProperty _fogColorGCurve;
		SerializedProperty _fogColorBCurve;
		SerializedProperty _farPlane;
		SerializedProperty _nearPlane;
		SerializedProperty _fogSkybox;
		SerializedProperty _colorSelection;
		SerializedProperty _fogMode;
		SerializedProperty _flipHeight;
		SerializedProperty _height;
		SerializedProperty _baseDensity;
		SerializedProperty _fogFactorIntensityCurve;
		SerializedProperty _densityFalloff;

		AnimBool m_UseHeight;
		AnimBool m_UseDistance;

		void OnEnable()
		{
			_useDistance     = serializedObject.FindProperty("settings.useDistance");
			_useHeight       = serializedObject.FindProperty("settings.useHeight");
			_fogOpacityCurve = serializedObject.FindProperty("settings.fogOpacityCurve");
			_fogColorRCurve  = serializedObject.FindProperty("settings.fogColorR");
			_fogColorGCurve  = serializedObject.FindProperty("settings.fogColorG");
			_fogColorBCurve  = serializedObject.FindProperty("settings.fogColorB");
			_farPlane        = serializedObject.FindProperty("settings.startDist");
			_nearPlane       = serializedObject.FindProperty("settings.endDist");
			_fogSkybox       = serializedObject.FindProperty("settings.fogSkybox");
			_fogMode         = serializedObject.FindProperty("settings.fogMode");
			_height          = serializedObject.FindProperty("settings.baseHeight");
			_baseDensity     = serializedObject.FindProperty("settings.baseDensity");
			_fogFactorIntensityCurve = serializedObject.FindProperty("settings.fogFactorIntensityCurve");
			_densityFalloff = serializedObject.FindProperty("settings.densityFalloff");

			StylisticFog targetInstance = (StylisticFog)target;

			m_UseHeight = new AnimBool(targetInstance.settings.fogMode == StylisticFog.FogMode.Height);
			m_UseHeight.valueChanged.AddListener(Repaint);

			m_UseDistance = new AnimBool(targetInstance.settings.fogMode == StylisticFog.FogMode.Distance);
			m_UseDistance.valueChanged.AddListener(Repaint);
		}

		public override void OnInspectorGUI()
		{
			StylisticFog targetInstance = (StylisticFog)target;

			serializedObject.Update();

			bool propertyTextureRebake = false;
			bool densityTextureRebake = false;
			bool checkNearFarDistances = false;

			EditorGUILayout.PropertyField(_useDistance);
			EditorGUILayout.PropertyField(_useHeight);

			m_UseHeight.target = targetInstance.settings.useHeight;
			m_UseDistance.target = targetInstance.settings.useDistance;

			// Curves to modify the color properties of the fog.
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(_fogOpacityCurve);
			EditorGUILayout.PropertyField(_fogColorRCurve);
			EditorGUILayout.PropertyField(_fogColorGCurve);
			EditorGUILayout.PropertyField(_fogColorBCurve);
			if (EditorGUI.EndChangeCheck())
				propertyTextureRebake = true;

			// The curve that defines how much the fog cntributes according to its intensity.
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField(_fogFactorIntensityCurve);
			if (EditorGUI.EndChangeCheck())
				densityTextureRebake = true;

			EditorGUILayout.Space();

			// Where the fog starts and where the fog reaches max saturation
			EditorGUI.BeginChangeCheck();

			if (EditorGUILayout.BeginFadeGroup(m_UseDistance.faded))
			{
				EditorGUILayout.PropertyField(_nearPlane);
			}
			EditorGUILayout.EndFadeGroup();

			EditorGUILayout.PropertyField(_farPlane);
			if (EditorGUI.EndChangeCheck())
				checkNearFarDistances = true;

			EditorGUILayout.Space();

			// Bool to decide if the skybox will be affected by fog.
			EditorGUILayout.PropertyField(_fogSkybox);

			// Parameters used when the fog volume is height based
			if (EditorGUILayout.BeginFadeGroup(m_UseHeight.faded))
			{

				EditorGUILayout.LabelField("Height Parameters");

				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(_height);
				EditorGUILayout.PropertyField(_baseDensity);
				EditorGUILayout.PropertyField(_densityFalloff);
				
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFadeGroup();

			if (propertyTextureRebake)
			{
				targetInstance.BakeFogProperty();
			}

			if(densityTextureRebake)
			{
				targetInstance.BakeFogIntensity();
			}

			if (checkNearFarDistances)
			{
				targetInstance.correctStartEndDistances();
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}



