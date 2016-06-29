// Miscellaneous shader passes

#include "Common.cginc"

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
