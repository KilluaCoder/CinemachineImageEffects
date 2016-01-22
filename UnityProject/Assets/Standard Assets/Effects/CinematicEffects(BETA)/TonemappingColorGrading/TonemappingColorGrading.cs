// Comment/uncomment the following line to show debugging features
#define INTERNAL_DEBUG

namespace UnityStandardAssets.CinematicEffects
{
    using UnityEngine;
    using UnityEngine.Events;
    using System;
    
    // TODO: Remove debugging stuff
    // TODO: Histogram optimizations, see HistogramCompute.compute

    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Color Adjustments/Tonemapping and Color Grading")]
    public class TonemappingColorGrading : MonoBehaviour
    {
#if UNITY_EDITOR
        // EDITOR ONLY call for allowing the editor to update the histogram
        public UnityAction<RenderTexture> OnFrameEndEditorOnly;

        [SerializeField]
        public ComputeShader HistogramComputeShader;

        [SerializeField]
        public Shader HistogramShader;

        [SerializeField]
        public bool HistogramRefreshOnPlay = true;
#endif

        #region Attributes
        [AttributeUsage(AttributeTargets.Field)]
        public class SettingsGroup : Attribute
        {
        }

        public class IndentedGroup : PropertyAttribute
        {
        }

        public class ChannelMixer : PropertyAttribute
        {
        }

        public class ColorWheelGroup : PropertyAttribute
        {
            public int minSizePerWheel = 60;
            public int maxSizePerWheel = 150;

            public ColorWheelGroup()
            {
            }

            public ColorWheelGroup(int minSizePerWheel, int maxSizePerWheel)
            {
                this.minSizePerWheel = minSizePerWheel;
                this.maxSizePerWheel = maxSizePerWheel;
            }
        }
        #endregion

        #region Settings
        [Serializable]
        public struct EyeAdaptationSettings
        {
            public bool Enabled;

            [Min(0f), Tooltip("Midpoint Adjustment.")]
            public float MiddleGrey;

            [Tooltip("The lowest possible exposure value; adjust this value to modify the brightest areas of your level.")]
            public float Min;

            [Tooltip("The highest possible exposure value; adjust this value to modify the darkest areas of your level.")]
            public float Max;

            [Min(0f), Tooltip("Speed of linear adaptation. Higher is faster.")]
            public float Speed;

            public static EyeAdaptationSettings DefaultSettings()
            {
                return new EyeAdaptationSettings
                {
                    Enabled = false,
                    MiddleGrey = 0.5f,
                    Min = -3f,
                    Max = 3f,
                    Speed = 1.5f
                };
            }
        }

        [Serializable]
        public struct TonemappingSettings
        {
            public bool Enabled;

            [Min(0f), Tooltip("Adjusts the overall exposure of the scene.")]
            public float Exposure;

            public static TonemappingSettings DefaultSettings()
            {
                return new TonemappingSettings
                {
                    Enabled = false,
                    Exposure = 1f
                };
            }
        }

        [Serializable]
        public struct LUTSettings
        {
            public bool Enabled;

            [Tooltip("Custom lookup texture (strip format, e.g. 256x16).")]
            public Texture Texture;

            [Range(0f, 1f), Tooltip("Blending factor.")]
            public float Contribution;

            public static LUTSettings DefaultSettings()
            {
                return new LUTSettings
                {
                    Enabled = false,
                    Texture = null,
                    Contribution = 1f
                };
            }
        }

        [Serializable]
        public struct ColorWheelsSettings
        {
            [ColorUsage(false)]
            public Color Shadows;

            [ColorUsage(false)]
            public Color Midtones;

            [ColorUsage(false)]
            public Color Highlights;

            public static ColorWheelsSettings DefaultSettings()
            {
                return new ColorWheelsSettings
                {
                    Shadows = Color.white,
                    Midtones = Color.white,
                    Highlights = Color.white
                };
            }
        }

        [Serializable]
        public struct BasicsSettings
        {
            [Range(-0.5f, 0.5f), Tooltip("Shift the hue of all colors.")]
            public float Hue;

            [Range(0f, 2f), Tooltip("Pushes the intensity of all colors.")]
            public float Saturation;

