using System;
using UnityEngine;
using System.IO;

namespace UnityStandardAssets.CinematicEffects
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(Camera))]
	[AddComponentMenu("Image Effects/Stylistic Fog")]
#if UNITY_5_4_OR_NEWER
    [ImageEffectAllowedInSceneView]
#endif
	public class StylisticFog : MonoBehaviour
	{

		[AttributeUsage(AttributeTargets.Field)]
		public class SettingsGroup : Attribute
		{ }

		public enum ColorSelectionType
		{
			ColorPicker = 0,
			Curves = 1,
			TextureRamp = 2,
			CopyOther = 3,
		}

		[Serializable]
		public class FogColorSource
		{
			[AttributeUsage(AttributeTargets.Field)]
			public class DisplayOnSelectionType : Attribute
			{
				public readonly ColorSelectionType selectionType;
				public DisplayOnSelectionType(ColorSelectionType _selectionType)
				{
					selectionType = _selectionType;
				}
			}

			[Tooltip("Uniform fog color")]
			[DisplayOnSelectionType(ColorSelectionType.ColorPicker)]
			public Color color;

			// Cureves for the color texture
			[Tooltip("Determines the opacity based on fog intensity.")]
			[DisplayOnSelectionType(ColorSelectionType.Curves)]
			public AnimationCurve fogOpacityCurve;

			[Tooltip("Red component of fog color, based on fog intensity.")]
			[DisplayOnSelectionType(ColorSelectionType.Curves)]
			public AnimationCurve fogColorR;

			[Tooltip("Green component of fog color, based on fog intensity.")]
			[DisplayOnSelectionType(ColorSelectionType.Curves)]
			public AnimationCurve fogColorG;

			[Tooltip("Blue component of fog color, based on fog intensity.")]
			[DisplayOnSelectionType(ColorSelectionType.Curves)]
			public AnimationCurve fogColorB;

			[Tooltip("Custom fog color ramp")]
			[DisplayOnSelectionType(ColorSelectionType.TextureRamp)]
			public Texture2D colorRamp;

			public static FogColorSource defaultSettings
			{
				get 
				{
					return new FogColorSource()
					{
						color = Color.white,
						fogOpacityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
						fogColorR = AnimationCurve.Linear(0f, 0f, 1f, 1f),
						fogColorG = AnimationCurve.Linear(0f, 0f, 1f, 1f),
						fogColorB = AnimationCurve.Linear(0f, 0f, 1f, 1f),
						colorRamp = null,
					};
				}
			}
		}

		[Serializable]
		public struct DistanceFogSettings
		{
			[Tooltip("Wheter or not to apply distance based fog.")]
			public bool enabled;

			[Tooltip("Wheter or not to apply distance based fog to the skybox")]
			public bool fogSkybox;

			[Tooltip("Fog is excluded from distances closer than this.")]
			public float startDist;

			[Tooltip("Fog is fully saturated beyond this distance.")]
			public float endDist;

			[Tooltip("How the intensity of the fog develops with the distance.")]
			public AnimationCurve intensityDevelopment;

			[Tooltip("Color selection for distance fog")]
			public ColorSelectionType colorSelectionType;

			public static DistanceFogSettings defaultSettings
			{
				get
				{
					return new DistanceFogSettings()
					{
						enabled = true,
						fogSkybox = false,
						startDist = 0f,
						endDist = 100f,
						intensityDevelopment = AnimationCurve.Linear(0f, 0f, 1f, 1f),
						colorSelectionType = ColorSelectionType.ColorPicker,
					};
				}
			}
		}

		[Serializable]
		public struct HeightFogSettings
		{
			[Tooltip("Wheter or not to apply height based fog.")]
			public bool enabled;

			[Tooltip("Wheter or not to apply height based fog to the skybox")]
			public bool fogSkybox;

			[Tooltip("Height where the fog starts")]
			public float baseHeight;

			[Tooltip("Fog density at fog altitude given by height.")]
			public float baseDensity;

			[Tooltip("The rate at which the thickness of the fog decays with altitude")]
			[Range(-1f, 1f)]
			public float densityFalloff;

			[Tooltip("Color selection for height fog")]
			public ColorSelectionType colorSelectionType;

			public static HeightFogSettings defaultSettings
			{
				get
				{
					return new HeightFogSettings()
					{
						enabled = false,
						fogSkybox = true,
						baseHeight = 0f,
						baseDensity = 0.1f,
						densityFalloff = 0.5f,
						colorSelectionType = ColorSelectionType.CopyOther,
					};
				}
			}
		}

		[SettingsGroup, SerializeField]
		public DistanceFogSettings distanceFog = DistanceFogSettings.defaultSettings;

		[SettingsGroup, SerializeField]
		public HeightFogSettings heightFog = HeightFogSettings.defaultSettings;

		[SerializeField]
		public FogColorSource distanceColorSource = FogColorSource.defaultSettings;

		[SerializeField]
		public FogColorSource heightColorSource = FogColorSource.defaultSettings;

		[SerializeField]
		private Texture2D m_DistanceColorTexture;
		public Texture2D distanceColorTexture
		{
			get
			{
				if (m_DistanceColorTexture == null)
				{
					m_DistanceColorTexture = new Texture2D(1024, 1, TextureFormat.ARGB32, false, false)
					{
						name = "Fog property",
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
					};
					BakeFogColor(m_DistanceColorTexture, distanceColorSource.fogColorR, distanceColorSource.fogColorG, distanceColorSource.fogColorB, distanceColorSource.fogOpacityCurve);
				}
				return m_DistanceColorTexture;
			}
		}
		
		[SerializeField]
		private Texture2D m_HeightColorTexture;
		public Texture2D heightColorTexture
		{
			get
			{
				if (m_HeightColorTexture == null)
				{
					m_HeightColorTexture = new Texture2D(1024, 1, TextureFormat.ARGB32, false, false)
					{
						name = "Fog property",
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
					};
					BakeFogColor(m_HeightColorTexture, heightColorSource.fogColorR, heightColorSource.fogColorG, heightColorSource.fogColorB, heightColorSource.fogOpacityCurve);
				}
				return m_HeightColorTexture;
			}
		}

		[SerializeField]
		private Texture2D m_distanceFogIntensityTexture;
		public Texture2D distanceFogIntensityTexture
		{
			get
			{
				if (m_distanceFogIntensityTexture == null)
				{
					m_distanceFogIntensityTexture = new Texture2D(1024, 1, TextureFormat.ARGB32, false, false)
					{
						name = "Fog Height density",
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
					};
					BakeFogIntensity();
				}
				return m_distanceFogIntensityTexture;
			}
		}

		[SerializeField, HideInInspector]
		private Shader m_Shader;
		public Shader shader
		{
			get
			{
				if (m_Shader == null)
				{
					const string shaderName = "Hidden/Image Effects/StylisticFog";
					m_Shader = Shader.Find(shaderName);
				}

				return m_Shader;
			}
		}

		private Material m_Material;
		public Material material
		{
			get
			{
				if (m_Material == null)
					m_Material = ImageEffectHelper.CheckShaderAndCreateMaterial(shader);

				return m_Material;
			}
		}

		public void UpdateProperties()
		{
			// Update the fog density
			BakeFogIntensity();

			// Check if both color selction types are to copy
			// If so, change one / show warning?
			bool selectionTypeSame = distanceFog.colorSelectionType == heightFog.colorSelectionType;
			bool distanceSelectedCopy = distanceFog.colorSelectionType == ColorSelectionType.CopyOther;
			if (selectionTypeSame && distanceSelectedCopy)
			{
				distanceFog.colorSelectionType = ColorSelectionType.ColorPicker;
				distanceSelectedCopy = false;
			}

			UpdateDistanceFogTextures(distanceFog.colorSelectionType);
			UpdateHeightFogTextures(heightFog.colorSelectionType);
		}

		private void UpdateDistanceFogTextures(ColorSelectionType selectionType)
		{
			if (selectionType != ColorSelectionType.Curves)
			{
				if (m_DistanceColorTexture != null)
					DestroyImmediate(m_DistanceColorTexture);
				m_DistanceColorTexture = null;
			}

			if (selectionType == ColorSelectionType.Curves)
			{
				BakeFogColor(distanceColorTexture,
									distanceColorSource.fogColorR,
									distanceColorSource.fogColorG,
									distanceColorSource.fogColorB,
									distanceColorSource.fogOpacityCurve);
			}
		}

		private void UpdateHeightFogTextures(ColorSelectionType selectionType)
		{
			if (selectionType != ColorSelectionType.Curves)
			{
				if (m_HeightColorTexture != null)
					DestroyImmediate(m_HeightColorTexture);
				m_HeightColorTexture = null;
			}

			if (selectionType == ColorSelectionType.Curves)
			{
				BakeFogColor(heightColorTexture,
									heightColorSource.fogColorR,
									heightColorSource.fogColorG,
									heightColorSource.fogColorB,
									heightColorSource.fogOpacityCurve);
			}
		}

		#region Private Members
		private void OnEnable()
		{
			if (!ImageEffectHelper.IsSupported(shader, true, false, this))
				enabled = false;

			GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;

			BakeFogIntensity();
			UpdateProperties();
		}

		private void OnDisable()
		{
			if (m_Material != null)
				DestroyImmediate(m_Material);

			if (m_DistanceColorTexture != null)
				DestroyImmediate(m_DistanceColorTexture);

			if (m_HeightColorTexture != null)
				DestroyImmediate(m_HeightColorTexture);

			if (m_distanceFogIntensityTexture != null)
				DestroyImmediate(m_distanceFogIntensityTexture);

			m_Material = null;
		}

		private void SetMaterialValues()
		{
			// Get the inverse view matrix for converting depth to world position.
			Matrix4x4 inverseViewMatrix = GetComponent<Camera>().cameraToWorldMatrix;
			material.SetMatrix("_InverseViewMatrix", inverseViewMatrix);

			// Decide wheter the skybox is included in by the distance fog
			if (distanceFog.fogSkybox)
				material.DisableKeyword("OMMIT_SKYBOX_DIST");
			else
				material.EnableKeyword("OMMIT_SKYBOX_DIST");

			// Decide wheter the skybox is included in by the height fog
			if (heightFog.fogSkybox)
				material.DisableKeyword("OMMIT_SKYBOX_HEIGHT");
			else
				material.EnableKeyword("OMMIT_SKYBOX_HEIGHT");

			// Check distance fog should be enabled.
			if (distanceFog.enabled)
			{
				material.EnableKeyword("USE_DISTANCE");
				material.SetTexture("_FogFactorIntensityTexture", distanceFogIntensityTexture);
				material.SetFloat("_FogStartDist", distanceFog.startDist);
				material.SetFloat("_FogEndDist", distanceFog.endDist);
			}
			else
			{
				material.DisableKeyword("USE_DISTANCE");
			}

			// check if height fog should be enabled.
			if (heightFog.enabled)
			{
				material.EnableKeyword("USE_HEIGHT");
				material.SetFloat("_Height", heightFog.baseHeight);
				material.SetFloat("_BaseDensity", heightFog.baseDensity);
				material.SetFloat("_DensityFalloff", heightFog.densityFalloff);
			}
			else
			{
				material.DisableKeyword("USE_HEIGHT");
			}

			// Share color settings if one of the sources are set to copy the other
			bool sharedColorSettings = (distanceFog.colorSelectionType == ColorSelectionType.CopyOther) 
										|| (heightFog.colorSelectionType == ColorSelectionType.CopyOther);

			if (sharedColorSettings)
			{
				bool selectingFromDistance = true;
				material.EnableKeyword("SHARED_COLOR_SETTINGS");
				ColorSelectionType activeSelectionType = distanceFog.colorSelectionType;
				if (activeSelectionType == ColorSelectionType.CopyOther)
				{
					activeSelectionType = heightFog.colorSelectionType;
					selectingFromDistance = false;
				}

				if (activeSelectionType == ColorSelectionType.ColorPicker)
				{
					material.SetColor("_FogPickerColor0", selectingFromDistance ? distanceColorSource.color : heightColorSource.color);
					material.EnableKeyword("SHARED_COLOR_PICKER");
					material.DisableKeyword("SHARED_COLOR_TEXTURE");
				} 
				else if (activeSelectionType == ColorSelectionType.Curves)
				{
					material.SetTexture("_FogColorTexture0", selectingFromDistance ? distanceColorTexture : heightColorTexture);
					material.EnableKeyword("SHARED_COLOR_TEXTURE");
					material.DisableKeyword("SHARED_COLOR_PICKER");
				} 
				else if (activeSelectionType == ColorSelectionType.TextureRamp)
				{
					material.SetTexture("_FogColorTexture0", selectingFromDistance ? distanceColorSource.colorRamp : heightColorSource.colorRamp);
					material.EnableKeyword("SHARED_COLOR_TEXTURE");
					material.DisableKeyword("SHARED_COLOR_PICKER");
				}

			}
			else
			{
				material.DisableKeyword("SHARED_COLOR_SETTINGS");

				if (distanceFog.enabled)
				{
					if(distanceFog.colorSelectionType == ColorSelectionType.ColorPicker)
					{
						material.EnableKeyword("DIST_COLOR_PICKER");
						material.DisableKeyword("DIST_COLOR_TEXTURE");
						material.SetColor("_FogPickerColor0", distanceColorSource.color);
					}
					else
					{
						material.EnableKeyword("DIST_COLOR_TEXTURE");
						material.DisableKeyword("DIST_COLOR_PICKER");
						material.SetTexture("_FogColorTexture0", distanceFog.colorSelectionType == ColorSelectionType.Curves ? distanceColorTexture : distanceColorSource.colorRamp);
					}
				}

				if (heightFog.enabled)
				{
					if (heightFog.colorSelectionType == ColorSelectionType.ColorPicker)
					{
						material.EnableKeyword("HEIGHT_COLOR_PICKER");
						material.DisableKeyword("HEIGHT_COLOR_TEXTURE");
						material.SetColor("_FogPickerColor1", heightColorSource.color);
					}
					else
					{
						material.EnableKeyword("HEIGHT_COLOR_TEXTURE");
						material.DisableKeyword("HEIGHT_COLOR_PICKER");
						material.SetTexture("_FogColorTexture1", heightFog.colorSelectionType == ColorSelectionType.Curves ? heightColorTexture : heightColorSource.colorRamp);
					}
				}
			}

		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			SetMaterialValues();
			Graphics.Blit(source, destination, material);
		}


		public void BakeFogColor( Texture2D target,
									 AnimationCurve colorCurveR, 
									 AnimationCurve colorCurveG, 
									 AnimationCurve colorCurveB, 
									 AnimationCurve opacityCurve )
		{
			if (target == null)
			{
				return;
			}

			float fWidth = target.width;
			Color[] pixels = new Color[target.width];

			for (float i = 0f; i <= 1f; i += 1f / fWidth)
			{
				float r = Mathf.Clamp(colorCurveR.Evaluate(i), 0f, 1f);
				float g = Mathf.Clamp(colorCurveG.Evaluate(i), 0f, 1f);
				float b = Mathf.Clamp(colorCurveB.Evaluate(i), 0f, 1f);
				float a = Mathf.Clamp(opacityCurve.Evaluate(i), 0f, 1f);
				pixels[(int)Mathf.Floor(i * (fWidth -1f))] = new Color(r, g, b, a);
			}

			target.SetPixels(pixels);
			target.Apply();
		}


		public void BakeFogColor(Texture2D target, Texture2D source)
		{
			if (target == null || source == null)
			{
				return;
			}

			float fWidthSource = source.width;
			float fWidthTarget = target.width;
			Color[] pixels = new Color[target.width];

			for (float i = 0f; i <= 1f; i += 1f / fWidthTarget)
			{
				float targetLookup = i * (fWidthSource / fWidthTarget);
				float lowerIndex = Mathf.Floor(targetLookup);
				float upperIndex = Mathf.Ceil(targetLookup);
				float lerpFactor = targetLookup - lowerIndex;

				Color lower = source.GetPixel((int)lowerIndex, 0);
				Color upper = source.GetPixel((int)upperIndex, 0);
				Color interpolated = Color.Lerp(lower, upper, lerpFactor);

				pixels[(int)Mathf.Floor(i * (fWidthTarget - 1f))] = interpolated;
			}

			target.SetPixels(pixels);
			target.Apply();
		}


		public void BakeFogIntensity()
		{
			if (distanceFogIntensityTexture == null)
			{
				return;
			}

			Color[] pixels = new Color[1024];

			for (float i = 0f; i <= 1f; i += 1f / 1024f)
			{
				int index = (int)Mathf.Floor(i * 1023f);
				float density = Mathf.Clamp(distanceFog.intensityDevelopment.Evaluate(i), 0f, 1f);
				density = (density == 1f) ? 0.9999f : density;
				pixels[index] = EncodeFloatAsColor(density);
			}

			m_distanceFogIntensityTexture.SetPixels(pixels);
			m_distanceFogIntensityTexture.Apply();
		}

		// From http://aras-p.info/blog/2009/07/30/encoding-floats-to-rgba-the-final/
		private static Color EncodeFloatAsColor(float f)
		{
			Color encoded = new Color(1.0f, 255.0f, 65025.0f, 160581375.0f) * f;
			encoded.r = encoded.r - Mathf.Floor(encoded.r);
			encoded.g = encoded.g - Mathf.Floor(encoded.g);
			encoded.b = encoded.b - Mathf.Floor(encoded.b);
			encoded.a = encoded.a - Mathf.Floor(encoded.a);
			Color temp = new Color(encoded.g, encoded.b, encoded.a, encoded.a);
			temp *= new Color(1.0f / 255.0f, 1.0f / 255.0f, 1.0f / 255.0f, 0.0f);
			encoded -= temp;
			return encoded;
		}

		// From http://aras-p.info/blog/2009/07/30/encoding-floats-to-rgba-the-final/
		private static float DecodeFloatFromColor(Color col)
		{
			Vector4 colorAsVec = new Vector4(col.r, col.g, col.b, col.a);
			Vector4 temp = new Vector4(1.0f, 1.0f / 255.0f, 1.0f / 65025.0f, 1.0f / 160581375.0f);
			return Vector4.Dot(colorAsVec, temp);
		}
		#endregion

	}
}
