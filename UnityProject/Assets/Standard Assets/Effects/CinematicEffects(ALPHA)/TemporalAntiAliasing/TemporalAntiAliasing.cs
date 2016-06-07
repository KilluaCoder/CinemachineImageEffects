using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Cinematic Image Effects/Temporal Anti-aliasing")]
public class TemporalAntiAliasing : MonoBehaviour
{
    // The idea for controlling the amount of feedback going to the final
    // frame construct is repurposed from Playdead's TAA implementation:
    // https://github.com/playdeadgames/temporal
    [Range(0f, 1f)] public float minimumFeedback = 0.88f;
    [Range(0f, 1f)] public float maximumFeedback = 0.97f;

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

    public RenderTexture m_History;
    private int m_SampleIndex = 0;

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

    private float GetCatmullRomValue(float k)
    {
        k = Mathf.Abs(k);

        if (k > 1.0f)
        {
            return ((-0.5f * k + 2.5f) * k - 4.0f) * k + 2.0f;
        }

        return (1.5f * k - 2.5f) * k * k + 1.0f;
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
        Vector2 offset = GenerateRandomOffset();

        // Debug.Log(offset);

        // material.SetVector("_Fuzz", offset);

        Matrix4x4 fuzz = Matrix4x4.identity;

        offset.x *= 2.0f / camera_.pixelWidth;
        offset.y *= 2.0f / camera_.pixelHeight;

        fuzz.m03 = offset.x;
        fuzz.m13 = offset.y;

        camera_.nonJitteredProjectionMatrix = camera_.projectionMatrix;
        camera_.projectionMatrix = fuzz * camera_.projectionMatrix;

        material.SetVector("_Fuzz", -offset);
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (m_History == null || (m_History.width != source.width || m_History.height != source.height))
        {
            if (m_History)
                RenderTexture.ReleaseTemporary(m_History);

            m_History = RenderTexture.GetTemporary(source.width, source.height, 0, source.format, RenderTextureReadWrite.Linear);
            m_History.hideFlags = HideFlags.HideAndDontSave;

            Graphics.Blit(source, m_History);
        }

        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, source.format, RenderTextureReadWrite.Linear);

        material.SetTexture("_HistoryTex", m_History);
        material.SetVector("_FeedbackBounds", new Vector2(minimumFeedback, maximumFeedback));

        Graphics.Blit(source, temporary, material, 0);

        Graphics.Blit(temporary, destination);
        Graphics.Blit(temporary, m_History);

        RenderTexture.ReleaseTemporary(temporary);
    }

    void OnPostRender()
    {
        camera_.ResetProjectionMatrix();
    }
}