            [Range(0f, 5f), Tooltip("Brightens or darkens all colors.")]
            public float Value;

            [Space, Range(0f, 2f), Tooltip("Expands or shrinks the overall range of tonal values.")]
            public float Contrast;

            [Range(-1f, 1f), Tooltip("Adjusts the saturation so that clipping is minimized as colors approach full saturation.")]
            public float Vibrance;

            public static BasicsSettings DefaultSettings()
            {
                return new BasicsSettings
                {
                    Contrast = 1f,
                    Hue = 0f,
                    Saturation = 1f,
                    Value = 1f,
                    Vibrance = 0f
                };
            }
        }

        [Serializable]
        public struct ChannelMixerSettings
        {
            public int CurrentChannel;
            public Vector3[] Channels;

            public static ChannelMixerSettings DefaultSettings()
            {
                return new ChannelMixerSettings
                {
                    CurrentChannel = 0,
                    Channels = new Vector3[]
                    {
                        new Vector3(1f, 0f, 0f),
                        new Vector3(0f, 1f, 0f),
                        new Vector3(0f, 0f, 1f)
                    }
                };
            }
        }

        [Serializable]
        public struct CurvesSettings
        {
            public AnimationCurve Master;
            public AnimationCurve Red;
            public AnimationCurve Green;
            public AnimationCurve Blue;

            public static CurvesSettings DefaultSettings()
            {
                return new CurvesSettings
                {
                    Master = DefaultCurve(),
                    Red = DefaultCurve(),
                    Green = DefaultCurve(),
                    Blue = DefaultCurve()
                };
            }

            public static AnimationCurve DefaultCurve()
            {
                return new AnimationCurve(new Keyframe(0f, 0f, 1f, 1f), new Keyframe(1f, 1f, 1f, 1f));
            }
        }

        [Serializable]
        public struct ColorGradingSettings
        {
            public bool Enabled;

            [ColorWheelGroup]
            public ColorWheelsSettings ColorWheels;

            [Space, IndentedGroup]
            public BasicsSettings Basics;

            [Space, ChannelMixer]
            public ChannelMixerSettings ChannelMixer;

            [Space, IndentedGroup]
            public CurvesSettings Curves;

            public static ColorGradingSettings DefaultSettings()
            {
                return new ColorGradingSettings
                {
                    Enabled = false,
                    ColorWheels = ColorWheelsSettings.DefaultSettings(),
                    Basics = BasicsSettings.DefaultSettings(),
                    ChannelMixer = ChannelMixerSettings.DefaultSettings(),
                    Curves = CurvesSettings.DefaultSettings()
                };
            }

            public void Reset()
            {
                Curves = CurvesSettings.DefaultSettings();
            }
        }

        [SerializeField, SettingsGroup]
        private EyeAdaptationSettings m_EyeAdaptation = EyeAdaptationSettings.DefaultSettings();
        public EyeAdaptationSettings EyeAdaptation
        {
            get { return m_EyeAdaptation; }
            set { m_EyeAdaptation = value; }
        }

        [SerializeField, SettingsGroup]
        private TonemappingSettings m_Tonemapping = TonemappingSettings.DefaultSettings();
        public TonemappingSettings Tonemapping
        {
            get { return m_Tonemapping; }
            set { m_Tonemapping = value; }
        }

        [SerializeField, SettingsGroup]
        private LUTSettings m_LUT = LUTSettings.DefaultSettings();
        public LUTSettings LUT
        {
            get { return m_LUT; }
            set
            {
                m_LUT = value;
                SetDirty();
            }
        }

        [SerializeField, SettingsGroup]
        private ColorGradingSettings m_ColorGrading = ColorGradingSettings.DefaultSettings();
        public ColorGradingSettings ColorGrading
        {
            get { return m_ColorGrading; }
            set
            {
                m_ColorGrading = value;
                SetDirty();
            }
        }
        #endregion

        private Texture2D m_IdentityLUT;
        private RenderTexture m_InternalLUT;
        private Texture2D m_CurveTexture;

