using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Cinematic/Bloom")]
    public class Bloom : MonoBehaviour
    {
        #region Public Properties

        /// Prefilter threshold value
        public float threshold {
            get { return m_threshold; }
            set { m_threshold = value; }
        }

        [SerializeField, Range(0, 1)]
        [Tooltip("Filters out pixels under this level of brightness.")]
        float m_threshold = 0.0f;

        /// Prefilter exposure value
        public float exposure {
            get { return m_exposure; }
            set { m_exposure = value; }
        }

        [SerializeField, Range(0, 1)]
        [Tooltip("Sensitivity of the effect\n(0=less sensitive, 1=fully sensitive).")]
        float m_exposure = 0.3f;

        /// Bloom radius
        public float radius {
            get { return m_radius; }
            set { m_radius = value; }
        }

        [SerializeField, Range(0, 5)]
        [Tooltip("Changes extent of veiling effects in a screen resolution-independent fashion.")]
        float m_radius = 2;

        /// Blend factor of result image
        public float intensity {
            get { return m_intensity; }
            set { m_intensity = value; }
        }

        [SerializeField, Range(0, 2)]
        [Tooltip("Blend factor of the result image.")]
        float m_intensity = 1.0f;

        /// Quality level options
        public QualityLevel quality {
            get { return m_quality; }
            set { m_quality = value; }
        }

        [SerializeField]
        [Tooltip("Resolution of temporary render textures.")]
        QualityLevel m_quality = QualityLevel.Normal;

        public enum QualityLevel {
            Low, Normal
        }

        /// Anti-flicker median filter
        [SerializeField]
        [Tooltip("Reduces flashing noise with a median filter.")]
        bool m_antiFlicker = false;

        public bool antiFlicker {
            get { return m_antiFlicker; }
            set { m_antiFlicker = value; }
        }

        #endregion

        #region Private Members

        [SerializeField, HideInInspector]
        Shader m_shader;

        Material m_material;

        void OnEnable()
        {
            const string shaderName = "Hidden/Image Effects/Cinematic/Bloom";
            m_material = new Material(m_shader ? m_shader : Shader.Find(shaderName));
            m_material.hideFlags = HideFlags.DontSave;
        }

        void OnDisable()
        {
            DestroyImmediate(m_material);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var isGamma = QualitySettings.activeColorSpace == ColorSpace.Gamma;

            // source texture size (half it when in the low quality mode)
            var tw = source.width;
            var th = source.height;

            if (m_quality == QualityLevel.Low)
            {
                tw /= 2;
                th /= 2;
            }

            // determine the iteration count
            var logh = Mathf.Log(th, 2) + m_radius - 6;
            var logh_i = (int)logh;
            var iteration = Mathf.Max(2, logh_i);

            // update the shader properties
            m_material.SetFloat("_Threshold", m_threshold);

            var pfc = -Mathf.Log(Mathf.Lerp(1e-2f, 1 - 1e-5f, m_exposure), 10);
            m_material.SetFloat("_Cutoff", m_threshold + pfc * 10);

            var pfo = m_quality == QualityLevel.Low && m_antiFlicker;
            m_material.SetFloat("_PrefilterOffs", pfo ? -0.5f : 0.0f);

            m_material.SetFloat("_SampleScale", 0.5f + logh - logh_i);
            m_material.SetFloat("_Intensity", m_intensity);

            if (m_antiFlicker)
                m_material.EnableKeyword("PREFILTER_MEDIAN");
            else
                m_material.DisableKeyword("PREFILTER_MEDIAN");

            if (isGamma)
            {
                m_material.DisableKeyword("LINEAR_COLOR");
                m_material.EnableKeyword("GAMMA_COLOR");
            }
            else
            {
                m_material.EnableKeyword("LINEAR_COLOR");
                m_material.DisableKeyword("GAMMA_COLOR");
            }

            // allocate temporary buffers
            var rt1 = new RenderTexture[iteration + 1];
            var rt2 = new RenderTexture[iteration + 1];

            for (var i = 0; i < iteration + 1; i++)
            {
                rt1[i] = RenderTexture.GetTemporary(tw, th, 0, source.format);
                if (i > 0 && i < iteration)
                    rt2[i] = RenderTexture.GetTemporary(tw, th, 0, source.format);
                tw /= 2;
                th /= 2;
            }

            // apply the prefilter
            Graphics.Blit(source, rt1[0], m_material, 0);

            // create a mip pyramid
            for (var i = 0; i < iteration; i++)
                Graphics.Blit(rt1[i], rt1[i + 1], m_material, 1);

            // blur and combine loop
            m_material.SetTexture("_BaseTex", rt1[iteration - 1]);
            Graphics.Blit(rt1[iteration], rt2[iteration - 1], m_material, 2);

            for (var i = iteration - 1; i > 1; i--)
            {
                m_material.SetTexture("_BaseTex", rt1[i - 1]);
                Graphics.Blit(rt2[i],  rt2[i - 1], m_material, 2);
            }

            // finish process
            m_material.SetTexture("_BaseTex", source);
            Graphics.Blit(rt2[1], destination, m_material, 3);

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
