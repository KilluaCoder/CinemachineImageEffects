using System;
using UnityEngine;

namespace UnitySampleAssets.ImageEffects
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Temporal Anti-aliasing")]
    public class TemporalAntiAliasing : MonoBehaviour
    {
        private Shader m_Shader;
        public Shader shader
        {
            get
            {
                if (m_Shader == null)
                    m_Shader = Shader.Find("Hidden/Temporal Anti-aliasing");

                return m_Shader;
            }
        }

        private Material m_Material;
        public Material material
        {
            get
            {
                if (m_Material == null)
                {
                    if (shader == null || !shader.isSupported)
                        return null;

                    m_Material = new Material(shader);
                }

                return m_Material;
            }
        }

        private Camera m_Camera;
        public Camera camera_
        {
            get
            {
                if (m_Camera == null)
                    m_Camera = GetComponent<Camera>();

                return m_Camera;
            }
        }

        private Matrix4x4 m_PreviousViewProjectionMatrix;

        private RenderTexture m_History;
        private int m_SampleIndex = 0;

        private float GetCatmullRomValue(float k)
        {
            k = Mathf.Abs(k);

            if (k > 1.0f)
            {
                return ((-0.5f * k + 2.5f) * k - 4.0f) * k + 2.0f;
            }

            return (1.5f * k - 2.5f) * k * k + 1.0f;
        }

        private float GetMitchellNetravaliValue(float k, float b, float c)
        {
            k = Mathf.Abs(k);

            if (k < 1.0f)
            {
                return ((12.0f - 9.0f * b - 6.0f * c) * k * k * k +
                        (-18.0f + 12.0f * b + 6.0f * c) * k * k +
                        (6.0f - 2.0f * b)) / 6.0f;
            }
            else if ((k >= 1.0f) && (k < 2.0f))
            {
                return ((-b - 6.0f * c) * k * k * k +
                        (6.0f * b + 30.0f * c) * k * k +
                        (-12.0f * b - 48.0f * c) * k +
                        (8.0f * b + 24.0f * c)) / 6.0f;
            }

            return 0.0f;
        }

        private float GetHaltonValue(int index, int radix)
        {
            float result = 0.0f;
            float fraction = 1.0f / (float)radix;

            while (index > 0)
            {
                result += (float)(index % radix) * fraction;

                index /= radix;
                fraction /= (float)radix;
            }

            return result;
        }

        private Vector2 GenerateRandomOffset()
        {
            Vector2 offset = new Vector2(
                    GetHaltonValue(m_SampleIndex & 1023, 2),
                    GetHaltonValue(m_SampleIndex & 1023, 3));

            if (++m_SampleIndex >= 16)
                m_SampleIndex = 0;

            return offset;
        }

        // Adapted heavily from PlayDead's TAA code
        // https://github.com/playdeadgames/temporal/blob/master/Assets/Scripts/Extensions.cs
        private Matrix4x4 GetPerspectiveProjectionMatrix(Vector2 offset)
        {
            float vertical = Mathf.Tan(0.5f * Mathf.Deg2Rad * camera_.fieldOfView);
            float horizontal = vertical * camera_.aspect;

            offset.x *= horizontal / (0.5f * camera_.pixelWidth);
            offset.y *= vertical / (0.5f * camera_.pixelHeight);

            float left = (offset.x - horizontal) * camera_.nearClipPlane;
            float right = (offset.x + horizontal) * camera_.nearClipPlane;
            float top = (offset.y + vertical) * camera_.nearClipPlane;
            float bottom = (offset.y - vertical) * camera_.nearClipPlane;

            Matrix4x4 matrix = new Matrix4x4();

            matrix[0, 0] = (2.0f * camera_.nearClipPlane) / (right - left);
            matrix[0, 1] = 0.0f;
            matrix[0, 2] = (right + left) / (right - left);
            matrix[0, 3] = 0.0f;

            matrix[1, 0] = 0.0f;
            matrix[1, 1] = (2.0f * camera_.nearClipPlane) / (top - bottom);
            matrix[1, 2] = (top + bottom) / (top - bottom);
            matrix[1, 3] = 0.0f;

            matrix[2, 0] = 0.0f;
            matrix[2, 1] = 0.0f;
            matrix[2, 2] = -(camera_.farClipPlane + camera_.nearClipPlane) / (camera_.farClipPlane - camera_.nearClipPlane);
            matrix[2, 3] = -(2.0f * camera_.farClipPlane * camera_.nearClipPlane) / (camera_.farClipPlane - camera_.nearClipPlane);

            matrix[3, 0] = 0.0f;
            matrix[3, 1] = 0.0f;
            matrix[3, 2] = -1.0f;
            matrix[3, 3] = 0.0f;

            return matrix;
        }

        void OnEnable()
        {
            camera_.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        }

        void OnDisable()
        {
            if (m_History != null)
            {
                RenderTexture.ReleaseTemporary(m_History);
                m_History = null;
            }

            camera_.depthTextureMode &= ~(DepthTextureMode.MotionVectors);
        }

        void OnPreCull()
        {
            Vector2 jitter = GenerateRandomOffset();

#if UNITY_5_4_OR_NEWER
            camera_.nonJitteredProjectionMatrix = camera_.projectionMatrix;
#endif
            camera_.projectionMatrix = GetPerspectiveProjectionMatrix(jitter);

            jitter.x /= camera_.pixelWidth;
            jitter.y /= camera_.pixelHeight;

            material.SetVector("_Jitter", jitter);
        }

        public void OnPostRender()
        {
            camera_.ResetProjectionMatrix();
        }

        public void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (camera_.orthographic)
            {
                Graphics.Blit(source, destination);
                return;
            }
            else if (m_History == null || (m_History.width != source.width || m_History.height != source.height))
            {
                if (m_History)
                    RenderTexture.ReleaseTemporary(m_History);

                m_History = RenderTexture.GetTemporary(source.width, source.height, 0, source.format, RenderTextureReadWrite.Default);
                m_History.hideFlags = HideFlags.HideAndDontSave;
                m_History.filterMode = FilterMode.Point;

                Graphics.Blit(source, m_History);
            }

            material.SetTexture("_HistoryTex", m_History);

            RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, source.format, RenderTextureReadWrite.Default);
            temporary.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, temporary, material, 0);

            Graphics.Blit(temporary, m_History);
            Graphics.Blit(temporary, destination);

            RenderTexture.ReleaseTemporary(temporary);
        }
    }
}
