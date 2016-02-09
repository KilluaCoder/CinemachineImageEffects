using UnityEngine;
using System;

namespace UnityStandardAssets.CinematicEffects
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Cinematic/Lens Aberrations")]
    public class LensAberrations : MonoBehaviour
    {
        #region Attributes
        [AttributeUsage(AttributeTargets.Field)]
        public class SettingsGroup : Attribute
        {}

        [AttributeUsage(AttributeTargets.Field)]
        public class AdvancedSetting : Attribute
        {}
        #endregion

        #region Settings
        public enum ChromaticAberrationMode
        {
            Simple,
            Advanced
        }

        [Serializable]
        public struct VignetteSettings
        {
            public bool enabled;

            [Tooltip("Vignette color. Use the alpha channel for transparency.")]
            public Color color;

            [Range(0f, 3f), Tooltip("Amount of vignetting on screen.")]
            public float intensity;

            [Range(0.1f, 3f), Tooltip("Smoothness of the vignette borders.")]
            public float smoothness;

            [Range(0f, 1f), Tooltip("Blurs the corners of the screen. Leave this at 0 to disable it.")]
            public float blur;

            [Range(0f, 1f), Tooltip("Desaturate the corners of the screen. Leave this to 0 to disable it.")]
            public float desaturate;

            public static VignetteSettings defaultSettings
            {
                get
                {
                    return new VignetteSettings
                           {
                               enabled = false,
                               color = Color.black,
                               intensity = 1.2f,
                               smoothness = 1.5f,
                               blur = 0f,
                               desaturate = 0f
                           };
                }
            }
        }

        [Serializable]
        public struct ChromaticAberrationSettings
        {
            public bool enabled;

            [Tooltip("Use the \"Advanced\" mode if you need more control over the chromatic aberrations at the expense of performances.")]
            public ChromaticAberrationMode mode;

            [Range(-2f, 2f)]
            public float tangential;

            [AdvancedSetting, Range(0f, 2f)]
            public float axial;

            [AdvancedSetting, Range(0f, 2f)]
            public float contrastDependency;

            public static ChromaticAberrationSettings defaultSettings
            {
                get
                {
                    return new ChromaticAberrationSettings
                           {
                               enabled = false,
                               mode = ChromaticAberrationMode.Simple,
                               tangential = 0f,
                               axial = 0f,
                               contrastDependency = 0f
                           };
                }
            }
        }
        #endregion

        [SettingsGroup]
        public VignetteSettings vignette = VignetteSettings.defaultSettings;

        [SettingsGroup]
        public ChromaticAberrationSettings chromaticAberration = ChromaticAberrationSettings.defaultSettings;

        private enum Pass
        {
            BlurPrePass,
            Simple,
            Desaturate,
            Blur,
            BlurDesaturate,
            ChromaticAberrationOnly
        }

        [SerializeField]
        private Shader m_Shader;
        public Shader shader
        {
            get
            {
                if (m_Shader == null)
                    m_Shader = Shader.Find("Hidden/LensAberrations");

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

        private void OnEnable()
        {
            if (!ImageEffectHelper.IsSupported(shader, false, false, this))
                enabled = false;
        }

        private void OnDisable()
        {
            if (m_Material != null)
                DestroyImmediate(m_Material);

            m_Material = null;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!vignette.enabled && !chromaticAberration.enabled)
            {
                Graphics.Blit(source, destination);
                return;
            }

            material.SetVector("_Vignette", new Vector4(vignette.intensity, vignette.smoothness, vignette.blur, 1f - vignette.desaturate));
            material.SetColor("_VignetteColor", vignette.color);

            material.DisableKeyword("CHROMATIC_SIMPLE");
            material.DisableKeyword("CHROMATIC_ADVANCED");

            if (chromaticAberration.enabled && chromaticAberration.tangential != 0f)
            {
                if (chromaticAberration.mode == ChromaticAberrationMode.Advanced)
                    material.EnableKeyword("CHROMATIC_ADVANCED");
                else
                    material.EnableKeyword("CHROMATIC_SIMPLE");

                Vector4 chromaParams = new Vector4(2.5f * chromaticAberration.tangential, 5f * chromaticAberration.axial, 5f / Mathf.Max(Mathf.Epsilon, chromaticAberration.contrastDependency), 5f);
                material.SetVector("_ChromaticAberration", chromaParams);
            }

            if (vignette.enabled && vignette.blur > 0f)
            {
                // Downscale + gaussian blur (2 passes)
                int w = source.width / 2;
                int h = source.height / 2;
                RenderTexture tmp1 = RenderTexture.GetTemporary(w, h, 0, source.format);
                RenderTexture tmp2 = RenderTexture.GetTemporary(w, h, 0, source.format);

                material.SetVector("_BlurPass", new Vector2(1f / w, 0f));
                Graphics.Blit(source, tmp1, material, (int)Pass.BlurPrePass);
                material.SetVector("_BlurPass", new Vector2(0f, 1f / h));
                Graphics.Blit(tmp1, tmp2, material, (int)Pass.BlurPrePass);

                material.SetVector("_BlurPass", new Vector2(1f / w, 0f));
                Graphics.Blit(tmp2, tmp1, material, (int)Pass.BlurPrePass);
                material.SetVector("_BlurPass", new Vector2(0f, 1f / h));
                Graphics.Blit(tmp1, tmp2, material, (int)Pass.BlurPrePass);

                material.SetTexture("_BlurTex", tmp2);

                if (vignette.desaturate > 0f)
                    Graphics.Blit(source, destination, material, (int)Pass.BlurDesaturate);
                else
                    Graphics.Blit(source, destination, material, (int)Pass.Blur);

                RenderTexture.ReleaseTemporary(tmp2);
                RenderTexture.ReleaseTemporary(tmp1);
            }
            else if (vignette.enabled && vignette.desaturate > 0f)
            {
                Graphics.Blit(source, destination, material, (int)Pass.Desaturate);
            }
            else if (vignette.enabled)
            {
                Graphics.Blit(source, destination, material, (int)Pass.Simple);
            }
            else
            {
                Graphics.Blit(source, destination, material, (int)Pass.ChromaticAberrationOnly);
            }
        }
    }
}