        private Texture2D IdentityLUT
        {
            get
            {
                if (m_IdentityLUT == null)
                {
                    m_IdentityLUT = GenerateIdentityLUT(kLutSize);
                    m_IdentityLUT.name = "Identity LUT";
                    m_IdentityLUT.filterMode = FilterMode.Bilinear;
                    m_IdentityLUT.anisoLevel = 0;
                    m_IdentityLUT.hideFlags = HideFlags.DontSave;
                }

                return m_IdentityLUT;
            }
        }

        private RenderTexture InternalLUTRT
        {
            get
            {
                if (m_InternalLUT == null)
                {
                    m_InternalLUT = new RenderTexture(kLutSize * kLutSize, kLutSize, 0, RenderTextureFormat.ARGB32);
                    m_InternalLUT.name = "Internal LUT";
                    m_InternalLUT.filterMode = FilterMode.Bilinear;
                    m_InternalLUT.anisoLevel = 0;
                    m_InternalLUT.hideFlags = HideFlags.DontSave;
                }

                return m_InternalLUT;
            }
        }

        private Texture2D CurveTexture
        {
            get
            {
                if (m_CurveTexture == null)
                {
                    m_CurveTexture = new Texture2D(256, 1, TextureFormat.ARGB32, false, true);
                    m_CurveTexture.name = "Curve Texture";
                    m_CurveTexture.wrapMode = TextureWrapMode.Clamp;
                    m_CurveTexture.filterMode = FilterMode.Bilinear;
                    m_CurveTexture.anisoLevel = 0;
                    m_CurveTexture.hideFlags = HideFlags.DontSave;
                }

                return m_CurveTexture;
            }
        }

        public Shader Shader;

        private Material m_Material;
        public Material Material
        {
            get
            {
                if (m_Material == null)
                    m_Material = ImageEffectHelper.CheckShaderAndCreateMaterial(Shader);

                return m_Material;
            }
        }

        public bool IsGammaColorSpace
        {
            get { return QualitySettings.activeColorSpace == ColorSpace.Gamma; }
        }
        
        public readonly int kLutSize = 16;
        public bool ValidRenderTextureFormat { get; private set; }
        public bool ValidUserLUTSize { get; private set; }

        private bool m_Dirty = true;

        private RenderTexture m_SmallAdaptiveRT;
        private RenderTextureFormat m_AdaptiveRTFormat;

#if INTERNAL_DEBUG
        private RenderTexture m_DebugAdaptiveRT;
#endif

        public void SetDirty()
        {
            m_Dirty = true;
        }

        void Start()
        {
            if (!ImageEffectHelper.IsSupported(Shader, false, true, this))
                enabled = false;
        }

        void OnEnable()
        {
            SetDirty();
        }

        void OnDisable()
        {
            if (m_Material != null)
                DestroyImmediate(m_Material);

            if (m_IdentityLUT != null)
                DestroyImmediate(m_IdentityLUT);

            if (m_InternalLUT != null)
                DestroyImmediate(InternalLUTRT);

            if (m_SmallAdaptiveRT != null)
                DestroyImmediate(m_SmallAdaptiveRT);

            if (m_CurveTexture != null)
                DestroyImmediate(m_CurveTexture);

#if INTERNAL_DEBUG
            if (m_DebugAdaptiveRT != null)
                DestroyImmediate(m_DebugAdaptiveRT);
#endif
        }

        void OnValidate()
        {
            SetDirty();
        }

        Texture2D GenerateIdentityLUT(int dim)
        {
            Color[] newC = new Color[dim * dim * dim];
            float oneOverDim = 1f / (dim - 1f);

            for (int i = 0; i < dim; i++)
                for (int j = 0; j < dim; j++)
                    for (int k = 0; k < dim; k++)
                        newC[i + (j * dim) + (k * dim * dim)] = new Color(i * oneOverDim, Mathf.Abs(k * oneOverDim), j * oneOverDim, 1f);
            
            Texture2D tex2D = new Texture2D(dim * dim, dim, TextureFormat.RGB24, false, true);
            tex2D.SetPixels(newC);
            tex2D.Apply();
            return tex2D;
        }

