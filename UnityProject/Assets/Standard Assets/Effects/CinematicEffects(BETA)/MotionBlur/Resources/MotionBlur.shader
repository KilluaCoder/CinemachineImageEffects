// Prefilter passes for the motion blur effect
Shader "Hidden/Image Effects/Cinematic/MotionBlur"
{
    Properties
    {
        _MainTex        ("", 2D) = ""{}
        _VelocityTex    ("", 2D) = ""{}
        _NeighborMaxTex ("", 2D) = ""{}
        _History1Tex    ("", 2D) = ""{}
        _History2Tex    ("", 2D) = ""{}
        _History3Tex    ("", 2D) = ""{}
        _History4Tex    ("", 2D) = ""{}
    }
    Subshader
    {
        // Pass 0: Velocity texture setup
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Prefilter.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_VelocitySetup
            #pragma target 3.0
            ENDCG
        }
        // Pass 1: TileMax filter (4 pixels width with normalization)
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Prefilter.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_TileMax4
            #pragma target 3.0
            ENDCG
        }
        // Pass 2: TileMax filter (2 pixels width)
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Prefilter.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_TileMax2
            #pragma target 3.0
            ENDCG
        }
        // Pass 3: TileMax filter (variable width)
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Prefilter.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_TileMaxV
            #pragma target 3.0
            ENDCG
        }
        // Pass 4: NeighborMax filter
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Prefilter.cginc"
            #pragma vertex vert_img
            #pragma fragment frag_NeighborMax
            #pragma target 3.0
            ENDCG
        }
        // Pass 5: Reconstruction filter
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Reconstruction.cginc"
            #pragma vertex vert_Multitex
            #pragma fragment frag_Reconstruction
            #pragma target 3.0
            ENDCG
        }
        // Pass 6: Frame blending
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Misc.cginc"
            #pragma vertex vert_Multitex
            #pragma fragment frag_FrameBlending
            #pragma target 3.0
            ENDCG
        }
        // Pass 7: Debug mode (velocity)
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Misc.cginc"
            #pragma vertex vert_Multitex
            #pragma fragment frag_Velocity
            ENDCG
        }
        // Pass 8: Debug mode (NeighborMax)
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Misc.cginc"
            #pragma vertex vert_Multitex
            #pragma fragment frag_NeighborMax
            ENDCG
        }
        // Pass 9: Debug mode (Depth)
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #include "Misc.cginc"
            #pragma vertex vert_Multitex
            #pragma fragment frag_Depth
            ENDCG
        }
    }
}
