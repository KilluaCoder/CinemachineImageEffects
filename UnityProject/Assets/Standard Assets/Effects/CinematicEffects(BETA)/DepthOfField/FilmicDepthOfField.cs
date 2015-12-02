using UnityEngine;
using System;

namespace UnityStandardAssets.ImageEffects
{
    //Improvement ideas:
    //  In hdr do local tonemapping/inverse tonemapping to stabilize bokeh.
    //  Use ldr buffer in ldr + See what pass can go with ldr buffer in hdr (in correlation to previous point and remapping coc from -1/0/1 to 0/0.5/1)
    //  Use temporal stabilisation.
    //  Optimize when near and far blur are the same.
    //  Improve quality when near and far blur are not the same (perf ?)
    //  Improve bokeh quality in low setting by using swirl effect on the samples.
    //  Improve integration of the bokeh texture into the other blur (perf/consistency)
    //  Support different near and far blur amount with the bokeh texture
    //  Use distance field for the bokeh texture.

    //References :
    //  This DOF implementation use ideas from public sources, a big thank to them :
    //  http://www.iryoku.com/next-generation-post-processing-in-call-of-duty-advanced-warfare
    //  http://www.crytek.com/download/Sousa_Graphics_Gems_CryENGINE3.pdf
    //  http://graphics.cs.williams.edu/papers/MedianShaderX6/
    //  http://http.developer.nvidia.com/GPUGems/gpugems_ch24.html
    //  http://vec3.ca/bicubic-filtering-in-fewer-taps/

    [ExecuteInEditMode]
    [AddComponentMenu("Image Effects/Other/FilmicDepthOfField")]
    [RequireComponent(typeof(Camera))]
    public class FilmicDepthOfField : MonoBehaviour
    {
        [AttributeUsage(AttributeTargets.Field)]
        public sealed class GradientRangeAttribute : PropertyAttribute
        {
            public readonly float max;
            public readonly float min;

            // Attribute used to make a float or int variable in a script be restricted to a specific range.
            public GradientRangeAttribute(float min, float max)
            {
                this.min = min;
                this.max = max;
            }
        }

        const float kMaxBlur = 45.0f;

        private enum Passes
        {
            BlurAlphaWeighted                    =  0 ,
            BoxDownsample                        =  1 ,
            BlurFGCocFromColor                   =  2 ,
            BlurFGCoc                            =  3 ,
            CaptureSignedCoc                     =  4 ,
            CaptureSignedCocExplicit             =  5 ,
            VisualizeCoc                         =  6 ,
            VisualizeCocExplicit                 =  7 ,
            CircleSignedCocPrefilter             =  8 ,
            CircleSignedCocBokeh                 =  9 ,
            CircleSignedCocBokehWithDilatedFG    =  10,
            CircleSignedCocBokehLow              =  11,
            CircleSignedCocBokehWithDilatedFGLow =  12,
            SignedCocMerge                       =  13,
            SignedCocMergeExplicit               =  14,
            SignedCocMergeBicubic                =  15,
            SignedCocMergeExplicitBicubic        =  16,
            ShapeLowQuality                      =  17,
            ShapeLowQualityDilateFG              =  18,
            ShapeLowQualityMerge                 =  19,
            ShapeLowQualityMergeDilateFG         =  20,
            ShapeMediumQuality                   =  21,
            ShapeMediumQualityDilateFG           =  22,
            ShapeMediumQualityMerge              =  23,
            ShapeMediumQualityMergeDilateFG      =  24,
            ShapeHighQuality                     =  25,
            ShapeHighQualityDilateFG             =  26,
            ShapeHighQualityMerge                =  27,
            ShapeHighQualityMergeDilateFG        =  28,

            //remove these
            BlurinessAmountVisualisation0        =  29,
            BlurinessAmount                      =  30,
            BlurinessAmountVisualisation2        =  31,
            BlurinessAmountExplicit              =  32,
            CaptureCoc                           =  33,
            CaptureCocExplicit                   =  34,
            Copy                                 =  35,
            Shape0                               =  36,
            Shape1                               =  37,
            Shape2                               =  38,
            Shape3                               =  39,
            Shape4                               =  40,
            Shape5                               =  41,
            Circle0                              =  42,
            Circle1                              =  43,
            Circle2                              =  44,
            Circle3                              =  45,
            BoostThresh                          =  46,
            Boost                                =  47,
            AlphaMask                            =  48,
            BlurBoxBlend                         =  49,
            BlurBox                              =  50
        }

        public enum MedianPasses
        {
            Median3 = 0,
            Median3x3 = 1
        }

        public enum BokehTexturesPasses
        {
            Apply = 2,
            Collect = 3
        }

        public enum UIMode
        {
            Basic,
            Advanced,
            Explicit
        }
        public enum ApertureShape
        {
            Circular,
            CircularOLD,
            Hexagonal,
            HexagonalOLD,
            Octogonal,
            OctogonalOLD,
            BokehTextureOLD
        }
        public enum FilterQuality
        {
            None,
            Normal,
            High
        }

        [Tooltip("Allow to view where the blur will be applied. Yellow for near blur, Blue for far blur.")]
        public bool visualizeBluriness  = false;

        [Tooltip("When enabled quality settings can be hand picked, rather than being driven by the quality slider.")]
        public bool customizeQualitySettings = false;

        public bool  prefilterBlur = true;
        public FilterQuality medianFilter = FilterQuality.High;
        public bool  dilateNearBlur = true;
        public bool  highQualityUpsampling = true;

        [GradientRange(0.0f, 100.0f)]
        [Tooltip("Color represent relative performance. From green (faster) to yellow (slower).")]
        public float quality = 100.0f;

