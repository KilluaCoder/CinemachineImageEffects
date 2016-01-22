/*
 * Copyright (c) 2015 Thomas Hourdel
 * Copyright (c) 2015 Goksel Goktas (Unity)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 *    1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 *
 *    2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 *
 *    3. This notice may not be removed or altered from any source
 *    distribution.
 */

namespace UnityStandardAssets.CinematicEffects
{
    using UnityEngine;
    using UnityEngine.Serialization;
    using System;

    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Subpixel Morphological Anti-aliasing")]
    public class AntiAliasing : MonoBehaviour
    {
        [AttributeUsage(AttributeTargets.Field)]
        public class SettingsGroup : Attribute
        {
        }

        [AttributeUsage(AttributeTargets.Field)]
        public class TopLevelSettings : Attribute
        {
        }

        public enum DebugPass
        {
            Off,
            Edges,
            Weights,
            Accumulation
        }

        public enum QualityPreset
        {
            Low = 0,
            Medium = 1,
            High = 2,
            Ultra = 3,
            Custom
        }

        public enum EdgeDetectionMethod
        {
            Luma = 1,
            Color = 2,
            Depth = 3
        }

        [Serializable]
        public struct GlobalSettings
        {
            [Tooltip("Use this to fine tune your settings when working in Custom quality mode. \"Accumulation\" only works when \"Temporal Filtering\" is enabled.")]
            public DebugPass DebugPass;

            [Tooltip("Low: 60% of the quality.\nMedium: 80% of the quality.\nHigh: 95% of the quality.\nUltra: 99% of the quality (overkill).")]
            public QualityPreset Quality;

            [Tooltip("You've three edge detection methods to choose from: luma, color or depth.\nThey represent different quality/performance and anti-aliasing/sharpness tradeoffs, so our recommendation is for you to choose the one that best suits your particular scenario:\n\n- Depth edge detection is usually the fastest but it may miss some edges.\n- Luma edge detection is usually more expensive than depth edge detection, but catches visible edges that depth edge detection can miss.\n- Color edge detection is usually the most expensive one but catches chroma-only edges.")]
            public EdgeDetectionMethod EdgeDetectionMethod;

            public static GlobalSettings DefaultSettings()
            {
                return new GlobalSettings
                {
                    DebugPass = DebugPass.Off,
                    Quality = QualityPreset.High,
                    EdgeDetectionMethod = EdgeDetectionMethod.Color
                };
            }
        }

        [Serializable]
        public struct QualitySettings
        {
            [Tooltip("Enables/Disables diagonal processing.")]
            public bool DiagonalDetection;

            [Tooltip("Enables/Disables corner detection. Leave this on to avoid blurry corners.")]
            public bool CornerDetection;

            [Range(0f, 0.5f)]
            [Tooltip("Specifies the threshold or sensitivity to edges. Lowering this value you will be able to detect more edges at the expense of performance.\n0.1 is a reasonable value, and allows to catch most visible edges. 0.05 is a rather overkill value, that allows to catch 'em all.")]
            public float Threshold;

            [Min(0.0001f)]
            [Tooltip("Specifies the threshold for depth edge detection. Lowering this value you will be able to detect more edges at the expense of performance.")]
            public float DepthThreshold;

            [Range(0, 112)]
            [Tooltip("Specifies the maximum steps performed in the horizontal/vertical pattern searches, at each side of the pixel.\nIn number of pixels, it's actually the double. So the maximum line length perfectly handled by, for example 16, is 64 (by perfectly, we meant that longer lines won't look as good, but still antialiased).")]
            public int MaxSearchSteps;

            [Range(0, 20)]
            [Tooltip("Specifies the maximum steps performed in the diagonal pattern searches, at each side of the pixel. In this case we jump one pixel at time, instead of two.\nOn high-end machines it is cheap (between a 0.8x and 0.9x slower for 16 steps), but it can have a significant impact on older machines.")]
            public int MaxDiagonalSearchSteps;

