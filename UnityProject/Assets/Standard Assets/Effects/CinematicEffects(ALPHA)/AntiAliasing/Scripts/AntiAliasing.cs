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

using UnityEngine;
using System;
using UnityEngine.Serialization;

namespace Smaa
{
    /// <summary>
    /// Implementation of Subpixel Morphological Antialiasing for Unity.
    /// </summary>
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Image Effects/Subpixel Morphological Anti-aliasing")]
    public class AntiAliasing : MonoBehaviour
    {
        /// <summary>
        /// Render target mode. Keep it to <see cref="HDRMode.Auto"/> unless you know what you're doing.
        /// </summary>
        public HDRMode Hdr = HDRMode.Auto;

        /// <summary>
        /// Use this to fine tune your settings when working in Custom quality mode.
        /// </summary>
        /// <seealso cref="DebugPass"/>
        public DebugPass DebugPass = DebugPass.Off;

        /// <summary>
        /// Quality preset to use. Set to <see cref="QualityPreset.Custom"/> to fine tune every setting.
        /// </summary>
        /// <seealso cref="QualityPreset"/>
        public QualityPreset Quality = QualityPreset.High;

        /// <summary>
        /// You have three edge detection methods to choose from: luma, color or depth.
        /// They represent different quality/performance and anti-aliasing/sharpness tradeoffs, so our recommendation is
        /// for you to choose the one that best suits your particular scenario.
        /// </summary>
        /// <seealso cref="EdgeDetectionMethod"/>
        public EdgeDetectionMethod DetectionMethod = EdgeDetectionMethod.Color;

        /// <summary>
        /// Predicated thresholding allows to better preserve texture details and to improve performance, by decreasing
        /// the number of detected edges using an additional buffer (the detph buffer).
        ///
        /// It locally decreases the luma or color threshold if an edge is found in an additional buffer (so the global
        /// threshold can be higher).
        /// 
        /// Note: currently useless without stencil buffer support. It actually makes the effect run slower.
        /// </summary>
        public bool UsePredication = false; // Unused with EdgeDetectionMethod.Depth

        /// <summary>
        /// Holds the custom preset to use with <see cref="QualityPreset.Custom"/>.
        /// </summary>
        public Preset CustomPreset;

        /// <summary>
        /// Holds the custom preset to use when <see cref="SMAA.UsePredication"/> is enabled.
        /// </summary>
        public PredicationPreset CustomPredicationPreset;

        /// <summary>
        /// The shader used by the processing effect.
        /// </summary>
        [FormerlySerializedAs("smaaShader")]
        public Shader Shader;

        /// <summary>
        /// This texture allows to obtain the area for a certain pattern and distances to the left and to right of the
        /// line. Automatically set by the component if <c>null</c>.
        /// </summary>
        public Texture2D AreaTex;

        /// <summary>
        /// This texture allows to know how many pixels we must advance in the last step of our line search algorithm,
        /// with a single fetch. Automatically set by the component if <c>null</c>.
        /// </summary>
        public Texture2D SearchTex;

        /// <summary>
        /// A reference to the camera this component is added to.
        /// </summary>
        protected Camera m_Camera;

        /// <summary>
        /// The internal <see cref="Preset"/> used for <c>Low</c>, <c>Medium</c>, <c>High</c>, <c>Ultra</c>.
        /// </summary>
        protected Preset[] m_StdPresets;

        /// <summary>
        /// The internal <c>Material</c> instance. Use <see cref="Material"/> instead.
        /// </summary>
        protected Material m_Material;

        /// <summary>
        /// The <c>Material</c> instance used by the post-processing effect.
        /// </summary>
        public Material Material
        {
            get
            {
                if (m_Material == null)
                {
                    m_Material = new Material(Shader);
                    m_Material.hideFlags = HideFlags.HideAndDontSave;
                }

                return m_Material;
            }
        }

        /// <summary>
        /// Controls the UV-based implementation of the SMAA T2x.
        /// TODO: motion vector support, eventually.
        /// </summary>
        public bool UseTemporalFiltering = true;

        /// <summary>
        /// The unaltered projection matrix of the camera, used for calculating the
        /// reprojection matrix for the UV-based velocity reprojection code path.
        /// </summary>
        private Matrix4x4 m_ProjectionMatrix;

