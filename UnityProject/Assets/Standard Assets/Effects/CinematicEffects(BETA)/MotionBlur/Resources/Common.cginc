// Common header (uniforms and a blit vertex shader)

#include "UnityCG.cginc"

// Main source texture
sampler2D _MainTex;
float4 _MainTex_TexelSize;

// Color accumulation texture
sampler2D _AccTex;
float4 _AccTex_TexelSize;

// Camera depth texture
sampler2D_float _CameraDepthTexture;
float4 _CameraDepthTexture_TexelSize;

// Camera motion vectors texture
sampler2D_half _CameraMotionVectorsTexture;
float4 _CameraMotionVectorsTexture_TexelSize;

// Pakced velocity texture (2/10/10/10)
sampler2D_half _VelocityTex;
float4 _VelocityTex_TexelSize;

// NeighborMax texture
sampler2D_half _NeighborMaxTex;
float4 _NeighborMaxTex_TexelSize;

// Velocity scale factor
float _VelocityScale;

// TileMax filter parameters
int _TileMaxLoop;
float2 _TileMaxOffs;

// Maximum blur radius (in pixels)
half _MaxBlurRadius;

// Filter parameters/coefficients
int _LoopCount;

// Color accumulation blend ratio
float _AccRatio;

// Vertex shader for multiple texture blitting
struct v2f_multitex
{
    float4 pos : SV_POSITION;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
};

v2f_multitex vert_Multitex(appdata_full v)
{
    v2f_multitex o;
    o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    o.uv0 = v.texcoord.xy;
    o.uv1 = v.texcoord.xy;
#if UNITY_UV_STARTS_AT_TOP
    if (_MainTex_TexelSize.y < 0.0)
        o.uv1.y = 1.0 - v.texcoord.y;
#endif
    return o;
}