        [Range(0.0f, 1.0f)]
        public float focusPlane  = 0.225f;
        [Range(0.0f, 1.0f)]
        public float focusRange = 0.9f;
        [Range(0.0f, 1.0f)]
        public float nearPlane = 0.0f;
        [Range(0.0f, kMaxBlur)]
        public float nearRadius = 20.0f;
        [Range(0.0f, 1.0f)]
        public float farPlane  = 1.0f;
        [Range(0.0f, kMaxBlur)]
        public float farRadius  = 20.0f;
        [Range(0.0f, kMaxBlur)]
        public float radius = 20.0f;
        [Range(0.5f, 4.0f)]
        public float boostPoint  = 0.75f;
        [Range(0.0f, 1.0f)]
        public float nearBoostAmount  = 0.0f;
        [Range(0.0f, 1.0f)]
        public float farBoostAmount  = 0.0f;
        [Range(0.0f, 32.0f)]
        public float fStops  = 5.0f;

        [Range(0.01f, 5.0f)]
        public float dx11BokehScale = 1.0f;
        [Range(0.01f, 100.0f)]
        public float dx11BokehIntensity = 50.0f;
        [Range(0.01f, 50.0f)]
        public float dx11BokehThreshold = 2.0f;
        [Range(0.01f, 1.0f)]
        public float dx11SpawnHeuristic = 0.15f;

        public Transform focusTransform = null;
        public Texture2D dx11BokehTexture = null;
        public ApertureShape apertureShape = ApertureShape.Circular;
        [Range(0.0f, 179.0f)]
        public float apertureOrientation = 0.0f;

        [Tooltip("Use with care Bokeh texture are only available on shader model 5, and performance scale with the number of bokehs.")]
        public bool useBokehTexture;

        private bool shouldPerformBokeh
        {
            get { return ImageEffectHelper.supportsDX11 && useBokehTexture && dx11BokehMaterial; }
        }

        public UIMode uiMode = UIMode.Basic;

        public Shader filmicDepthOfFieldShader;
        public Shader medianFilterShader;
        public Shader dx11BokehShader;

        private Material m_FilmicDepthOfFieldMaterial;
        private Material m_MedianFilterMaterial;
        private Material m_Dx11BokehMaterial;

        public Material filmicDepthOfFieldMaterial
        {
            get
            {
                if (m_FilmicDepthOfFieldMaterial == null)
                    m_FilmicDepthOfFieldMaterial = ImageEffectHelper.CheckShaderAndCreateMaterial(filmicDepthOfFieldShader);

                return m_FilmicDepthOfFieldMaterial;
            }
        }

        public Material medianFilterMaterial
        {
            get
            {
                if (m_MedianFilterMaterial == null)
                    m_MedianFilterMaterial = ImageEffectHelper.CheckShaderAndCreateMaterial(medianFilterShader);

                return m_MedianFilterMaterial;
            }
        }

        public Material dx11BokehMaterial
        {
            get
            {
                if (m_Dx11BokehMaterial == null)
                    m_Dx11BokehMaterial = ImageEffectHelper.CheckShaderAndCreateMaterial(dx11BokehShader);

                return m_Dx11BokehMaterial;
            }
        }

        public ComputeBuffer computeBufferDrawArgs
        {
            get
            {
                if (m_ComputeBufferDrawArgs == null)
                {
                    m_ComputeBufferDrawArgs = new ComputeBuffer(1, 16, ComputeBufferType.DrawIndirect);
                    var args = new int[4];
                    args[0] = 0;
                    args[1] = 1;
                    args[2] = 0;
                    args[3] = 0;
                    m_ComputeBufferDrawArgs.SetData(args);
                }
                return m_ComputeBufferDrawArgs;
            }
        }

        public ComputeBuffer computeBufferPoints
        {
            get
            {
                if (m_ComputeBufferPoints == null)
                {
                    m_ComputeBufferPoints = new ComputeBuffer(90000, 12 + 16, ComputeBufferType.Append);
                }
                return m_ComputeBufferPoints;
            }
        }

        private ComputeBuffer m_ComputeBufferDrawArgs;
        private ComputeBuffer m_ComputeBufferPoints;

        private float m_LastApertureOrientation;
        private Vector4 m_OctogonalBokehDirection1;
        private Vector4 m_OctogonalBokehDirection2;
        private Vector4 m_OctogonalBokehDirection3;
        private Vector4 m_OctogonalBokehDirection4;
        private Vector4 m_HexagonalBokehDirection1;
        private Vector4 m_HexagonalBokehDirection2;
        private Vector4 m_HexagonalBokehDirection3;


        protected void OnEnable()
        {
            if (filmicDepthOfFieldShader == null)
                filmicDepthOfFieldShader = Shader.Find("Hidden/FilmicDepthOfField");

            if (medianFilterShader == null)
                medianFilterShader = Shader.Find("Hidden/MedianFilter");

            if (dx11BokehShader == null)
                dx11BokehShader = Shader.Find("Hidden/Dof/DX11Dof");

            if (!ImageEffectHelper.IsSupported(filmicDepthOfFieldShader, true, true, this)
                || !ImageEffectHelper.IsSupported(medianFilterShader, true, true, this)
                )
            {
                enabled = false;
                Debug.LogWarning("The image effect " + ToString() + " has been disabled as it's not supported on the current platform.");
                return;
            }

            if (ImageEffectHelper.supportsDX11)
            {
                if (!ImageEffectHelper.IsSupported(dx11BokehShader, true, true, this))
                {
                    enabled = false;
                    Debug.LogWarning("The image effect " + ToString() + " has been disabled as it's not supported on the current platform.");
                    return;
                }
            }

            ComputeBlurDirections(true);
            GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
        }

        void OnDisable()
        {
            ReleaseComputeResources();

            if (m_FilmicDepthOfFieldMaterial)
                DestroyImmediate(m_FilmicDepthOfFieldMaterial);
            if (m_Dx11BokehMaterial)
                DestroyImmediate(m_Dx11BokehMaterial);
            if (m_MedianFilterMaterial)
                DestroyImmediate(m_MedianFilterMaterial);

            m_Dx11BokehMaterial = null;
            m_FilmicDepthOfFieldMaterial = null;
            m_MedianFilterMaterial = null;
        }