            [Range(0, 100)]
            [Tooltip("Specifies how much sharp corners will be rounded.")]
            public int CornerRounding;

            [Min(0f)]
            [Tooltip("If there is an neighbor edge that has a local contrast factor times bigger contrast than current edge, current edge will be discarded.\nThis allows to eliminate spurious crossing edges, and is based on the fact that, if there is too much contrast in a direction, that will hide perceptually contrast in the other neighbors.")]
            public float LocalContrastAdaptationFactor;
            
            public static QualitySettings[] presetQualitySettings = new QualitySettings[]
            {
                // Low
                new QualitySettings
                {
                    DiagonalDetection = false,
                    CornerDetection = false,
                    Threshold = 0.15f,
                    DepthThreshold = 0.01f,
                    MaxSearchSteps = 4,
                    MaxDiagonalSearchSteps = 8,
                    CornerRounding = 25,
                    LocalContrastAdaptationFactor = 2f
                },
                
                // Medium
                new QualitySettings
                {
                    DiagonalDetection = false,
                    CornerDetection = false,
                    Threshold = 0.1f,
                    DepthThreshold = 0.01f,
                    MaxSearchSteps = 8,
                    MaxDiagonalSearchSteps = 8,
                    CornerRounding = 25,
                    LocalContrastAdaptationFactor = 2f
                },
                
                // High
                new QualitySettings
                {
                    DiagonalDetection = true,
                    CornerDetection = true,
                    Threshold = 0.1f,
                    DepthThreshold = 0.01f,
                    MaxSearchSteps = 16,
                    MaxDiagonalSearchSteps = 8,
                    CornerRounding = 25,
                    LocalContrastAdaptationFactor = 2f
                },
                
                // Ultra
                new QualitySettings
                {
                    DiagonalDetection = true,
                    CornerDetection = true,
                    Threshold = 0.05f,
                    DepthThreshold = 0.01f,
                    MaxSearchSteps = 32,
                    MaxDiagonalSearchSteps = 16,
                    CornerRounding = 25,
                    LocalContrastAdaptationFactor = 2f
                },
            };
        }

        [Serializable]
        public struct TemporalSettings
        {
            [Tooltip("Temporal filtering makes it possible for the SMAA algorithm to benefit from minute subpixel information available that has been accumulated over many frames.")]
            public bool Enabled;

            [Range(0.5f, 10.0f)]
            [Tooltip("The size of the fuzz-displacement (jitter) in pixels applied to the camera's perspective projection matrix.\nUsed for 2x temporal anti-aliasing.")]
            public float FuzzSize;

            public static TemporalSettings DefaultSettings()
            {
                return new TemporalSettings
                {
                    Enabled = true,
                    FuzzSize = 2f
                };
            }
        }

        [Serializable]
        public struct PredicationSettings
        {
            [Tooltip("Predicated thresholding allows to better preserve texture details and to improve performance, by decreasing the number of detected edges using an additional buffer (the detph buffer).\nIt locally decreases the luma or color threshold if an edge is found in an additional buffer (so the global threshold can be higher).")]
            public bool Enabled;

            [Min(0.0001f)]
            [Tooltip("Threshold to be used in the additional predication buffer.")]
            public float Threshold;

            [Range(1f, 5f)]
            [Tooltip("How much to scale the global threshold used for luma or color edge detection when using predication.")]
            public float Scale;

            [Range(0f, 1f)]
            [Tooltip("How much to locally decrease the threshold.")]
            public float Strength;

            public static PredicationSettings DefaultSettings()
            {
                return new PredicationSettings
                {
                    Enabled = false,
                    Threshold = 0.01f,
                    Scale = 2f,
                    Strength = 0.4f
                };
            }
        }

        [TopLevelSettings]
        public GlobalSettings Settings = GlobalSettings.DefaultSettings();

        [SettingsGroup]
        public QualitySettings Quality = QualitySettings.presetQualitySettings[2];

        [SettingsGroup]
        public PredicationSettings Predication = PredicationSettings.DefaultSettings();

