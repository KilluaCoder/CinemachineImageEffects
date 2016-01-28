#include "UnityCG.cginc"

sampler2D _MainTex;
half4 _MainTex_TexelSize;

half _Exposure;
sampler2D _LutTex;
half4 _LutParams;

sampler2D _LumTex;
half _AdaptationSpeed;
half _MiddleGrey;
half _AdaptationMin;
half _AdaptationMax;

inline half LinToPerceptual(half3 color)
{
    half lum = Luminance(color);
    return log(max(lum, 0.001));
}

inline half PerceptualToLin(half f)
{
    return exp(f);
}

half4 frag_log(v2f_img i) : SV_Target
{
    half sum = 0.0;
    sum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1,-1)).rgb);
    sum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2( 1, 1)).rgb);
    sum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1, 1)).rgb);
    sum += LinToPerceptual(tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2( 1,-1)).rgb);
    half avg = sum / 4.0;
    return half4(avg, avg, avg, avg);
}

half4 frag_exp(v2f_img i) : SV_Target
{
    half sum = 0.0;
    sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1,-1)).x;
    sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2( 1, 1)).x;
    sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2( 1,-1)).x;
    sum += tex2D(_MainTex, i.uv + _MainTex_TexelSize.xy * half2(-1, 1)).x;
    half avg = PerceptualToLin(sum / 4.0);
    return half4(avg, avg, avg, saturate(0.0125 * _AdaptationSpeed));
}

half3 apply_lut(sampler2D tex, half3 uv, half3 scaleOffset)
{
    uv.z *= scaleOffset.z;
    half shift = floor(uv.z);
    uv.xy = uv.xy * scaleOffset.z * scaleOffset.xy + 0.5 * scaleOffset.xy;
    uv.x += shift * scaleOffset.y;
    uv.xyz = lerp(tex2D(tex, uv.xy).rgb, tex2D(tex, uv.xy + half2(scaleOffset.y, 0)).rgb, uv.z - shift);
    return uv;
}

half3 tonemapACES(half3 color)
{
    color *= _Exposure;

    // See https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
    const half a = 2.51;
    const half b = 0.03;
    const half c = 2.43;
    const half d = 0.59;
    const half e = 0.14;
    return saturate((color * (a * color + b)) / (color * (c * color + d) + e));
}

half3 tonemapPhotographic(half3 color)
{
    color *= _Exposure;
    return 1.0 - exp2(-color);
}

half3 tonemapHabble(half3 color)
{
    const half a = 0.15;
    const half b = 0.50;
    const half c = 0.10;
    const half d = 0.20;
    const half e = 0.02;
    const half f = 0.30;
    const half w = 11.2;

    color *= _Exposure * 2.0;
    half3 curr = ((color * (a * color + c * b) + d * e) / (color * (a * color + b) + d * f)) - e / f;
    color = w;
    half3 whiteScale = 1.0 / (((color * (a * color + c * b) + d * e) / (color * (a * color + b) + d * f)) - e / f);
    return curr * whiteScale;
}

half3 tonemapHejiDawson(half3 color)
{
    const half a = 6.2;
    const half b = 0.5;
    const half c = 1.7;
    const half d = 0.06;

    color *= _Exposure;
    color = max(color, color - 0.004);
    color = (color * (a * color + b)) / (color * (a * color + c) + d);
    return color * color;
}

half3 tonemapReinhard(half3 color)
{
    half lum = Luminance(color);
    half lumTm = lum * _Exposure;
    half scale = lumTm / (1.0 + lumTm);
    return color * scale / lum;
}
        
half4 frag_tcg(v2f_img i) : SV_Target
{
    half4 color = tex2D(_MainTex, i.uv);

#if GAMMA_COLORSPACE
    color.rgb = GammaToLinearSpace(color.rgb);
#endif

#if ENABLE_EYE_ADAPTATION
    // Fast eye adaptation
    half avg_luminance = tex2D(_LumTex, i.uv).x;
    half linear_exposure = _MiddleGrey / avg_luminance;
    color.rgb *= max(_AdaptationMin, min(_AdaptationMax, linear_exposure));
#endif

#if defined(TONEMAPPING_ACES)
    color.rgb = tonemapACES(color.rgb);
#elif defined(TONEMAPPING_HABBLE)
    color.rgb = tonemapHabble(color.rgb);
#elif defined(TONEMAPPING_HEJI_DAWSON)
    color.rgb = tonemapHejiDawson(color.rgb);
#elif defined(TONEMAPPING_PHOTOGRAPHIC)
    color.rgb = tonemapPhotographic(color.rgb);
#elif defined(TONEMAPPING_REINHARD)
    color.rgb = tonemapReinhard(color.rgb);
#endif

#if ENABLE_COLOR_GRADING
    // LUT color grading
    half3 color_corrected = apply_lut(_LutTex, saturate(color.rgb), _LutParams.xyz);
    color.rgb = lerp(color.rgb, color_corrected, _LutParams.w);
#endif

#if GAMMA_COLORSPACE
    color.rgb = LinearToGammaSpace(color.rgb);
#endif

    return color;
}
