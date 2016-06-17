// Main part of the motion blur effect

using UnityEngine;

namespace UnityStandardAssets.CinematicEffects
{
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Cinematic/Motion Blur")]
    public partial class MotionBlur : MonoBehaviour
    {
        #region Public properties

        /// Effect settings.
        public Settings settings
        {
            get { return _settings; }
            set { _settings = value; }
        }

        [SerializeField]
        Settings _settings = Settings.defaultSettings;

        // Debug mode switch.
        enum DebugMode { Off, Velocity, NeighborMax, Depth }

        [SerializeField, Tooltip("The debug visualization mode.")]
        DebugMode _debugMode;

        #endregion

        #region Private properties and methods

        [SerializeField] Shader _prefilterShader;
        [SerializeField] Shader _reconstructionShader;

        Material _prefilterMaterial;
        Material _reconstructionMaterial;

        // Shader retrieval
        Shader prefilterShader
        {
            get
            {
                if (_prefilterShader != null) return _prefilterShader;
                return Shader.Find("Hidden/Image Effects/Cinematic/MotionBlur/Prefilter");
            }
        }

        Shader reconstructionShader
        {
            get
            {
                if (_reconstructionShader != null) return _reconstructionShader;
                return Shader.Find("Hidden/Image Effects/Cinematic/MotionBlur/Reconstruction");
            }
        }

        // Lazy material initialization
        Material prefilterMaterial
        {
            get
            {
                if (_prefilterMaterial == null)
                    _prefilterMaterial = ImageEffectHelper.CheckShaderAndCreateMaterial(prefilterShader);
                return _prefilterMaterial;
            }
        }

        Material reconstructionMaterial
        {
            get
            {
                if (_reconstructionMaterial == null)
                    _reconstructionMaterial = ImageEffectHelper.CheckShaderAndCreateMaterial(reconstructionShader);
                return _reconstructionMaterial;
            }
        }

        // The count of reconstruction filter loop (== SampleCount/2).
        int reconstructionLoopCount
        {
            get
            {
                switch (_settings.sampleCount)
                {
                    case SampleCount.Low:    return 2;
                    case SampleCount.Medium: return 5;
                    case SampleCount.High:   return 10;
                }
                return Mathf.Clamp(_settings.sampleCountValue / 2, 1, 64);
            }
        }

        // Scale factor for motion vectors used to apply the exposure time.
        float velocityScale
        {
            get
            {
                if (_settings.exposureMode == ExposureMode.Constant)
                    return 1.0f / (_settings.shutterSpeed * Time.smoothDeltaTime);
                else // ExposureMode.DeltaTime
                    return _settings.exposureTimeScale;
            }
        }

        // Temporary render texture management
        RenderTexture GetTemporaryRT(Texture source, int divider, RenderTextureFormat format)
        {
            var w = source.width / divider;
            var h = source.height / divider;
            var rt = RenderTexture.GetTemporary(w, h, 0, format);
            rt.filterMode = FilterMode.Point;
            return rt;
        }

        void ReleaseTemporaryRT(RenderTexture rt)
        {
            RenderTexture.ReleaseTemporary(rt);
        }

        #endregion

        #region MonoBehaviour functions

        void OnEnable()
        {
            // Check if the shader is supported in the current platform.
            if (!ImageEffectHelper.IsSupported(prefilterShader, true, false, this) ||
                !ImageEffectHelper.IsSupported(reconstructionShader, true, false, this))
            {
                enabled = false;
                return;
            }

            // Requires depth and motion vectors.
            GetComponent<Camera>().depthTextureMode |=
                DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }

        void OnDisable()
        {
            DestroyImmediate(_prefilterMaterial);
            _prefilterMaterial = null;

            DestroyImmediate(_reconstructionMaterial);
            _reconstructionMaterial = null;
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // Texture format for storing packed velocity/depth.
            const RenderTextureFormat packedRTFormat = RenderTextureFormat.ARGB2101010;

            // Texture format for storing 2D vectors.
            const RenderTextureFormat vectorRTFormat = RenderTextureFormat.RGHalf;

            // Calculate the maximum blur radius in pixels.
            var maxBlurPixels = (int)(_settings.maxBlurRadius * source.height / 100);

            // Calculate the TileMax size.
            // It should be a multiple of 8 and larger than maxBlur.
            var tileSize = ((maxBlurPixels - 1) / 8 + 1) * 8;

            // Pass 1 - Velocity/depth packing
            var prefilter = prefilterMaterial;
            prefilter.SetFloat("_VelocityScale", velocityScale);
            prefilter.SetFloat("_MaxBlurRadius", maxBlurPixels);

            var vbuffer = GetTemporaryRT(source, 1, packedRTFormat);
            Graphics.Blit(null, vbuffer, prefilter, 0);

            // Pass 2 - First TileMax filter (1/4 downsize)
            var tile4 = GetTemporaryRT(source, 4, vectorRTFormat);
            Graphics.Blit(vbuffer, tile4, prefilter, 1);

            // Pass 3 - Second TileMax filter (1/2 downsize)
            var tile8 = GetTemporaryRT(source, 8, vectorRTFormat);
            Graphics.Blit(tile4, tile8, prefilter, 2);
            ReleaseTemporaryRT(tile4);

            // Pass 4 - Third TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
            prefilter.SetVector("_TileMaxOffs", tileMaxOffs);
            prefilter.SetInt("_TileMaxLoop", tileSize / 8);

            var tile = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile8, tile, prefilter, 3);
            ReleaseTemporaryRT(tile8);

            // Pass 5 - NeighborMax filter
            var neighborMax = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile, neighborMax, prefilter, 4);
            ReleaseTemporaryRT(tile);

            // Pass 6 - Reconstruction pass
            var reconstruction = reconstructionMaterial;
            reconstruction.SetInt("_LoopCount", reconstructionLoopCount);
            reconstruction.SetFloat("_MaxBlurRadius", maxBlurPixels);
            reconstruction.SetTexture("_NeighborMaxTex", neighborMax);
            reconstruction.SetTexture("_VelocityTex", vbuffer);
            Graphics.Blit(source, destination, reconstruction, (int)_debugMode);

            // Cleaning up
            ReleaseTemporaryRT(vbuffer);
            ReleaseTemporaryRT(neighborMax);
        }

        #endregion
    }
}