        [SettingsGroup]
        public TemporalSettings Temporal = TemporalSettings.DefaultSettings();
        
        private Matrix4x4 m_ProjectionMatrix;
        private Matrix4x4 m_PreviousViewProjectionMatrix;
        private float m_FlipFlop = 1.0f;
        private RenderTexture m_Accumulation;

        [FormerlySerializedAs("smaaShader")]
        public Shader Shader;
        
        public Texture2D AreaTex;
        public Texture2D SearchTex;
        
        private Camera m_Camera;
        public Camera Camera
        {
            get
            {
                if (m_Camera == null)
                    m_Camera = GetComponent<Camera>();

                return m_Camera;
            }
        }

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

        void Start()
        {
            if (!ImageEffectHelper.IsSupported(Shader, true, false, this))
                enabled = false;
        }

        void OnEnable()
        {
            // Make sure the helper textures are set
            if (AreaTex == null)
                AreaTex = Resources.Load<Texture2D>("AreaTex");

            if (SearchTex == null)
                SearchTex = Resources.Load<Texture2D>("SearchTex");
        }

        void OnDisable()
        {
            // Cleanup
            if (m_Material != null)
                DestroyImmediate(m_Material);

            if (m_Accumulation != null)
                DestroyImmediate(m_Accumulation);
        }

        void OnPreCull()
        {
            if (Temporal.Enabled)
            {
                m_ProjectionMatrix = Camera.projectionMatrix;
                m_FlipFlop -= (2.0f * m_FlipFlop);

                Matrix4x4 fuzz = Matrix4x4.identity;

                fuzz.m03 = (0.25f * m_FlipFlop) * Temporal.FuzzSize / Camera.pixelWidth;
                fuzz.m13 = (-0.25f * m_FlipFlop) * Temporal.FuzzSize / Camera.pixelHeight;

                Camera.projectionMatrix = fuzz * Camera.projectionMatrix;
            }
        }