        Color NormalizeColor(Color c)
        {
            float sum = (c.r + c.g + c.b) / 3f;

            if (sum == 0f)
                return new Color(1f, 1f, 1f, 1f);

            Color ret = new Color();
            ret.r = c.r / sum;
            ret.g = c.g / sum;
            ret.b = c.b / sum;
            ret.a = 1f;
            return ret;
        }

        void GenLiftGammaGain(out Color lift, out Color gamma, out Color gain)
        {
            Color nLift = NormalizeColor(ColorGrading.ColorWheels.Shadows);
            Color nGamma = NormalizeColor(ColorGrading.ColorWheels.Midtones);
            Color nGain = NormalizeColor(ColorGrading.ColorWheels.Highlights);

            float avgLift = (nLift.r + nLift.g + nLift.b) / 3f;
            float avgGamma = (nGamma.r + nGamma.g + nGamma.b) / 3f;
            float avgGain = (nGain.r + nGain.g + nGain.b) / 3f;

            // Magic numbers
            float liftScale = 0.1f;
            float gammaScale = 0.5f;
            float gainScale = 0.5f;

            float liftR = (nLift.r - avgLift) * liftScale;
            float liftG = (nLift.g - avgLift) * liftScale;
            float liftB = (nLift.b - avgLift) * liftScale;

            float gammaR = Mathf.Pow(2f, (nGamma.r - avgGamma) * gammaScale);
            float gammaG = Mathf.Pow(2f, (nGamma.g - avgGamma) * gammaScale);
            float gammaB = Mathf.Pow(2f, (nGamma.b - avgGamma) * gammaScale);

            float gainR = Mathf.Pow(2f, (nGain.r - avgGain) * gainScale);
            float gainG = Mathf.Pow(2f, (nGain.g - avgGain) * gainScale);
            float gainB = Mathf.Pow(2f, (nGain.b - avgGain) * gainScale);

            float minGamma = 0.01f;
            float invGammaR = 1f / Mathf.Max(minGamma, gammaR);
            float invGammaG = 1f / Mathf.Max(minGamma, gammaG);
            float invGammaB = 1f / Mathf.Max(minGamma, gammaB);

            lift = new Color(liftR, liftG, liftB);
            gamma = new Color(invGammaR, invGammaG, invGammaB);
            gain = new Color(gainR, gainG, gainB);
        }

        void GenCurveTexture()
        {
            AnimationCurve master = ColorGrading.Curves.Master;
            AnimationCurve red = ColorGrading.Curves.Red;
            AnimationCurve green = ColorGrading.Curves.Green;
            AnimationCurve blue = ColorGrading.Curves.Blue;

            Color[] pixels = new Color[256];

            for (float i = 0f; i <= 1f; i += 1f / 255f)
            {
                float m = Mathf.Clamp(master.Evaluate(i), 0f, 1f);
                float r = Mathf.Clamp(red.Evaluate(i), 0f, 1f);
                float g = Mathf.Clamp(green.Evaluate(i), 0f, 1f);
                float b = Mathf.Clamp(blue.Evaluate(i), 0f, 1f);
                pixels[(int)Mathf.Floor(i * 255f)] = new Color(r, g, b, m);
            }

            CurveTexture.SetPixels(pixels);
            CurveTexture.Apply();
        }

        bool CheckUserLut()
        {
            ValidUserLUTSize = (LUT.Texture.height == Mathf.Sqrt(LUT.Texture.width));
            return ValidUserLUTSize;
        }

        bool CheckSmallAdaptiveRT()
        {
            if (m_SmallAdaptiveRT != null)
                return false;

            m_AdaptiveRTFormat = RenderTextureFormat.ARGBHalf;
            
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf))
                m_AdaptiveRTFormat = RenderTextureFormat.RGHalf;

            m_SmallAdaptiveRT = new RenderTexture(1, 1, 0, m_AdaptiveRTFormat);
            m_SmallAdaptiveRT.hideFlags = HideFlags.DontSave;

#if INTERNAL_DEBUG
            m_DebugAdaptiveRT = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32);
            m_DebugAdaptiveRT.hideFlags = HideFlags.DontSave;
