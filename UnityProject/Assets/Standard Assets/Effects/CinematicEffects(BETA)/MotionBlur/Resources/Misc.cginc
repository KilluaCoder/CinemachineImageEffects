// Miscellaneous shader passes

#include "Common.cginc"

// Frame blending shader
half4 frag_FrameBlending(v2f_multitex i) : SV_Target
{
    half4 src = tex2D(_MainTex, i.uv0);

    half3 acc = src.rgb;
    half w = 1;

    acc += tex2D(_History1Tex, i.uv1) * _History1Weight;
    w += _History1Weight;

    acc += tex2D(_History2Tex, i.uv1) * _History2Weight;
    w += _History2Weight;

    acc += tex2D(_History3Tex, i.uv1) * _History3Weight;
    w += _History3Weight;

    acc += tex2D(_History4Tex, i.uv1) * _History4Weight;
    w += _History4Weight;

    return half4(acc / w, src.a);
}

// Debug visualization shaders
half4 frag_Velocity(v2f_multitex i) : SV_Target
{
    half2 v = tex2D(_VelocityTex, i.uv1).xy;
    return half4(v, 0.5, 1);
}

half4 frag_NeighborMax(v2f_multitex i) : SV_Target
{
    half2 v = tex2D(_NeighborMaxTex, i.uv1).xy;
    v = (v / _MaxBlurRadius + 1) / 2;
    return half4(v, 0.5, 1);
}

half4 frag_Depth(v2f_multitex i) : SV_Target
{
    half z = frac(tex2D(_VelocityTex, i.uv1).z * 128);
    return half4(z, z, z, 1);
}