        void OnPostRender()
        {
            if (Temporal.Enabled)
                Camera.ResetProjectionMatrix();
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            int width = Camera.pixelWidth;
            int height = Camera.pixelHeight;

            bool isFirstFrame = false;

            QualitySettings preset = Quality;

            if (Settings.Quality != QualityPreset.Custom)
                preset = QualitySettings.presetQualitySettings[(int)Settings.Quality];

            // Pass IDs
            int passEdgeDetection = (int)Settings.EdgeDetectionMethod;
            int passBlendWeights = 4;
            int passNeighborhoodBlending = 5;
            int passResolve = 6;

            // Reprojection setup
            var viewProjectionMatrix = GL.GetGPUProjectionMatrix(m_ProjectionMatrix, true) * Camera.worldToCameraMatrix;

            // Uniforms
            Material.SetTexture("_AreaTex", AreaTex);
            Material.SetTexture("_SearchTex", SearchTex);

            Material.SetVector("_Metrics", new Vector4(1f / width, 1f / height, width, height));
            Material.SetVector("_Params1", new Vector4(preset.Threshold, preset.DepthThreshold, preset.MaxSearchSteps, preset.MaxDiagonalSearchSteps));
            Material.SetVector("_Params2", new Vector2(preset.CornerRounding, preset.LocalContrastAdaptationFactor));

            Material.SetMatrix("_ReprojectionMatrix", m_PreviousViewProjectionMatrix * Matrix4x4.Inverse(viewProjectionMatrix));

            // float subsampleIndex = 1.0f + (float) (m_FlipFlop < 0.0f); // Retarded C# should learn some C
            float subsampleIndex = (m_FlipFlop < 0.0f) ? 2.0f : 1.0f;
            Material.SetVector("_SubsampleIndices", new Vector4(subsampleIndex, subsampleIndex, subsampleIndex, 0.0f));

            // Handle predication & depth-based edge detection
            Shader.DisableKeyword("USE_PREDICATION");

            if (Settings.EdgeDetectionMethod == EdgeDetectionMethod.Depth)
            {
                Camera.depthTextureMode |= DepthTextureMode.Depth;
            }
            else if (Predication.Enabled)
            {
                Camera.depthTextureMode |= DepthTextureMode.Depth;
                Shader.EnableKeyword("USE_PREDICATION");
                Material.SetVector("_Params3", new Vector3(Predication.Threshold, Predication.Scale, Predication.Strength));
            }

            // Diag search & corner detection
            Shader.DisableKeyword("USE_DIAG_SEARCH");
            Shader.DisableKeyword("USE_CORNER_DETECTION");

            if (preset.DiagonalDetection)
                Shader.EnableKeyword("USE_DIAG_SEARCH");

            if (preset.CornerDetection)
                Shader.EnableKeyword("USE_CORNER_DETECTION");

            // UV-based reprojection (up to Unity 5.x)
            // TODO: use motion vectors when available!
            Shader.DisableKeyword("USE_UV_BASED_REPROJECTION");

            if (Temporal.Enabled)
                Shader.EnableKeyword("USE_UV_BASED_REPROJECTION");

            // Persistent textures and lazy-initializations
            if (m_Accumulation == null || (m_Accumulation.width != width || m_Accumulation.height != height))
            {
                if (m_Accumulation)
                    RenderTexture.ReleaseTemporary(m_Accumulation);

                m_Accumulation = RenderTexture.GetTemporary(width, height, 0, source.format, RenderTextureReadWrite.Linear);
                m_Accumulation.hideFlags = HideFlags.HideAndDontSave;

                isFirstFrame = true;
            }
            
            RenderTexture rt1 = TempRT(width, height, source.format);
            Graphics.Blit(null, rt1, Material, 0); // Clear

            // Edge Detection
            Graphics.Blit(source, rt1, Material, passEdgeDetection);

            if (Settings.DebugPass == DebugPass.Edges)
            {
                Graphics.Blit(rt1, destination);
            }
            else
            {
                RenderTexture rt2 = TempRT(width, height, source.format);
                Graphics.Blit(null, rt2, Material, 0); // Clear

                // Blend Weights
                Graphics.Blit(rt1, rt2, Material, passBlendWeights);

                if (Settings.DebugPass == DebugPass.Weights)
                {
                    Graphics.Blit(rt2, destination);
                }
                else
                {
                    // Neighborhood Blending
                    Material.SetTexture("_BlendTex", rt2);
                    
                    if (Temporal.Enabled)
                    {
                        // Temporal filtering
                        Graphics.Blit(source, rt1, Material, passNeighborhoodBlending);

                        if (Settings.DebugPass == DebugPass.Accumulation)
                        {
                            Graphics.Blit(m_Accumulation, destination);
                        }
                        else if (!isFirstFrame)
                        {
                            Material.SetTexture("_AccumulationTex", m_Accumulation);
                            Graphics.Blit(rt1, destination, Material, passResolve);
                        }
                        else
                        {
                            Graphics.Blit(rt1, destination);
                        }

                        Graphics.Blit(rt1, m_Accumulation);
                        RenderTexture.active = null;
                    }
                    else
                    {
                        Graphics.Blit(source, destination, Material, passNeighborhoodBlending);
                    }
                }

                RenderTexture.ReleaseTemporary(rt2);
            }
            
            RenderTexture.ReleaseTemporary(rt1);

            // Store the future-previous frame's view-projection matrix
            m_PreviousViewProjectionMatrix = viewProjectionMatrix;
        }

        RenderTexture TempRT(int width, int height, RenderTextureFormat format)
        {
            // Skip the depth & stencil buffer creation when DebugPass is set to avoid flickering
            // TODO: Stencil buffer not working for some reason
            // int depthStencilBits = DebugPass == DebugPass.Off ? 24 : 0;
            int depthStencilBits = 0;
            return RenderTexture.GetTemporary(width, height, depthStencilBits, format, RenderTextureReadWrite.Linear);
        }
    }
}
