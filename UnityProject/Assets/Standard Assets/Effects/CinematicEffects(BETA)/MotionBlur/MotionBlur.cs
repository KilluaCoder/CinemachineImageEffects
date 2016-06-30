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

        #endregion

        #region Debug settings

        enum DebugMode { Off, Velocity, NeighborMax, Depth }

        [SerializeField]
        [Tooltip("The debug visualization mode.")]
        DebugMode _debugMode;

        #endregion

        #region Private properties and methods

        [SerializeField] Shader _shader;

        Material _material;

        // Shader retrieval
        Shader shader
        {
            get
            {
                if (_shader != null) return _shader;
                return Shader.Find("Hidden/Image Effects/Cinematic/MotionBlur");
            }
        }

        // Lazy material initialization
        Material material
        {
            get
            {
                if (_material == null)
                    _material = ImageEffectHelper.CheckShaderAndCreateMaterial(shader);
                return _material;
            }
        }

        // Scale factor of motion vectors
        float VelocityScale
        {
            get
            {
                if (_settings.exposureTime == ExposureTime.Constant)
                    return 1.0f / (_settings.shutterSpeed * Time.smoothDeltaTime);
                else // ExposureTime.DeltaTime
                    return Mathf.Clamp01(_settings.shutterAngle / 360);
            }
        }

        // The count of reconstruction filter loop (== SampleCount/2).
        int LoopCount
        {
            get
            {
                switch (_settings.sampleCount)
                {
                    case SampleCount.Low:    return 2;  // 4 samples
                    case SampleCount.Medium: return 5;  // 10 samples
                    case SampleCount.High:   return 10; // 20 samples
                }
                // SampleCount.Custom
                return Mathf.Clamp(_settings.customSampleCount / 2, 1, 64);
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
            if (!ImageEffectHelper.IsSupported(shader, true, false, this))
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
            DestroyImmediate(_material);
            _material = null;
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
            var m = material;
            m.SetFloat("_VelocityScale", VelocityScale);
            m.SetFloat("_MaxBlurRadius", maxBlurPixels);

            var vbuffer = GetTemporaryRT(source, 1, packedRTFormat);
            Graphics.Blit(null, vbuffer, m, 0);

            // Pass 2 - First TileMax filter (1/4 downsize)
            var tile4 = GetTemporaryRT(source, 4, vectorRTFormat);
            Graphics.Blit(vbuffer, tile4, m, 1);

            // Pass 3 - Second TileMax filter (1/2 downsize)
            var tile8 = GetTemporaryRT(source, 8, vectorRTFormat);
            Graphics.Blit(tile4, tile8, m, 2);
            ReleaseTemporaryRT(tile4);

            // Pass 4 - Third TileMax filter (reduce to tileSize)
            var tileMaxOffs = Vector2.one * (tileSize / 8.0f - 1) * -0.5f;
            m.SetVector("_TileMaxOffs", tileMaxOffs);
            m.SetInt("_TileMaxLoop", tileSize / 8);

            var tile = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile8, tile, m, 3);
            ReleaseTemporaryRT(tile8);

            // Pass 5 - NeighborMax filter
            var neighborMax = GetTemporaryRT(source, tileSize, vectorRTFormat);
            Graphics.Blit(tile, neighborMax, m, 4);
            ReleaseTemporaryRT(tile);

            // Pass 6 - Reconstruction pass
            m.SetInt("_LoopCount", LoopCount);
            m.SetFloat("_MaxBlurRadius", maxBlurPixels);
            m.SetTexture("_NeighborMaxTex", neighborMax);
            m.SetTexture("_VelocityTex", vbuffer);
            Graphics.Blit(source, destination, m, 5 + (int)_debugMode);

            // Cleaning up
            ReleaseTemporaryRT(vbuffer);
            ReleaseTemporaryRT(neighborMax);
        }

        #endregion
    }
}
