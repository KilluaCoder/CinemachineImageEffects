using System;
using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Cinematic/Bloom")]
    public class Bloom : MonoBehaviour
    {
        [Serializable]
        public struct Settings
        {
            [SerializeField, Range(0, 1)]
            [Tooltip("Filters out pixels under this level of brightness.")]
            public float threshold;

            [SerializeField, Range(0, 1)]
            [Tooltip("Sensitivity of the effect\n(0=less sensitive, 1=fully sensitive).")]
            public float exposure;

            [SerializeField, Range(0, 5)]
            [Tooltip("Changes extent of veiling effects in a screen resolution-independent fashion.")]
            public float radius;

            [SerializeField, Range(0, 2)]
            [Tooltip("Blend factor of the result image.")]
            public float intensity;

            [SerializeField]
            [Tooltip("Controls filter quality and buffer resolution.")]
            public bool highQuality;

            [SerializeField]
            [Tooltip("Reduces flashing noise with a median filter.")]
            public bool antiFlicker;

            public static Settings defaultSettings
            {
                get
                {
                    var settings = new Settings
                    {
                        threshold = 0.9f,
                        exposure = 0.3f,
                        radius = 2.0f,
                        intensity = 1.0f,
                        highQuality = true,
                        antiFlicker = false
                    };
                    return settings;
                }
            }
        }

        #region Public Properties

        [SerializeField]
        public Settings settings = Settings.defaultSettings;

        #endregion

        [SerializeField, HideInInspector]
        private Shader m_Shader;

        public Shader shader
        {
            get
            {
                if (m_Shader == null)
                {
                    const string shaderName = "Hidden/Image Effects/Cinematic/Bloom";
                    m_Shader = Shader.Find(shaderName);
                    m_Shader.hideFlags = HideFlags.DontSave;
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
        }

        private void OnDisable()
        {
            if (m_Material != null)
            {
                DestroyImmediate(m_Material);
                m_Material = null;
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var useRGBM = Application.isMobilePlatform;
            var isGamma = QualitySettings.activeColorSpace == ColorSpace.Gamma;

            // source texture size
            var tw = source.width;
            var th = source.height;

            // halve the texture size for the low quality mode
            if (!settings.highQuality)
            {
                tw /= 2;
                th /= 2;
            }

            // blur buffer format
            var rtFormat = useRGBM ?
                RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR;

            // determine the iteration count
            var logh = Mathf.Log(th, 2) + settings.radius - 6;
            var logh_i = (int)logh;
            var iteration = Mathf.Max(2, logh_i);

            // update the shader properties
            material.SetFloat("_Threshold", settings.threshold);

            var pfc = -Mathf.Log(Mathf.Lerp(1e-2f, 1 - 1e-5f, settings.exposure), 10);
            material.SetFloat("_Cutoff", settings.threshold + pfc * 10);

            var pfo = !settings.highQuality && settings.antiFlicker;
            material.SetFloat("_PrefilterOffs", pfo ? -0.5f : 0.0f);

            material.SetFloat("_SampleScale", 0.5f + logh - logh_i);
            material.SetFloat("_Intensity", settings.intensity);

            if (settings.highQuality)
                material.EnableKeyword("HIGH_QUALITY");
            else
                material.DisableKeyword("HIGH_QUALITY");

            if (settings.antiFlicker)
                material.EnableKeyword("PREFILTER_MEDIAN");
            else
                material.DisableKeyword("PREFILTER_MEDIAN");

            if (isGamma)
            {
                material.DisableKeyword("LINEAR_COLOR");
                material.EnableKeyword("GAMMA_COLOR");
            }
            else
            {
                material.EnableKeyword("LINEAR_COLOR");
                material.DisableKeyword("GAMMA_COLOR");
            }

            // allocate temporary buffers
            var rt1 = new RenderTexture[iteration + 1];
            var rt2 = new RenderTexture[iteration + 1];

            for (var i = 0; i < iteration + 1; i++)
            {
                rt1[i] = RenderTexture.GetTemporary(tw, th, 0, rtFormat);
                if (i > 0 && i < iteration)
                    rt2[i] = RenderTexture.GetTemporary(tw, th, 0, rtFormat);
                tw /= 2;
                th /= 2;
            }

            // apply the prefilter
            Graphics.Blit(source, rt1[0], material, 0);

            // create a mip pyramid
            for (var i = 0; i < iteration; i++)
                Graphics.Blit(rt1[i], rt1[i + 1], material, 1);

            // blur and combine loop
            material.SetTexture("_BaseTex", rt1[iteration - 1]);
            Graphics.Blit(rt1[iteration], rt2[iteration - 1], material, 2);

            for (var i = iteration - 1; i > 1; i--)
            {
                material.SetTexture("_BaseTex", rt1[i - 1]);
                Graphics.Blit(rt2[i], rt2[i - 1], material, 2);
            }

            // finish process
            material.SetTexture("_BaseTex", source);
            Graphics.Blit(rt2[1], destination, material, 3);

            // release the temporary buffers
            for (var i = 0; i < iteration + 1; i++)
            {
                RenderTexture.ReleaseTemporary(rt1[i]);
                RenderTexture.ReleaseTemporary(rt2[i]);
            }
        }

        #endregion
    }
}
