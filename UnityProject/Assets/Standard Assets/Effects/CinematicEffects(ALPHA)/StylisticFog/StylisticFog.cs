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

		public delegate string WarningDelegate();

		[AttributeUsage(AttributeTargets.Field)]
		public class SettingsGroup : Attribute
		{ }

		[Serializable]
		public enum ColorSelectionType
		{
			Gradient = 1,
			TextureRamp = 2,
			CopyOther = 3,
		}

		private enum FogTypePass
		{
			DistanceOnly              = 0,
			HeightOnly                = 1,
			BothSharedColorSettings   = 2,
			BothSeperateColorSettinsg = 3,
			None,
		}

		#region settings
		[Serializable]
		public struct FogColorSource
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

			[Tooltip("Color gradient.")]
			[DisplayOnSelectionType(ColorSelectionType.Gradient)]
			public Gradient gradient;

			[Tooltip("Custom fog color ramp")]
			[DisplayOnSelectionType(ColorSelectionType.TextureRamp)]
			public Texture2D colorRamp;

			public static FogColorSource defaultSettings
			{
				get
				{
					GradientAlphaKey firstAlpha = new GradientAlphaKey(0f, 0f);
					GradientAlphaKey lastAlpha = new GradientAlphaKey(1f, 1f);
					GradientAlphaKey[] initialAlphaKeys = { firstAlpha, lastAlpha };
					FogColorSource source =  new FogColorSource()
					{
						gradient = new Gradient(),
						colorRamp = null,
					};
					source.gradient.alphaKeys = initialAlphaKeys;
					return source;
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

			[Tooltip("Fog is fully saturated beyond this distance.")]
			public float endDistance;

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
						endDistance = 100f,
						intensityDevelopment = AnimationCurve.Linear(0f, 0f, 1f, 1f),
						colorSelectionType = ColorSelectionType.Gradient,
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
		#endregion

		#region settingFields
		[SettingsGroup, SerializeField]
		public DistanceFogSettings distanceFog = DistanceFogSettings.defaultSettings;

		[SettingsGroup, SerializeField]
		public HeightFogSettings heightFog = HeightFogSettings.defaultSettings;

		[SerializeField]
		public FogColorSource distanceColorSource = FogColorSource.defaultSettings;

		[SerializeField]
		public FogColorSource heightColorSource = FogColorSource.defaultSettings;
		#endregion

		#region fields
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
					BakeFogColor(m_DistanceColorTexture, distanceColorSource.gradient);
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
					BakeFogColor(m_HeightColorTexture, heightColorSource.gradient);
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
		#endregion 

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
				distanceFog.colorSelectionType = ColorSelectionType.Gradient;
				distanceSelectedCopy = false;
			}

			UpdateDistanceFogTextures(distanceFog.colorSelectionType);
			UpdateHeightFogTextures(heightFog.colorSelectionType);
		}

		private void UpdateDistanceFogTextures(ColorSelectionType selectionType)
		{
			// If the gradient texture is not used, delete it.
			if (selectionType != ColorSelectionType.Gradient || GradientIsSingleColor(distanceColorSource.gradient))
			{
				if (m_DistanceColorTexture != null)
					DestroyImmediate(m_DistanceColorTexture);
				m_DistanceColorTexture = null;
			}

			if (selectionType == ColorSelectionType.Gradient)
			{
				BakeFogColor(distanceColorTexture, distanceColorSource.gradient);
			}
		}

		private void UpdateHeightFogTextures(ColorSelectionType selectionType)
		{
			// If the gradient texture is not used, delete it.
			if (selectionType != ColorSelectionType.Gradient || GradientIsSingleColor(heightColorSource.gradient))
			{
				if (m_HeightColorTexture != null)
					DestroyImmediate(m_HeightColorTexture);
				m_HeightColorTexture = null;
			}

			if (selectionType == ColorSelectionType.Gradient)
			{
				BakeFogColor(heightColorTexture, heightColorSource.gradient);
			}
		}

		#region Private Members
		
		// If all keys in a gradient are the same
		// the gradient can be represented as just a single color
		private bool GradientIsSingleColor(Gradient gradient)
		{
			GradientAlphaKey[] alphaKeys = gradient.alphaKeys;
			GradientColorKey[] colorKeys = gradient.colorKeys;
			bool allKeysAreTheSame = true;
			float referenceAlpha = alphaKeys[0].alpha;
			Color referenceColor = colorKeys[0].color;

			foreach(GradientAlphaKey alphaKey in alphaKeys)
			{
				if (alphaKey.alpha != referenceAlpha)
				{
					allKeysAreTheSame = false;
				}
			}

			foreach(GradientColorKey colorKey in colorKeys)
			{
				if (colorKey.color != referenceColor)
				{
					allKeysAreTheSame = false;
				}
			}

			return allKeysAreTheSame;
		}

		private Color GradientGetSingleColor(Gradient gradient)
		{
			Color singleColor = gradient.colorKeys[0].color;
			singleColor.a = gradient.alphaKeys[0].alpha;
			return singleColor;
		}

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


		private void SetDistanceFogUniforms()
		{
			material.SetTexture("_FogFactorIntensityTexture", distanceFogIntensityTexture);
			material.SetFloat("_FogEndDistance", distanceFog.endDistance);
		}

		private void SetHeightFogUniforms()
		{
			material.SetFloat("_Height", heightFog.baseHeight);
			material.SetFloat("_BaseDensity", heightFog.baseDensity);
			material.SetFloat("_DensityFalloff", heightFog.densityFalloff);
		}

		private FogTypePass SetMaterialUniforms()
		{

			// Determine the fog type pass
			FogTypePass fogType = FogTypePass.DistanceOnly;

			if(!distanceFog.enabled && heightFog.enabled)
				fogType = FogTypePass.HeightOnly;

			// Share color settings if one of the sources are set to copy the other
			bool sharedColorSettings = (distanceFog.colorSelectionType == ColorSelectionType.CopyOther)
										|| (heightFog.colorSelectionType == ColorSelectionType.CopyOther);

			if(distanceFog.enabled && heightFog.enabled)
			{
				if(sharedColorSettings)
				{
					fogType = FogTypePass.BothSharedColorSettings;
				}
				else
				{
					fogType = FogTypePass.BothSeperateColorSettinsg;
				}
			}

			if (!distanceFog.enabled && !heightFog.enabled)
				return FogTypePass.None;

			// Get the inverse view matrix for converting depth to world position.
			Matrix4x4 inverseViewMatrix = GetComponent<Camera>().cameraToWorldMatrix;
			material.SetMatrix("_InverseViewMatrix", inverseViewMatrix);

			// Decide wheter the skybox should have fog applied
			material.SetInt("_ApplyDistToSkybox", distanceFog.fogSkybox ? 1 : 0);
			material.SetInt("_ApplyHeightToSkybox", heightFog.fogSkybox ? 1 : 0);

			// Is the shared color sampled from a texture? Otherwise it's from a single color( picker)
			if (sharedColorSettings)
			{
				bool selectingFromDistance = true;
				FogColorSource activeSelectionSource = distanceColorSource;
				ColorSelectionType activeSelectionType = distanceFog.colorSelectionType;
				if (activeSelectionType == ColorSelectionType.CopyOther)
				{
					activeSelectionType = heightFog.colorSelectionType;
					activeSelectionSource = heightColorSource;
					selectingFromDistance = false;
				}

				bool useSingleColor = (activeSelectionType == ColorSelectionType.Gradient) && GradientIsSingleColor(activeSelectionSource.gradient);

				material.SetInt("_ColorSourceOneIsTexture", useSingleColor ? 0 : 1);
				SetDistanceFogUniforms();
				SetHeightFogUniforms();
				if (useSingleColor)
				{
					material.SetInt("_ColorSourceOneIsTexture", 0);
					Color appliedColor = GradientGetSingleColor(activeSelectionSource.gradient);
					material.SetColor("_FogColor0", appliedColor);
				}
				else
				{
					material.SetInt("_ColorSourceOneIsTexture", 1);
					if(activeSelectionType == ColorSelectionType.Gradient)
						material.SetTexture("_FogColorTexture0", selectingFromDistance ? distanceColorTexture : heightColorTexture);
					else
						material.SetTexture("_FogColorTexture0", selectingFromDistance ? distanceColorSource.colorRamp : heightColorSource.colorRamp);
				}
			}
			else
			{
				if (distanceFog.enabled)
				{
					if (distanceFog.colorSelectionType == ColorSelectionType.Gradient && GradientIsSingleColor(distanceColorSource.gradient))
					{
						Color appliedColor = GradientGetSingleColor(distanceColorSource.gradient);
						material.SetColor("_FogColor0", appliedColor);
						material.SetInt("_ColorSourceOneIsTexture", 0);
					}
					else
					{
						material.SetTexture("_FogColorTexture0", distanceFog.colorSelectionType == ColorSelectionType.Gradient ? distanceColorTexture : distanceColorSource.colorRamp);
						material.SetInt("_ColorSourceOneIsTexture", 1);
					}
				}

				if (heightFog.enabled)
				{
					string textureSourceIdentifier = fogType == FogTypePass.HeightOnly ? "_ColorSourceOneIsTexture" : "_ColorSourceTwoIsTexture";

					if (heightFog.colorSelectionType == ColorSelectionType.Gradient && GradientIsSingleColor(heightColorSource.gradient))
					{
						string colorPickerIdentifier = fogType == FogTypePass.HeightOnly ? "_FogColor0" : "_FogColor1";
						Color appliedColor = GradientGetSingleColor(distanceColorSource.gradient);
						material.SetColor(colorPickerIdentifier, appliedColor);
						material.SetInt(textureSourceIdentifier, 0);
					}
					else
					{
						string colorTextureIdentifier = fogType == FogTypePass.HeightOnly ? "_FogColorTexture0" : "_FogColorTexture1";
						material.SetTexture(colorTextureIdentifier, heightFog.colorSelectionType == ColorSelectionType.Gradient ? heightColorTexture : heightColorSource.colorRamp);
						material.SetInt(textureSourceIdentifier, 1);
					}
				}
			}

			// Set distance fog properties
			if (distanceFog.enabled)
			{
				SetDistanceFogUniforms();
			}

			// Set height fog properties
			if (heightFog.enabled)
			{
				SetHeightFogUniforms();
			}

			return fogType;
		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			FogTypePass fogType = SetMaterialUniforms();
			if (fogType == FogTypePass.None)
				Graphics.Blit(source, destination);
			else
				Graphics.Blit(source, destination, material, (int)fogType);
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

		public void BakeFogColor(Texture2D target, Gradient gradient)
		{
			if (target == null)
			{
				return;
			}

			float fWidth = target.width;
			Color[] pixels = new Color[target.width];

			for (float i = 0f; i <= 1f; i += 1f / fWidth)
			{
				Color color = gradient.Evaluate(i);
				pixels[(int)Mathf.Floor(i * (fWidth - 1f))] = color;
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