        /// <summary>
        /// The view-projection matrix of the previous frame, used for calculating the
        /// reprojection matrix for the UV-based velocity reprojection code path.
        /// </summary>
        private Matrix4x4 m_PreviousViewProjectionMatrix;

        /// <summary>
        /// A flip-flop counter to keep track of the correct jitter offset used for jittering
        /// the camera's projection matrix for frame-buffer accumulation.
        /// </summary>
        private float m_FlipFlop = 1.0f;

        /// <summary>
        /// The accumulation buffer used for implementing 2x temporal anti-aliasing.
        /// </summary>
        private RenderTexture m_Accumulation;


        /// <summary>
        /// The size of the fuzz-displacement (jitter) in pixels applied to the camera's
        /// perspective projection matrix. Used for 2x temporal anti-aliasing.
        /// </summary>
        [Range(0.5f, 10.0f)]
        public float FuzzSize = 2.0f;

        void OnEnable()
        {
            // Make sure the helper textures are set
            if (AreaTex == null)
                AreaTex = Resources.Load<Texture2D>("AreaTex");

            if (SearchTex == null)
                SearchTex = Resources.Load<Texture2D>("SearchTex");

            // Misc
            m_Camera = GetComponent<Camera>();

            // Create default presets
            CreatePresets();
        }

        void Start()
        {
            // Disable if we don't support image effects
            if (!SystemInfo.supportsImageEffects)
            {
                Debug.LogWarning("Image effects aren't supported on this device");
                enabled = false;
                return;
            }

            // Disable the image effect if the shader can't run on the user's graphics card
            if (!Shader || !Shader.isSupported)
            {
                Debug.LogWarning("The shader is null or unsupported on this device");
                enabled = false;
            }
        }

        void OnDisable()
        {
            // Cleanup
            if (m_Material != null)
            {
                #if UNITY_EDITOR
                DestroyImmediate(m_Material);
                #else
                Destroy(m_Material);
                #endif
            }

            if (m_Accumulation != null)
            {
                #if UNITY_EDITOR
                DestroyImmediate(m_Accumulation);
                #else
                Destroy(m_Accumulation);
                #endif
            }
        }

        void OnPreCull()
        {
            if (UseTemporalFiltering)
            {
                m_ProjectionMatrix = m_Camera.projectionMatrix;
                m_FlipFlop -= (2.0f * m_FlipFlop);

                Matrix4x4 fuzz = Matrix4x4.identity;

                fuzz.m03 = (0.25f * m_FlipFlop) * FuzzSize / m_Camera.pixelWidth;
                fuzz.m13 = (-0.25f * m_FlipFlop) * FuzzSize / m_Camera.pixelHeight;

                m_Camera.projectionMatrix = fuzz * m_Camera.projectionMatrix;
            }
        }