#endif

            return true;
        }

        [ImageEffectTransformsToLDR]
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
#if UNITY_EDITOR
            ValidRenderTextureFormat = true;

            if (source.format != RenderTextureFormat.ARGBHalf && source.format != RenderTextureFormat.ARGBFloat)
                ValidRenderTextureFormat = false;
#endif

            if (IsGammaColorSpace)
                Material.EnableKeyword("GAMMA_COLORSPACE");
            else
                Material.DisableKeyword("GAMMA_COLORSPACE");

            Material.DisableKeyword("ENABLE_EYE_ADAPTATION");
            Material.DisableKeyword("ENABLE_TONEMAPPING");
            Material.DisableKeyword("ENABLE_COLOR_GRADING");

            Texture lutUsed = null;
            float lutContrib = 1f;

            RenderTexture rtSquared = null;
            RenderTexture[] rts = null;

            if (EyeAdaptation.Enabled)
            {
                bool freshlyBrewedSmallRT = CheckSmallAdaptiveRT();
                int srcSize = source.width < source.height ? source.width : source.height;

                // Fast lower or equal power of 2
                int adaptiveSize = srcSize;
                adaptiveSize |= (adaptiveSize >> 1);
                adaptiveSize |= (adaptiveSize >> 2);
                adaptiveSize |= (adaptiveSize >> 4);
                adaptiveSize |= (adaptiveSize >> 8);
                adaptiveSize |= (adaptiveSize >> 16);
                adaptiveSize -= (adaptiveSize >> 1);
                
                rtSquared = RenderTexture.GetTemporary(adaptiveSize, adaptiveSize, 0, m_AdaptiveRTFormat);
                Graphics.Blit(source, rtSquared);

                int downsample = (int)Mathf.Log(rtSquared.width, 2f);

                int div = 2;
                rts = new RenderTexture[downsample];
                for (int i = 0; i < downsample; i++)
                {
                    rts[i] = RenderTexture.GetTemporary(rtSquared.width / div, rtSquared.width / div, 0, m_AdaptiveRTFormat);
                    div <<= 1;
                }

                // Downsample pyramid
                var lumRt = rts[downsample - 1];
                Graphics.Blit(rtSquared, rts[0], Material, 2);
                for (int i = 0; i < downsample - 1; i++)
                {
                    Graphics.Blit(rts[i], rts[i + 1]);
                    lumRt = rts[i + 1];
                }

                // Keeping luminance values between frames, RT restore expected
                m_SmallAdaptiveRT.MarkRestoreExpected();

                Material.SetFloat("_AdaptationSpeed", Mathf.Max(EyeAdaptation.Speed, 0.001f));

#if UNITY_EDITOR
                if (Application.isPlaying && !freshlyBrewedSmallRT)
                    Graphics.Blit(lumRt, m_SmallAdaptiveRT, Material, 3);
                else
                    Graphics.Blit(lumRt, m_SmallAdaptiveRT, Material, 4);
#else
				Graphics.Blit(lumRt, m_SmallAdaptiveRT, Material, freshlyBrewedSmallRT ? 4 : 3);
#endif

                Material.SetFloat("_MiddleGrey", EyeAdaptation.MiddleGrey);
                Material.SetFloat("_AdaptationMin", Mathf.Pow(2f, EyeAdaptation.Min));
                Material.SetFloat("_AdaptationMax", Mathf.Pow(2f, EyeAdaptation.Max));
                Material.SetTexture("_LumTex", m_SmallAdaptiveRT);

#if INTERNAL_DEBUG
                Graphics.Blit(m_SmallAdaptiveRT, m_DebugAdaptiveRT, Material, 5);
#endif

                Material.EnableKeyword("ENABLE_EYE_ADAPTATION");
            }

            if (Tonemapping.Enabled)
            {
                Material.SetFloat("_Exposure", Tonemapping.Exposure);
                Material.EnableKeyword("ENABLE_TONEMAPPING");
            }

            if (LUT.Enabled)
            {
                Texture lut = LUT.Texture;

                if (LUT.Texture == null || !CheckUserLut())
                    lut = IdentityLUT;
                
                lutUsed = lut;
                lutContrib = LUT.Contribution;
                Material.EnableKeyword("ENABLE_COLOR_GRADING");
            }

            if (ColorGrading.Enabled)
            {
                if (m_Dirty)
                {
                    if (!LUT.Enabled || LUT.Texture == null)
                    {
                        Material.SetVector("_UserLutParams", new Vector4(1f / IdentityLUT.width, 1f / IdentityLUT.height, IdentityLUT.height - 1f, LUT.Contribution));
                        Material.SetTexture("_UserLutTex", IdentityLUT);
                    }
                    else
                    {
                        Material.SetVector("_UserLutParams", new Vector4(1f / LUT.Texture.width, 1f / LUT.Texture.height, LUT.Texture.height - 1f, LUT.Contribution));
                        Material.SetTexture("_UserLutTex", LUT.Texture);
                    }

                    Color lift, gamma, gain;
                    GenLiftGammaGain(out lift, out gamma, out gain);
                    GenCurveTexture();

                    Material.SetVector("_Lift", lift);
                    Material.SetVector("_Gamma", gamma);
                    Material.SetVector("_Gain", gain);
                    Material.SetFloat("_Contrast", ColorGrading.Basics.Contrast);
                    Material.SetFloat("_Vibrance", ColorGrading.Basics.Vibrance);
                    Material.SetVector("_HSV", new Vector4(ColorGrading.Basics.Hue, ColorGrading.Basics.Saturation, ColorGrading.Basics.Value));
                    Material.SetVector("_ChannelMixerRed", ColorGrading.ChannelMixer.Channels[0]);
                    Material.SetVector("_ChannelMixerGreen", ColorGrading.ChannelMixer.Channels[1]);
                    Material.SetVector("_ChannelMixerBlue", ColorGrading.ChannelMixer.Channels[2]);
                    Material.SetTexture("_CurveTex", CurveTexture);
                    Graphics.Blit(IdentityLUT, InternalLUTRT, Material, 0);
                    m_Dirty = false;
                }

                lutUsed = InternalLUTRT;
                lutContrib = 1f;
                Material.EnableKeyword("ENABLE_COLOR_GRADING");
            }

            if (lutUsed != null)
            {
                Material.SetTexture("_LutTex", lutUsed);
                Material.SetVector("_LutParams", new Vector4(1f / lutUsed.width, 1f / lutUsed.height, lutUsed.height - 1f, lutContrib));
            }

            Graphics.Blit(source, destination, Material, 1);

            // Cleanup for eye adaptation
            if (EyeAdaptation.Enabled)
            {
                for (int i = 0; i < rts.Length; i++)
                    RenderTexture.ReleaseTemporary(rts[i]);

                RenderTexture.ReleaseTemporary(rtSquared);
            }

