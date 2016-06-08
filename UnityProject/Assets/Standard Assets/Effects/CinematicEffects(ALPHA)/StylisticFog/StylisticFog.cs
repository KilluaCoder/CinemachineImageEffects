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

		public enum FogMode
		{
			Distance,
			Height
		}

		[Serializable]
		public struct FogSettings
		{
			// Cureves for the color texture
			[SerializeField]
			[Tooltip("Determines the opacity based on fog intensity.")]
			public AnimationCurve fogOpacityCurve;

			[SerializeField]
			[Tooltip("Red component of fog color, based on fog intensity.")]
			public AnimationCurve fogColorR;

			[SerializeField]
			[Tooltip("Green component of fog color, based on fog intensity.")]
			public AnimationCurve fogColorG;

			[SerializeField]
			[Tooltip("Blue component of fog color, based on fog intensity.")]
			public AnimationCurve fogColorB;

			[SerializeField]
			[Tooltip("Fog is excluded from distances closer than this.")]
			public float startDist;

			[SerializeField]
			[Tooltip("Fog is fully saturated beyond this distance.")]
			public float endDist;

			[SerializeField]
			[Tooltip("Wheter to apply fog to the skybox")]
			public bool fogSkybox;

			[SerializeField]
			[Tooltip("The type of fog to apply")]
			public FogMode fogMode;

			[SerializeField]
			[Tooltip("Height where the fog starts")]
			public float baseHeight;

			[SerializeField]
			[Tooltip("Fog density at fog altitude given by height.")]
			public float baseDensity;

			[SerializeField]
			[Tooltip("The rate at which the thickness of the fog decays with altitude")]
			[Range(-1f,1f)]
			public float densityFalloff;

			[SerializeField]
			[Tooltip("Density of fog based on height")]
			public AnimationCurve fogFactorIntensityCurve;

			public static FogSettings defaultSettings()
			{
				return new FogSettings()
				{
					fogOpacityCurve = AnimationCurve.Linear(0.25f, 0.25f, 0.75f, 0.75f),
					fogColorR = AnimationCurve.Linear(0.25f, 0.25f, 0.75f, 0.75f),
					fogColorG = AnimationCurve.Linear(0.25f, 0.25f, 0.75f, 0.75f),
					fogColorB = AnimationCurve.Linear(0.25f, 0.25f, 0.75f, 0.75f),
					startDist = 0f,
					endDist = 200f,
					fogSkybox = true,
					fogMode = FogMode.Distance,
					baseHeight = 0f,
					baseDensity = 0.1f,
					fogFactorIntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
					densityFalloff = 0.5f,
				};
			}
		}

		[SerializeField]
		public FogSettings settings = FogSettings.defaultSettings();

		public Texture2D m_FogPropertyTexture;
		public Texture2D fogPropertyTexture
		{
			get
			{
				if (m_FogPropertyTexture == null)
				{
					m_FogPropertyTexture = new Texture2D(1024, 1, TextureFormat.ARGB32, false, false)
					{
						name = "Fog property",
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
					};
					BakeFogProperty();
				}
				return m_FogPropertyTexture;
			}
		}

		public Texture2D m_FogHeightDensityTexture;
		public Texture2D fogHeightDensityTexture
		{
			get
			{
				if (m_FogHeightDensityTexture == null)
				{
					m_FogHeightDensityTexture = new Texture2D(1024, 1, TextureFormat.ARGB32, false, false)
					{
						name = "Fog Height density",
						wrapMode = TextureWrapMode.Clamp,
						filterMode = FilterMode.Bilinear,
						anisoLevel = 0,
					};
					BakeFogIntensity();
				}
				return m_FogHeightDensityTexture;
			}
		}

		public Vector4 fogPlane = new Vector4(0f, 1f, 0f, 0f);

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


		#region Private Members
		private void OnEnable()
		{
			if (!ImageEffectHelper.IsSupported(shader, true, false, this))
				enabled = false;

			GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;

			BakeFogIntensity();
			BakeFogProperty();
		}

		private void OnDisable()
		{
			if (m_Material != null)
				DestroyImmediate(m_Material);

			if (m_FogPropertyTexture != null)
				DestroyImmediate(m_FogPropertyTexture);

			if (m_FogPropertyTexture != null)
				DestroyImmediate(m_FogHeightDensityTexture);

			m_Material = null;
		}

		private void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			// Set variables that need no processing
			Matrix4x4 inverseViewMatrix = GetComponent<Camera>().cameraToWorldMatrix;
			material.SetTexture("_FogPropertyTexture", fogPropertyTexture);
			material.SetTexture("_FogHeightDensityTexture", fogHeightDensityTexture);

			material.SetMatrix("_InverseViewMatrix", inverseViewMatrix);
			material.SetFloat("_FogStartDist", settings.startDist);
			material.SetFloat("_FogEndDist", settings.endDist);

			// Decide wheter the skybox is included in by the fog
			if (settings.fogSkybox)
				material.DisableKeyword("OMMIT_SKYBOX");
			else
				material.EnableKeyword("OMMIT_SKYBOX");

			// Set height specific parameters
			if(settings.fogMode == FogMode.Height)
			{
				material.EnableKeyword("USE_HEIGHT");

				material.SetFloat("_Height", settings.baseHeight);
				material.SetFloat("_BaseDensity", settings.baseDensity);
				material.SetFloat("_DensityFalloff", settings.densityFalloff);
			}
			else
			{
				material.DisableKeyword("USE_HEIGHT");
			}

			Graphics.Blit(source, destination, material);
		}

		public void BakeFogProperty()
		{
			if(m_FogPropertyTexture == null)
			{
				return;
			}

			Color[] pixels = new Color[1024];

			for (float i = 0f; i <= 1f; i += 1f / 1024f)
			{
				float r = Mathf.Clamp(settings.fogColorR.Evaluate(i), 0f, 1f);
				float g = Mathf.Clamp(settings.fogColorG.Evaluate(i), 0f, 1f);
				float b = Mathf.Clamp(settings.fogColorB.Evaluate(i), 0f, 1f);
				float a = Mathf.Clamp(settings.fogOpacityCurve.Evaluate(i), 0f, 1f);
				pixels[(int)Mathf.Floor(i * 1023f)] = new Color(r, g, b, a);
			}

			m_FogPropertyTexture.SetPixels(pixels);
			m_FogPropertyTexture.Apply();
		}

		public void BakeFogIntensity()
		{
			if (m_FogHeightDensityTexture == null)
			{
				return;
			}

			Color[] pixels = new Color[1024];
            
			for (float i = 0f; i <= 1f; i += 1f / 1024f)
			{
				int index = (int)Mathf.Floor(i * 1023f);
				float density = settings.fogFactorIntensityCurve.Evaluate(i);
				density = (density == 1f) ? 0.9999f : density;
				pixels[index] = EncodeFloatAsColor(density);
			}

			m_FogHeightDensityTexture.SetPixels(pixels);
			m_FogHeightDensityTexture.Apply();
		}

		public void correctStartEndDistances()
		{
			if (settings.endDist > settings.startDist)
			{
				settings.startDist = settings.endDist + 0.1f;
			}
		}

		// From http://aras-p.info/blog/2009/07/30/encoding-floats-to-rgba-the-final/
		private Color EncodeFloatAsColor(float f)
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
		private float DecodeFloatFromColor(Color col)
		{
			Vector4 colorAsVec = new Vector4(col.r, col.g, col.b, col.a);
			Vector4 temp = new Vector4(1.0f, 1.0f / 255.0f, 1.0f / 65025.0f, 1.0f / 160581375.0f);
			return Vector4.Dot(colorAsVec, temp);
		}
		#endregion


	}
}