        void OnPostRender()
        {
            if (UseTemporalFiltering)
                m_Camera.ResetProjectionMatrix();
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            int width = m_Camera.pixelWidth;
            int height = m_Camera.pixelHeight;

            bool isFirstFrame = false;

            Preset preset = CustomPreset;

            if (Quality != QualityPreset.Custom)
                preset = m_StdPresets[(int)Quality];

            // Pass IDs
            int passEdgeDetection = (int)DetectionMethod;
            int passBlendWeights = 4;
            int passNeighborhoodBlending = 5;
            int passResolve = 6;

            // Render format
            RenderTextureFormat renderFormat = source.format;

            if (Hdr == HDRMode.Off)
                renderFormat = RenderTextureFormat.ARGB32;
            else if (Hdr == HDRMode.On)
                renderFormat = RenderTextureFormat.ARGBHalf;

            // Reprojection setup
            var viewProjectionMatrix = GL.GetGPUProjectionMatrix(m_ProjectionMatrix, true) * m_Camera.worldToCameraMatrix;

            // Uniforms
            Material.SetTexture("_AreaTex", AreaTex);
            Material.SetTexture("_SearchTex", SearchTex);

            Material.SetVector("_Metrics", new Vector4(1f / width, 1f / height, width, height));
            Material.SetVector("_Params1", new Vector4(preset.Threshold, preset.DepthThreshold, preset.MaxSearchSteps, preset.MaxSearchStepsDiag));
            Material.SetVector("_Params2", new Vector2(preset.CornerRounding, preset.LocalContrastAdaptationFactor));

            Material.SetMatrix("_ReprojectionMatrix", m_PreviousViewProjectionMatrix * Matrix4x4.Inverse(viewProjectionMatrix));

            // float subsampleIndex = 1.0f + (float) (m_FlipFlop < 0.0f); // Retarded C# should learn some C
            float subsampleIndex = (m_FlipFlop < 0.0f) ? 2.0f : 1.0f;
            Material.SetVector("_SubsampleIndices", new Vector4(subsampleIndex, subsampleIndex, subsampleIndex, 0.0f));

            // Handle predication & depth-based edge detection
            Shader.DisableKeyword("USE_PREDICATION");

            if (DetectionMethod == EdgeDetectionMethod.Depth)
            {
                m_Camera.depthTextureMode |= DepthTextureMode.Depth;
            }
            else if (UsePredication)
            {
                m_Camera.depthTextureMode |= DepthTextureMode.Depth;
                Shader.EnableKeyword("USE_PREDICATION");
                Material.SetVector("_Params3", new Vector3(CustomPredicationPreset.Threshold, CustomPredicationPreset.Scale, CustomPredicationPreset.Strength));
            }

            // Diag search & corner detection
            Shader.DisableKeyword("USE_DIAG_SEARCH");
            Shader.DisableKeyword("USE_CORNER_DETECTION");

            if (preset.DiagDetection)
                Shader.EnableKeyword("USE_DIAG_SEARCH");

            if (preset.CornerDetection)
                Shader.EnableKeyword("USE_CORNER_DETECTION");

            // UV-based reprojection (up to Unity 5.x, TODO: use motion vectors when available!)
            Shader.DisableKeyword("USE_UV_BASED_REPROJECTION");

            if (UseTemporalFiltering)
                Shader.EnableKeyword("USE_UV_BASED_REPROJECTION");

            // Persistent textures and lazy-initializations
            if (m_Accumulation == null || (m_Accumulation.width != width || m_Accumulation.height != height))
            {
                if (m_Accumulation)
                    RenderTexture.ReleaseTemporary(m_Accumulation);

                m_Accumulation = RenderTexture.GetTemporary(width, height, 0, renderFormat, RenderTextureReadWrite.Linear);
                m_Accumulation.hideFlags = HideFlags.HideAndDontSave;

                isFirstFrame = true;
            }
            
            RenderTexture rt1 = TempRT(width, height, renderFormat);
            Graphics.Blit(null, rt1, Material, 0); // Clear

            // Edge Detection
            Graphics.Blit(source, rt1, Material, passEdgeDetection);

            if (DebugPass == DebugPass.Edges)
            {
                Graphics.Blit(rt1, destination);
            }
            else
            {
                RenderTexture rt2 = TempRT(width, height, renderFormat);
                Graphics.Blit(null, rt2, Material, 0); // Clear

                // Blend Weights
                Graphics.Blit(rt1, rt2, Material, passBlendWeights);

                if (DebugPass == DebugPass.Weights)
                {
                    Graphics.Blit(rt2, destination);
                }
                else
                {
                    // Neighborhood Blending
                    Material.SetTexture("_BlendTex", rt2);
                    
                    if (UseTemporalFiltering)
                    {
                        // Temporal filtering
                        Graphics.Blit(source, rt1, Material, passNeighborhoodBlending);

                        if (DebugPass == DebugPass.Accumulation)
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

        void CreatePresets()
        {
            m_StdPresets = new Preset[4];

            // Low
            m_StdPresets[0] = new Preset
            {
                Threshold = 0.15f,
                MaxSearchSteps = 4,
                DiagDetection = false,
                CornerDetection = false
            };

            // Medium
            m_StdPresets[1] = new Preset
            {
                Threshold = 0.1f,
                MaxSearchSteps = 8,
                DiagDetection = false,
                CornerDetection = false
            };

            // High
            m_StdPresets[2] = new Preset
            {
                Threshold = 0.1f,
                MaxSearchSteps = 16,
                MaxSearchStepsDiag = 8,
                CornerRounding = 25
            };

            // Ultra
            m_StdPresets[3] = new Preset
            {
                Threshold = 0.05f,
                MaxSearchSteps = 32,
                MaxSearchStepsDiag = 16,
                CornerRounding = 25
            };
        }
    }
}