#if UNITY_EDITOR
            // If we have an on frame end callabck we need to pass a valid result texture
            // if destination is null we wrote to the backbuffer so we need to copy that out.
            // It's slow and not amazing, but editor only
            if (OnFrameEndEditorOnly != null)
            {
                if (destination == null)
                {
                    RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0);
                    Graphics.Blit(source, rt, Material, 1);
                    OnFrameEndEditorOnly(rt);
                    RenderTexture.ReleaseTemporary(rt);
                    RenderTexture.active = null;
                }
                else
                {
                    OnFrameEndEditorOnly(destination);
                }
            }
#endif

#if INTERNAL_DEBUG
            int yoffset = 0;

            if (m_InternalLUT != null && ColorGrading.Enabled)
            {
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
                Graphics.DrawTexture(new Rect(0f, yoffset, kLutSize * kLutSize, kLutSize), InternalLUTRT);
                GL.PopMatrix();
                yoffset += 16;
            }

            if (m_CurveTexture != null && ColorGrading.Enabled)
            {
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
                Graphics.DrawTexture(new Rect(0f, yoffset, m_CurveTexture.width, 4f), m_CurveTexture);
                GL.PopMatrix();
                yoffset += 4;
            }

            if (m_DebugAdaptiveRT != null && EyeAdaptation.Enabled)
            {
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
                Graphics.DrawTexture(new Rect(0f, yoffset, 64f, 64f), m_DebugAdaptiveRT);
                GL.PopMatrix();
                yoffset += 64;
            }
#endif
        }
    }
}
