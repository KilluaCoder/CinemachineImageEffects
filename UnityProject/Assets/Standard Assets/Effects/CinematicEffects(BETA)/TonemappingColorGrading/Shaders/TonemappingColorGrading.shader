Shader "Hidden/TonemappingColorGrading"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE

        #pragma vertex vert_img
        #pragma fragmentoption ARB_precision_hint_fastest
        #pragma target 3.0
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

#if ENABLE_TONEMAPPING
            // Global exposure
            color.rgb *= _Exposure;

            // ACES Tonemapping
            // See https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
            const half a = 2.51;
            const half b = 0.03;
            const half c = 2.43;
            const half d = 0.59;
            const half e = 0.14;
            color.rgb = saturate((color * (a * color + b)) / (color * (c * color + d) + e));
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

    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Fog { Mode off }

        // (0) Lut generator
        Pass
        {
            CGPROGRAM

                #pragma fragment frag

                sampler2D _UserLutTex;
                half4 _UserLutParams;

                half3 _Lift;
                half3 _Gamma;
                half3 _Gain;
                half _Contrast;
                half _Vibrance;
                half3 _HSV;
                half3 _ChannelMixerRed;
                half3 _ChannelMixerGreen;
                half3 _ChannelMixerBlue;
                sampler2D _CurveTex;
                half _Contribution;

                half3 rgb_to_hsv(half3 c)
                {
                    half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                    half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                    half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                    half d = q.x - min(q.w, q.y);
                    half e = 1.0e-10;
                    return half3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
                }

                half3 hsv_to_rgb(half3 c)
                {
                    half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                    half3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
                }

                // CG's fmod() is not the same as GLSL's mod() with negative values, we'll use our own
                inline half gmod(half x, half y)
                {
                    return x - y * floor(x / y);
                }

                half4 frag(v2f_img i) : SV_Target
                {
                    half3 neutral_lut = tex2D(_MainTex, i.uv).rgb;
                    half3 final_lut = neutral_lut;

                    // User lut + contrib
                    half3 user_luted = apply_lut(_UserLutTex, final_lut, _UserLutParams.xyz);
                    final_lut = lerp(final_lut, user_luted, _UserLutParams.w);

                    // Lift/gamma/gain
                    final_lut = _Gain * (_Lift * (1.0 - final_lut) + pow(final_lut, _Gamma));

                    // Hue/saturation/value
                    half3 hsv = rgb_to_hsv(final_lut);
                    hsv.x = gmod(hsv.x + _HSV.x, 1.0);
                    hsv.yz *= _HSV.yz;
                    final_lut = hsv_to_rgb(hsv);

                    // Contrast
                    final_lut = saturate((final_lut - 0.5) * _Contrast + 0.5);

                    // Vibrance
                    half sat = max(final_lut.r, max(final_lut.g, final_lut.b)) - min(final_lut.r, min(final_lut.g, final_lut.b));
                    final_lut = lerp(Luminance(final_lut), final_lut, (1.0 + (_Vibrance * (1.0 - (sign(_Vibrance) * sat)))));
                    
                    // Color mixer
                    final_lut = (final_lut.rrr * _ChannelMixerRed) + (final_lut.ggg * _ChannelMixerGreen) + (final_lut.bbb * _ChannelMixerBlue);

                    // Curves
                    half mr = tex2D(_CurveTex, half2(final_lut.r, 0.5)).a;
                    half mg = tex2D(_CurveTex, half2(final_lut.g, 0.5)).a;
                    half mb = tex2D(_CurveTex, half2(final_lut.b, 0.5)).a;
                    final_lut = half3(mr, mg, mb);
                    half r = tex2D(_CurveTex, half2(final_lut.r, 0.5)).r;
                    half g = tex2D(_CurveTex, half2(final_lut.g, 0.5)).g;
                    half b = tex2D(_CurveTex, half2(final_lut.b, 0.5)).b;
                    final_lut = half3(r, g, b);

                    return half4(final_lut, 1.0);
                }

            ENDCG
        }

        // (1) Tonemapping & grading
        Pass
        {
            CGPROGRAM
                #pragma multi_compile __ GAMMA_COLORSPACE
                #pragma multi_compile __ ENABLE_TONEMAPPING
                #pragma multi_compile __ ENABLE_COLOR_GRADING
                #pragma multi_compile __ ENABLE_EYE_ADAPTATION
                #pragma fragment frag_tcg
            ENDCG
        }

        // (2) The three following passes are used to get an average log luminance using a downsample pyramid
        Pass
        {
            CGPROGRAM
                #pragma fragment frag_log
            ENDCG
        }

        // (3)
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
                #pragma fragment frag_exp
            ENDCG
        }

        // (4)
        Pass
        {
            Blend Off

            CGPROGRAM
                #pragma fragment frag_exp
            ENDCG
        }

        // (5) Debug, to be removed
        Pass
        {
            CGPROGRAM
                #pragma fragment frag_debug
                half4 frag_debug(v2f_img i) : SV_Target
                {
                    half lum = tex2D(_MainTex, i.uv).r;
                    return half4(lum, lum, lum, 1.0);
                }
            ENDCG
        }
    }
}