        //----------------------------------//
        //TODO remove this section after new path have been validated
        //----------------------------------//
        void DoDX11Bokeh(RenderTexture source, RenderTexture destination)
        {
            float maxRadius = nearRadius > farRadius ? nearRadius : farRadius;
            if (maxRadius == 0.0 || !ImageEffectHelper.supportsDX11)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Setup
            float internalBlurWidth = maxRadius / 2.0f;
            int rtW = source.width;
            int rtH = source.height;
            RenderTexture rtLow = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, source.format);
            RenderTexture dest2 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW / 2, rtH / 2, source.format);
            RenderTexture rtSuperLow1 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW / 2, rtH / 2, source.format);
            RenderTexture rtSuperLow2 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW / 2, rtH / 2, source.format);

            // Blur Map
            Bluriness(source, rtLow);

            Graphics.Blit(source, rtSuperLow1, filmicDepthOfFieldMaterial, (int)Passes.BoxDownsample);
            filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(0.0f, 1.5f, 0.0f, 1.5f));
            Graphics.Blit(rtSuperLow1, rtSuperLow2, filmicDepthOfFieldMaterial, (int)Passes.BlurAlphaWeighted);
            filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(1.5f, 0.0f, 0.0f, 1.5f));
            Graphics.Blit(rtSuperLow2, rtSuperLow1, filmicDepthOfFieldMaterial, (int)Passes.BlurAlphaWeighted);

            float scaleFactor = source.height / 720.0f; //Adjust scale taking 720p as a reference.

            dx11BokehMaterial.SetTexture("_BlurredColor", rtSuperLow1);
            dx11BokehMaterial.SetFloat("_SpawnHeuristic", dx11SpawnHeuristic);
            dx11BokehMaterial.SetVector("_BokehParams", new Vector4(dx11BokehScale * scaleFactor, dx11BokehIntensity, dx11BokehThreshold, internalBlurWidth));
            dx11BokehMaterial.SetTexture("_FgCocMask", null);

            // collect bokeh candidates and replace with a darker pixel
            Graphics.SetRandomWriteTarget(1, computeBufferPoints);
            Graphics.Blit(source, rtLow, dx11BokehMaterial, 0);
            Graphics.ClearRandomWriteTargets();

            Graphics.Blit(rtLow, dest2, filmicDepthOfFieldMaterial, (int)Passes.AlphaMask);

            // box blur
            filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(internalBlurWidth, 0.0f, 0.0f, internalBlurWidth));
            Graphics.Blit(rtLow, source, filmicDepthOfFieldMaterial, (int)(Passes.BlurBox));
            filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(0.0f, internalBlurWidth, 0.0f, internalBlurWidth));
            Graphics.Blit(source, dest2, filmicDepthOfFieldMaterial, (int)(Passes.BlurBoxBlend));

            // apply bokeh candidates
            Graphics.SetRenderTarget(dest2);
            ComputeBuffer.CopyCount(computeBufferPoints, computeBufferDrawArgs, 0);
            dx11BokehMaterial.SetBuffer("pointBuffer", computeBufferPoints);
            dx11BokehMaterial.SetTexture("_MainTex", dx11BokehTexture);
            dx11BokehMaterial.SetVector("_Screen", new Vector3(1.0f / (1.0f * source.width), 1.0f / (1.0f * source.height), internalBlurWidth));
            dx11BokehMaterial.SetPass(2);

            Graphics.DrawProceduralIndirect(MeshTopology.Points, computeBufferDrawArgs, 0);

            Graphics.Blit(dest2, destination, filmicDepthOfFieldMaterial, (int)Passes.Copy);    // hackaround for DX11 high resolution flipfun (OPTIMIZEME)
        }

        void WhiteBoost(RenderTexture source, RenderTexture bluriness, RenderTexture tmp, RenderTexture destination)
        {
            if ((nearBoostAmount == 0.0f && farBoostAmount == 0.0f) || (uiMode == UIMode.Basic))
            {
                Graphics.Blit(source, destination);
            }
            else
            {
                Vector4 blurrinessCoe = new Vector4(nearRadius, farRadius, 0.0f, 0.0f);
                filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
                filmicDepthOfFieldMaterial.SetFloat("_Param1", boostPoint);
                filmicDepthOfFieldMaterial.SetTexture("_MainTex", source);
                filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
                Graphics.Blit(source, tmp, filmicDepthOfFieldMaterial, (int)Passes.BoostThresh);
                filmicDepthOfFieldMaterial.SetTexture("_MainTex", source);
                filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
                filmicDepthOfFieldMaterial.SetTexture("_ThirdTex", tmp);
                Vector4 boostCoe = new Vector4(nearBoostAmount * 0.5f, farBoostAmount * 0.5f, 0.0f, 0.0f);
                filmicDepthOfFieldMaterial.SetVector("_Param0", boostCoe);
                Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, (int)Passes.Boost);
            }
        }

        void Bluriness(RenderTexture source, RenderTexture destination)
        {
            Camera camera = GetComponent<Camera>();
            Vector4 blurrinessParam;
            Vector4 blurrinessCoe;
            if (uiMode == UIMode.Basic || uiMode == UIMode.Advanced)
            {
                float focusDistance01 = focusTransform ? (camera.WorldToViewportPoint(focusTransform.position)).z / (camera.farClipPlane) : (focusPlane * focusPlane * focusPlane * focusPlane);
                float focusRange01 = 0.0f;
                if (uiMode == UIMode.Advanced)
                    focusRange01 = focusRange * focusRange * focusRange * focusRange;
                float focalLength = 4.0f / Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
                float aperture = focalLength / fStops;
                blurrinessCoe = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
                blurrinessParam = new Vector4(aperture, focalLength, focusDistance01, focusRange01);
                filmicDepthOfFieldMaterial.SetVector("_BlurParams", blurrinessParam);
                filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);

                if (visualizeBluriness)
                {
                    Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, (int)Passes.BlurinessAmountVisualisation0);
                }
                else
                {
                    if (apertureShape == ApertureShape.BokehTextureOLD)
                    {
                        //source.MarkRestoreExpected();
                        Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, (int)Passes.CaptureCoc);
                        Graphics.Blit(destination, source);
                    }
                    else
                    {
                        Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, (int)Passes.BlurinessAmount);
                    }
                }
            }
            else
            {
                float focusDistance01 = focusTransform ? (camera.WorldToViewportPoint(focusTransform.position)).z / (camera.farClipPlane) : (focusPlane * focusPlane * focusPlane * focusPlane);
                float nearDistance01 = nearPlane * nearPlane * nearPlane * nearPlane;
                float farDistance01 = farPlane * farPlane * farPlane * farPlane;
                float nearFocusRange01 = focusRange * focusRange * focusRange * focusRange;
                float farFocusRange01 = nearFocusRange01;

                if (focusDistance01 <= nearDistance01)
                    focusDistance01 = nearDistance01 + 0.0000001f;
                if (focusDistance01 >= farDistance01)
                    focusDistance01 = farDistance01 - 0.0000001f;
                if ((focusDistance01 - nearFocusRange01) <= nearDistance01)
                    nearFocusRange01 = (focusDistance01 - nearDistance01 - 0.0000001f);
                if ((focusDistance01 + farFocusRange01) >= farDistance01)
                    farFocusRange01 = (farDistance01 - focusDistance01 - 0.0000001f);


                float a1 = 1.0f / (nearDistance01 - focusDistance01 + nearFocusRange01);
                float a2 = 1.0f / (farDistance01 - focusDistance01 - farFocusRange01);
                float b1 = (1.0f - a1 * nearDistance01), b2 = (1.0f - a2 * farDistance01);
                float c1 = -1.0f, c2 = 1.0f;
                blurrinessParam = new Vector4(c1 * a1, c1 * b1, c2 * a2, c2 * b2);
                blurrinessCoe = new Vector4(0.0f, 0.0f, (b2 - b1) / (a1 - a2), 0.0f);
                filmicDepthOfFieldMaterial.SetVector("_BlurParams", blurrinessParam);
                filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);

                if (visualizeBluriness)
                {
                    Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, (int)Passes.BlurinessAmountVisualisation2);
                }
                else
                {
                    if (apertureShape == ApertureShape.BokehTextureOLD)
                    {
                        //source.MarkRestoreExpected();
                        Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, (int)Passes.CaptureCocExplicit);
                        Graphics.Blit(destination, source);
                    }
                    else
                    {
                        Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, (int)Passes.BlurinessAmountExplicit);
                    }
                }
            }
        }

        void DoCircle(RenderTexture source, RenderTexture destination)
        {
            float maxRadius = nearRadius > farRadius ? nearRadius : farRadius;
            if (maxRadius == 0.0)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Setup
            int rtW = source.width;
            int rtH = source.height;
            RenderTexture blurinessFullRes = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
            RenderTexture tmpFullRes = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, source.format);
            RenderTexture tmpQuarterRes = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW / 2, rtH / 2, source.format);

            // Blur Map
            Bluriness(source, blurinessFullRes);

            // Boost
            RenderTexture sourceWithBoost = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
            WhiteBoost(source, blurinessFullRes, tmpFullRes, sourceWithBoost);
            ImageEffectHelper.ReleaseTemporaryRenderTexture(this, tmpFullRes);

            // Convolve
            Vector4 blurrinessCoe;
            int blurPass = maxRadius <= 7.0f ? (int)Passes.Circle0 : (int)Passes.Circle2;

            RenderTexture sourceBoostQuarterRes = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW / 2, rtH / 2, RenderTextureFormat.ARGBHalf);
            Graphics.Blit(sourceWithBoost, sourceBoostQuarterRes);
            blurrinessCoe = new Vector4(0.4f * nearRadius, 0.4f * farRadius, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", sourceBoostQuarterRes);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", blurinessFullRes);
            Graphics.Blit(sourceBoostQuarterRes, tmpQuarterRes, filmicDepthOfFieldMaterial, blurPass);

            blurrinessCoe = new Vector4(0.8f * nearRadius, 0.8f * farRadius, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", sourceWithBoost);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", blurinessFullRes);
            filmicDepthOfFieldMaterial.SetTexture("_ThirdTex", tmpQuarterRes);
            Graphics.Blit(sourceWithBoost, destination, filmicDepthOfFieldMaterial, blurPass + 1);
        }

        void DoHexagon(RenderTexture source, RenderTexture destination)
        {
            float maxRadius = nearRadius > farRadius ? nearRadius : farRadius;
            if (maxRadius == 0.0)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Setup
            int rtW = source.width;
            int rtH = source.height;
            RenderTexture bluriness = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
            RenderTexture tmp1 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, source.format);
            RenderTexture tmp2 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, source.format);

            // Blur Map
            Bluriness(source, bluriness);

            // Boost
            RenderTexture sourceWithBoost = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
            WhiteBoost(source, bluriness, tmp1, sourceWithBoost);

            // Convolve
            int blurPass = maxRadius <= 5.0f ? (int)Passes.Shape0 : (maxRadius <= 10.0f ? (int)Passes.Shape2 : (int)Passes.Shape4);
            Vector4 blurrinessCoe = new Vector4(nearRadius, farRadius, 0.0f, 0.0f);
            Vector4 delta = new Vector4(0.5f, 0.0f, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", sourceWithBoost);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
            filmicDepthOfFieldMaterial.SetVector("_Delta", delta);
            Graphics.Blit(sourceWithBoost, tmp1, filmicDepthOfFieldMaterial, blurPass);

            delta = new Vector4(0.25f, 0.433013f, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", tmp1);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
            filmicDepthOfFieldMaterial.SetVector("_Delta", delta);
            Graphics.Blit(tmp1, tmp2, filmicDepthOfFieldMaterial, blurPass);

            delta = new Vector4(0.25f, -0.433013f, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", tmp1);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
            filmicDepthOfFieldMaterial.SetTexture("_ThirdTex", tmp2);
            filmicDepthOfFieldMaterial.SetVector("_Delta", delta);
            Graphics.Blit(tmp1, destination, filmicDepthOfFieldMaterial, blurPass + 1);
        }

        void DoOctogon(RenderTexture source, RenderTexture destination)
        {
            float maxRadius = nearRadius > farRadius ? nearRadius : farRadius;
            if (maxRadius == 0.0)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Setup
            int rtW = source.width;
            int rtH = source.height;
            RenderTexture bluriness = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
            RenderTexture tmp1 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, source.format);
            RenderTexture tmp2 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, source.format);

            // Blur Map
            Bluriness(source, bluriness);

            // Boost
            RenderTexture sourceWithBoost = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
            WhiteBoost(source, bluriness, tmp1, sourceWithBoost);

            // Convolve
            int blurPass = maxRadius <= 5.0f ? (int)Passes.Shape0 : (maxRadius <= 10.0f ? (int)Passes.Shape2 : (int)Passes.Shape4);
            Vector4 blurrinessCoe = new Vector4(nearRadius, farRadius, 0.0f, 0.0f);
            Vector4 delta = new Vector4(0.5f, 0.0f, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", sourceWithBoost);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
            filmicDepthOfFieldMaterial.SetVector("_Delta", delta);
            Graphics.Blit(sourceWithBoost, tmp1, filmicDepthOfFieldMaterial, blurPass);

            delta = new Vector4(0.0f, 0.5f, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", tmp1);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
            filmicDepthOfFieldMaterial.SetVector("_Delta", delta);
            Graphics.Blit(tmp1, tmp2, filmicDepthOfFieldMaterial, blurPass);

            delta = new Vector4(-0.353553f, 0.353553f, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", sourceWithBoost);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
            filmicDepthOfFieldMaterial.SetVector("_Delta", delta);
            Graphics.Blit(sourceWithBoost, tmp1, filmicDepthOfFieldMaterial, blurPass);

            delta = new Vector4(0.353553f, 0.353553f, 0.0f, 0.0f);
            filmicDepthOfFieldMaterial.SetTexture("_MainTex", tmp1);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", bluriness);
            filmicDepthOfFieldMaterial.SetTexture("_ThirdTex", tmp2);
            filmicDepthOfFieldMaterial.SetVector("_Delta", delta);
            Graphics.Blit(tmp1, destination, filmicDepthOfFieldMaterial, blurPass + 1);
        }

        void OnRenderImage_OLD(RenderTexture source, RenderTexture destination)
        {
            if (visualizeBluriness)
            {
                Bluriness(source, destination);
            }
            else
            {
                switch (apertureShape)
                {
                    case ApertureShape.CircularOLD:       DoCircle(source, destination); break;
                    case ApertureShape.HexagonalOLD:      DoHexagon(source, destination); break;
                    case ApertureShape.OctogonalOLD:      DoOctogon(source, destination); break;
                    case ApertureShape.BokehTextureOLD: DoDX11Bokeh(source, destination); break;
                }
            }

            ImageEffectHelper.ReleaseAllTemporyRenderTexutres(this);
        }

        //-------------------------------------------------------------------//
        // Main entry point                                                  //
        //-------------------------------------------------------------------//
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (medianFilterMaterial == null || filmicDepthOfFieldMaterial == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            //TODO cleanme
            if ((apertureShape == ApertureShape.CircularOLD) ||
                (apertureShape == ApertureShape.HexagonalOLD) ||
                (apertureShape == ApertureShape.OctogonalOLD) ||
                (apertureShape == ApertureShape.BokehTextureOLD))
            {
                OnRenderImage_OLD(source, destination);
                return;
            }

            if (visualizeBluriness)
            {
                Vector4 blurrinessParam;
                Vector4 blurrinessCoe;
                ComputeCocParameters(out blurrinessParam, out blurrinessCoe);
                filmicDepthOfFieldMaterial.SetVector("_BlurParams", blurrinessParam);
                filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
                Graphics.Blit(null, destination, filmicDepthOfFieldMaterial, (uiMode == UIMode.Explicit) ? (int)Passes.VisualizeCocExplicit : (int)Passes.VisualizeCoc);
            }
            else
            {
                DoDepthOfField(source, destination);
            }

            ImageEffectHelper.ReleaseAllTemporyRenderTexutres(this);
        }

        private void DoDepthOfField(RenderTexture source, RenderTexture destination)
        {
            float radiusAdjustement = source.height / 720.0f;

            float textureBokehScale = radiusAdjustement;
            float textureBokehMaxRadius = Mathf.Max(nearRadius, farRadius) * textureBokehScale * 0.75f;

            float nearBlurRadius = nearRadius * radiusAdjustement;
            float farBlurRadius = farRadius * radiusAdjustement;
            float maxBlurRadius = Mathf.Max(nearBlurRadius, farBlurRadius);
            switch (apertureShape)
            {
                case ApertureShape.Hexagonal: maxBlurRadius *= 1.2f; break;
                case ApertureShape.Octogonal: maxBlurRadius *= 1.15f; break;
            }

            if (maxBlurRadius < 0.5f)
            {
                Graphics.Blit(source, destination);
                return;
            }

            //Quarter resolution
            int rtW = source.width / 2;
            int rtH = source.height / 2;
            Vector4 blurrinessCoe = new Vector4(nearBlurRadius * 0.5f, farBlurRadius * 0.5f, 0.0f, 0.0f);
            RenderTexture colorAndCoc  = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
            RenderTexture colorAndCoc2 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);


            // Downsample to Color + COC buffer and apply boost
            Vector4 cocParam;
            Vector4 cocCoe;
            ComputeCocParameters(out cocParam, out cocCoe);
            filmicDepthOfFieldMaterial.SetVector("_BlurParams", cocParam);
            filmicDepthOfFieldMaterial.SetVector("_BlurCoe", cocCoe);
            filmicDepthOfFieldMaterial.SetVector("_BoostParams", new Vector4(nearBlurRadius * nearBoostAmount * -0.5f, farBlurRadius * farBoostAmount * 0.5f, boostPoint, 0.0f));
            Graphics.Blit(source, colorAndCoc2, filmicDepthOfFieldMaterial, (uiMode == UIMode.Explicit) ? (int)Passes.CaptureSignedCocExplicit : (int)Passes.CaptureSignedCoc);
            RenderTexture src = colorAndCoc2;
            RenderTexture dst = colorAndCoc;


            // Collect texture bokeh candidates and replace with a darker pixel
            if (shouldPerformBokeh)
            {
                // Blur a bit so we can do a frequency check
                RenderTexture blurred = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.ARGBHalf);
                Graphics.Blit(src, blurred, filmicDepthOfFieldMaterial, (int)Passes.BoxDownsample);
                filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(0.0f, 1.5f, 0.0f, 1.5f));
                Graphics.Blit(blurred, dst, filmicDepthOfFieldMaterial, (int)Passes.BlurAlphaWeighted);
                filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(1.5f, 0.0f, 0.0f, 1.5f));
                Graphics.Blit(dst, blurred, filmicDepthOfFieldMaterial, (int)Passes.BlurAlphaWeighted);

                // Collect texture bokeh candidates and replace with a darker pixel
                dx11BokehMaterial.SetTexture("_BlurredColor", blurred);
                dx11BokehMaterial.SetFloat("_SpawnHeuristic", dx11SpawnHeuristic);
                dx11BokehMaterial.SetVector("_BokehParams", new Vector4(dx11BokehScale * textureBokehScale, dx11BokehIntensity, dx11BokehThreshold, textureBokehMaxRadius));
                Graphics.SetRandomWriteTarget(1, computeBufferPoints);
                Graphics.Blit(src, dst, dx11BokehMaterial, (int)BokehTexturesPasses.Collect);
                Graphics.ClearRandomWriteTargets();
                SwapRenderTexture(ref src, ref dst);
                ImageEffectHelper.ReleaseTemporaryRenderTexture(this, blurred);
            }

            filmicDepthOfFieldMaterial.SetVector("_BlurParams", cocParam);
            filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
            filmicDepthOfFieldMaterial.SetVector("_BoostParams", new Vector4(nearBlurRadius * nearBoostAmount * -0.5f, farBlurRadius * farBoostAmount * 0.5f, boostPoint, 0.0f));

            // Dilate near blur factor
            RenderTexture blurredFgCoc = null;
            if (dilateNearBlur)
            {
                RenderTexture blurredFgCoc2 = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.RHalf);
                blurredFgCoc = ImageEffectHelper.GetTemporaryRenderTexture(this, rtW, rtH, RenderTextureFormat.RHalf);
                filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(0.0f, nearBlurRadius * 0.25f, 0.0f, 0.0f));
                Graphics.Blit(src, blurredFgCoc2, filmicDepthOfFieldMaterial, (int)Passes.BlurFGCocFromColor);
                filmicDepthOfFieldMaterial.SetVector("_Offsets", new Vector4(nearBlurRadius * 0.25f, 0.0f, 0.0f, 0.0f));
                Graphics.Blit(blurredFgCoc2, blurredFgCoc, filmicDepthOfFieldMaterial, (int)Passes.BlurFGCoc);
                ImageEffectHelper.ReleaseTemporaryRenderTexture(this, blurredFgCoc2);
            }

            // Blur downsampled color to fill the gap between samples
            if (prefilterBlur)
            {
                Graphics.Blit(src, dst, filmicDepthOfFieldMaterial, (int)Passes.CircleSignedCocPrefilter);
                SwapRenderTexture(ref src, ref dst);
            }

            // Apply blur : Circle / Hexagonal or Octagonal (blur will create bokeh if bright pixel where not removed by "m_UseBokehTexture")
            switch (apertureShape)
            {
                case ApertureShape.Circular: DoCircularBlur(blurredFgCoc, ref src, ref dst, maxBlurRadius); break;
                case ApertureShape.Hexagonal: DoHexagonalBlur(blurredFgCoc, ref src, ref dst, maxBlurRadius); break;
                case ApertureShape.Octogonal: DoOctogonalBlur(blurredFgCoc, ref src, ref dst, maxBlurRadius); break;
            }

            // Smooth result
            switch (medianFilter)
            {
                case FilterQuality.Normal:
                {
                    medianFilterMaterial.SetVector("_Offsets", new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                    Graphics.Blit(src, dst, medianFilterMaterial, (int)MedianPasses.Median3);
                    SwapRenderTexture(ref src, ref dst);
                    medianFilterMaterial.SetVector("_Offsets", new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
                    Graphics.Blit(src, dst, medianFilterMaterial, (int)MedianPasses.Median3);
                    SwapRenderTexture(ref src, ref dst);
                    break;
                }
                case FilterQuality.High:
                {
                    Graphics.Blit(src, dst, medianFilterMaterial, (int)MedianPasses.Median3x3);
                    SwapRenderTexture(ref src, ref dst);
                    break;
                }
            }

            // Merge to full resolution (with boost) + upsampling (linear or bicubic)
            filmicDepthOfFieldMaterial.SetVector("_BlurCoe", blurrinessCoe);
            filmicDepthOfFieldMaterial.SetVector("_Convolved_TexelSize", new Vector4(src.width, src.height, 1.0f / src.width, 1.0f / src.height));
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", src);
            int mergePass = (uiMode == UIMode.Explicit) ? (int)Passes.SignedCocMergeExplicit : (int)Passes.SignedCocMerge;
            if (highQualityUpsampling)
            {
                mergePass = (uiMode == UIMode.Explicit) ? (int)Passes.SignedCocMergeExplicitBicubic : (int)Passes.SignedCocMergeBicubic;
            }

            // Apply texture bokeh
            if (shouldPerformBokeh)
            {
                RenderTexture tmp = ImageEffectHelper.GetTemporaryRenderTexture(this, source.height, source.width, source.format);
                Graphics.Blit(source, tmp, filmicDepthOfFieldMaterial, mergePass);

                Graphics.SetRenderTarget(tmp);
                ComputeBuffer.CopyCount(computeBufferPoints, computeBufferDrawArgs, 0);
                dx11BokehMaterial.SetBuffer("pointBuffer", computeBufferPoints);
                dx11BokehMaterial.SetTexture("_MainTex", dx11BokehTexture);
                dx11BokehMaterial.SetVector("_Screen", new Vector3(1.0f / (1.0f * source.width), 1.0f / (1.0f * source.height), textureBokehMaxRadius));
                dx11BokehMaterial.SetPass((int)BokehTexturesPasses.Apply);
                Graphics.DrawProceduralIndirect(MeshTopology.Points, computeBufferDrawArgs, 0);
                Graphics.Blit(tmp, destination);// hackaround for DX11 flipfun (OPTIMIZEME)
            }
            else
            {
                Graphics.Blit(source, destination, filmicDepthOfFieldMaterial, mergePass);
            }
        }

        //-------------------------------------------------------------------//
        // Blurs                                                             //
        //-------------------------------------------------------------------//
        private void DoHexagonalBlur(RenderTexture blurredFgCoc, ref RenderTexture src, ref RenderTexture dst, float maxRadius)
        {
            ComputeBlurDirections(false);

            int blurPass;
            int blurPassMerge;
            GetDirectionalBlurPassesFromRadius(blurredFgCoc, maxRadius, out blurPass, out blurPassMerge);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", blurredFgCoc);
            RenderTexture tmp = ImageEffectHelper.GetTemporaryRenderTexture(this, src.width, src.height, src.format);

            filmicDepthOfFieldMaterial.SetVector("_Delta", m_HexagonalBokehDirection1);
            Graphics.Blit(src, tmp, filmicDepthOfFieldMaterial, blurPass);

            filmicDepthOfFieldMaterial.SetVector("_Delta", m_HexagonalBokehDirection2);
            Graphics.Blit(tmp, src, filmicDepthOfFieldMaterial, blurPass);

            filmicDepthOfFieldMaterial.SetVector("_Delta", m_HexagonalBokehDirection3);
            filmicDepthOfFieldMaterial.SetTexture("_ThirdTex", src);
            Graphics.Blit(tmp, dst, filmicDepthOfFieldMaterial, blurPassMerge);
            ImageEffectHelper.ReleaseTemporaryRenderTexture(this, tmp);
            SwapRenderTexture(ref src, ref dst);
        }

        private void DoOctogonalBlur(RenderTexture blurredFgCoc, ref RenderTexture src, ref RenderTexture dst, float maxRadius)
        {
            ComputeBlurDirections(false);

            int blurPass;
            int blurPassMerge;
            GetDirectionalBlurPassesFromRadius(blurredFgCoc, maxRadius, out blurPass, out blurPassMerge);
            filmicDepthOfFieldMaterial.SetTexture("_SecondTex", blurredFgCoc);
            RenderTexture tmp = ImageEffectHelper.GetTemporaryRenderTexture(this, src.width, src.height, src.format);

            filmicDepthOfFieldMaterial.SetVector("_Delta", m_OctogonalBokehDirection1);
            Graphics.Blit(src, tmp, filmicDepthOfFieldMaterial, blurPass);

            filmicDepthOfFieldMaterial.SetVector("_Delta", m_OctogonalBokehDirection2);
            Graphics.Blit(tmp, dst, filmicDepthOfFieldMaterial, blurPass);

            filmicDepthOfFieldMaterial.SetVector("_Delta", m_OctogonalBokehDirection3);
            Graphics.Blit(src, tmp, filmicDepthOfFieldMaterial, blurPass);

            filmicDepthOfFieldMaterial.SetVector("_Delta", m_OctogonalBokehDirection4);
            filmicDepthOfFieldMaterial.SetTexture("_ThirdTex", dst);
            Graphics.Blit(tmp, src, filmicDepthOfFieldMaterial, blurPassMerge);
            ImageEffectHelper.ReleaseTemporaryRenderTexture(this, tmp);
        }

        private void DoCircularBlur(RenderTexture blurredFgCoc, ref RenderTexture src, ref RenderTexture dst, float maxRadius)
        {
            int bokehPass;
            if (blurredFgCoc != null)
            {
                filmicDepthOfFieldMaterial.SetTexture("_SecondTex", blurredFgCoc);
                bokehPass = (maxRadius > 10.0f) ? (int)Passes.CircleSignedCocBokehWithDilatedFG : (int)Passes.CircleSignedCocBokehWithDilatedFGLow;
            }
            else
            {
                bokehPass = (maxRadius > 10.0f) ? (int)Passes.CircleSignedCocBokeh : (int)Passes.CircleSignedCocBokehLow;
            }
            Graphics.Blit(src, dst, filmicDepthOfFieldMaterial, bokehPass);
            SwapRenderTexture(ref src, ref dst);
        }

        //-------------------------------------------------------------------//
        // Helpers                                                           //
        //-------------------------------------------------------------------//
        private void ComputeCocParameters(out Vector4 blurParams, out Vector4 blurCoe)
        {
            Camera sceneCamera = GetComponent<Camera>();
            float focusDistance01 = focusTransform ? (sceneCamera.WorldToViewportPoint(focusTransform.position)).z / (sceneCamera.farClipPlane) : (focusPlane * focusPlane * focusPlane * focusPlane);

            if (uiMode == UIMode.Basic || uiMode == UIMode.Advanced)
            {
                float focusRange01 = focusRange * focusRange * focusRange * focusRange;
                float focalLength = 4.0f / Mathf.Tan(0.5f * sceneCamera.fieldOfView * Mathf.Deg2Rad);
                float aperture = focalLength / fStops;
                blurCoe = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
                blurParams = new Vector4(aperture, focalLength, focusDistance01, focusRange01);
            }
            else
            {
                float nearDistance01 = nearPlane * nearPlane * nearPlane * nearPlane;
                float farDistance01 = farPlane * farPlane * farPlane * farPlane;
                float nearFocusRange01 = focusRange * focusRange * focusRange * focusRange;
                float farFocusRange01 = nearFocusRange01;

                if (focusDistance01 <= nearDistance01)
                    focusDistance01 = nearDistance01 + 0.0000001f;
                if (focusDistance01 >= farDistance01)
                    focusDistance01 = farDistance01 - 0.0000001f;
                if ((focusDistance01 - nearFocusRange01) <= nearDistance01)
                    nearFocusRange01 = (focusDistance01 - nearDistance01 - 0.0000001f);
                if ((focusDistance01 + farFocusRange01) >= farDistance01)
                    farFocusRange01 = (farDistance01 - focusDistance01 - 0.0000001f);

                float a1 = 1.0f / (nearDistance01 - focusDistance01 + nearFocusRange01);
                float a2 = 1.0f / (farDistance01  - focusDistance01 - farFocusRange01);
                float b1 = (1.0f - a1 * nearDistance01), b2 = (1.0f - a2 * farDistance01);
                const float c1 = -1.0f;
                const float c2 = 1.0f;
                blurParams = new Vector4(c1 * a1, c1 * b1, c2 * a2, c2 * b2);
                blurCoe = new Vector4(0.0f, 0.0f, (b2 - b1) / (a1 - a2), 0.0f);
            }
        }

        private void ReleaseComputeResources()
        {
            if (m_ComputeBufferDrawArgs != null)
                m_ComputeBufferDrawArgs.Release();
            m_ComputeBufferDrawArgs = null;
            if (m_ComputeBufferPoints != null)
                m_ComputeBufferPoints.Release();
            m_ComputeBufferPoints = null;
        }

        private void ComputeBlurDirections(bool force)
        {
            if (!force && Math.Abs(m_LastApertureOrientation - apertureOrientation) < float.Epsilon) return;

            m_LastApertureOrientation = apertureOrientation;

            float rotationRadian = apertureOrientation * Mathf.Deg2Rad;
            float cosinus = Mathf.Cos(rotationRadian);
            float sinus = Mathf.Sin(rotationRadian);

            m_OctogonalBokehDirection1 = new Vector4(0.5f, 0.0f, 0.0f, 0.0f);
            m_OctogonalBokehDirection2 = new Vector4(0.0f, 0.5f, 0.0f, 0.0f);
            m_OctogonalBokehDirection3 = new Vector4(-0.353553f, 0.353553f, 0.0f, 0.0f);
            m_OctogonalBokehDirection4 = new Vector4(0.353553f, 0.353553f, 0.0f, 0.0f);

            m_HexagonalBokehDirection1 = new Vector4(0.5f, 0.0f, 0.0f, 0.0f);
            m_HexagonalBokehDirection2 = new Vector4(0.25f, 0.433013f, 0.0f, 0.0f);
            m_HexagonalBokehDirection3 = new Vector4(0.25f, -0.433013f, 0.0f, 0.0f);

            if (rotationRadian > float.Epsilon)
            {
                Rotate2D(ref m_OctogonalBokehDirection1, cosinus, sinus);
                Rotate2D(ref m_OctogonalBokehDirection2, cosinus, sinus);
                Rotate2D(ref m_OctogonalBokehDirection3, cosinus, sinus);
                Rotate2D(ref m_OctogonalBokehDirection4, cosinus, sinus);
                Rotate2D(ref m_HexagonalBokehDirection1, cosinus, sinus);
                Rotate2D(ref m_HexagonalBokehDirection2, cosinus, sinus);
                Rotate2D(ref m_HexagonalBokehDirection3, cosinus, sinus);
            }
        }

        private static void Rotate2D(ref Vector4 direction, float cosinus, float sinus)
        {
            Vector4 source = direction;
            direction.x = source.x * cosinus - source.y * sinus;
            direction.y = source.x * sinus + source.y * cosinus;
        }

        private static void SwapRenderTexture(ref RenderTexture src, ref RenderTexture dst)
        {
            RenderTexture tmp = dst;
            dst = src;
            src = tmp;
        }

        private static void GetDirectionalBlurPassesFromRadius(RenderTexture blurredFgCoc, float maxRadius, out int blurPass, out int blurAndMergePass)
        {
            if (blurredFgCoc == null)
            {
                if (maxRadius > 10.0f)
                {
                    blurPass = (int)Passes.ShapeHighQuality;
                    blurAndMergePass = (int)Passes.ShapeHighQualityMerge;
                }
                else if (maxRadius > 5.0f)
                {
                    blurPass = (int)Passes.ShapeMediumQuality;
                    blurAndMergePass = (int)Passes.ShapeMediumQualityMerge;
                }
                else
                {
                    blurPass = (int)Passes.ShapeLowQuality;
                    blurAndMergePass = (int)Passes.ShapeLowQualityMerge;
                }
            }
            else
            {
                if (maxRadius > 10.0f)
                {
                    blurPass = (int)Passes.ShapeHighQualityDilateFG;
                    blurAndMergePass = (int)Passes.ShapeHighQualityMergeDilateFG;
                }
                else if (maxRadius > 5.0f)
                {
                    blurPass = (int)Passes.ShapeMediumQualityDilateFG;
                    blurAndMergePass = (int)Passes.ShapeMediumQualityMergeDilateFG;
                }
                else
                {
                    blurPass = (int)Passes.ShapeLowQualityDilateFG;
                    blurAndMergePass = (int)Passes.ShapeLowQualityMergeDilateFG;
                }
            }
        }
    }
}
