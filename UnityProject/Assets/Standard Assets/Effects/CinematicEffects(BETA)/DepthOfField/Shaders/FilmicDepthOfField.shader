Shader "Hidden/FilmicDepthOfField"
{

Properties
{
    _MainTex ("-", 2D) = "black"
    _SecondTex ("-", 2D) = "black"
    _ThirdTex ("-", 2D) = "black"
}

CGINCLUDE
#pragma target 3.0
#pragma fragmentoption ARB_precision_hint_fastest
#include "UnityCG.cginc"

sampler2D _MainTex;
sampler2D _SecondTex;
sampler2D _ThirdTex;
sampler2D _CameraDepthTexture;
uniform half4 _MainTex_TexelSize;
uniform half4 _Delta;
uniform half4 _BlurCoe;
uniform half4 _BlurParams;
uniform half4 _BoostParams;
uniform half4 _Convolved_TexelSize;
uniform half4 _Param0;
uniform float4 _Offsets;
uniform half _Param1;

uniform half4 _MainTex_ST;
uniform half4 _SecondTex_ST;
uniform half4 _ThirdTex_ST;

///////////////////////////////////////////////////////////////////////////////
// Verter Shaders and declaration
///////////////////////////////////////////////////////////////////////////////

struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
};

struct v2fDepth
{
    half4 pos  : SV_POSITION;
    half2 uv   : TEXCOORD0;
};

struct v2fBlur
{
    float4 pos  : SV_POSITION;
    float2 uv   : TEXCOORD0;
    float4 uv01 : TEXCOORD1;
    float4 uv23 : TEXCOORD2;
    float4 uv45 : TEXCOORD3;
    float4 uv67 : TEXCOORD4;
    float4 uv89 : TEXCOORD5;
};

v2fDepth vert(appdata_img v)
{
    v2fDepth o;
    o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    o.uv = v.texcoord.xy;
#if UNITY_UV_STARTS_AT_TOP
    if (_MainTex_TexelSize.y < 0)
    o.uv.y = 1-o.uv.y;
#endif
    return o;
}

v2fDepth vertNoFlip(appdata_img v)
{
    v2fDepth o;
    o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    o.uv = v.texcoord.xy;
    return o;
}

v2f vert_d( appdata_img v )
{
    v2f o;
    o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
    o.uv1.xy = v.texcoord.xy;
    o.uv.xy = v.texcoord.xy;

#if UNITY_UV_STARTS_AT_TOP
    if (_MainTex_TexelSize.y < 0)
    o.uv.y = 1-o.uv.y;
#endif

    return o;
}

v2f vertFlip( appdata_img v )
{
    v2f o;
    o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
    o.uv1.xy = v.texcoord.xy;
    o.uv.xy = v.texcoord.xy;

#if UNITY_UV_STARTS_AT_TOP
    if (_MainTex_TexelSize.y < 0)
    o.uv.y = 1-o.uv.y;
    if (_MainTex_TexelSize.y < 0)
    o.uv1.y = 1-o.uv1.y;
#endif

    return o;
}

v2fBlur vertBlurPlusMinus (appdata_img v)
{
    v2fBlur o;
    o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
    o.uv.xy = v.texcoord.xy;
    o.uv01 =  v.texcoord.xyxy + _Offsets.xyxy * float4(1,1, -1,-1) * _MainTex_TexelSize.xyxy / 6.0;
    o.uv23 =  v.texcoord.xyxy + _Offsets.xyxy * float4(2,2, -2,-2) * _MainTex_TexelSize.xyxy / 6.0;
    o.uv45 =  v.texcoord.xyxy + _Offsets.xyxy * float4(3,3, -3,-3) * _MainTex_TexelSize.xyxy / 6.0;
    o.uv67 =  v.texcoord.xyxy + _Offsets.xyxy * float4(4,4, -4,-4) * _MainTex_TexelSize.xyxy / 6.0;
    o.uv89 =  v.texcoord.xyxy + _Offsets.xyxy * float4(5,5, -5,-5) * _MainTex_TexelSize.xyxy / 6.0;
    return o;
}

///////////////////////////////////////////////////////////////////////////////
// Helpers
///////////////////////////////////////////////////////////////////////////////

inline half3 getBoostAmount(half4 colorAndCoc)
{
    half boost = colorAndCoc.a * (colorAndCoc.a < 0.0f ?_BoostParams.x:_BoostParams.y);
    half luma = dot(colorAndCoc.rgb, half3(0.3h, 0.59h, 0.11h));
    return luma < _BoostParams.z ? half3(0.0h, 0.0h, 0.0h):colorAndCoc.rgb * boost.rrr;
}

inline half GetSignedCocFromDepth(half2 uv, bool useExplicit)
{
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
    d = Linear01Depth (d);

    if (useExplicit)
    {
        half coc = d < _BlurCoe.z ? clamp((_BlurParams.x * d + _BlurParams.y), -1.0f, 0.0f):clamp((_BlurParams.z * d + _BlurParams.w), 0.0f, 1.0f);
        return coc;
    }
    else
    {
        half aperture = _BlurParams.x;
        half focalLength = _BlurParams.y;
        half focusDistance01 = _BlurParams.z;
        half focusRange01 = _BlurParams.w;
        half coc = aperture * abs(d - focusDistance01) / (d + 1e-5f) - focusRange01;
        coc = (d < focusDistance01 ? -1.0h:1.0h) * clamp(coc, 0.0f, 1.0f);
        return coc;
    }
}

//TODO REMOVE ME
#define SCATTER_OVERLAP_SMOOTH (0.265h)
inline half BokehWeightDisc(half sampleDepth, half sampleDistance, half centerDepth)
{
    return smoothstep(-SCATTER_OVERLAP_SMOOTH, 0.0, sampleDepth - centerDepth*sampleDistance);
}
inline half2 BokehWeightDisc2(half sampleADepth, half sampleBDepth, half2 sampleDistance2, half centerSampleDepth)
{
    return smoothstep(half2(-SCATTER_OVERLAP_SMOOTH, -SCATTER_OVERLAP_SMOOTH), half2(0.0,0.0), half2(sampleADepth, sampleBDepth) - half2(centerSampleDepth, centerSampleDepth)*sampleDistance2);
}

///////////////////////////////////////////////////////////////////////////////
// Directional (hexagonal/octogonal) bokeh
///////////////////////////////////////////////////////////////////////////////

#define SAMPLE_NUM_L    6
#define SAMPLE_NUM_M    11
#define SAMPLE_NUM_H    16

half4 shapeDirectionalBlur(half2 uv, bool mergePass, int numSample, bool sampleDilatedFG)
{
    half4 centerTap = tex2Dlod (_MainTex, float4(uv,0,0));
    half  fgCoc  = centerTap.a;
    if (sampleDilatedFG)
    {
        fgCoc  = min(tex2Dlod(_SecondTex, half4(uv,0,0)).r, fgCoc);
    }

    half  bgRadius = smoothstep(0.0h, 0.85h, centerTap.a)  * _BlurCoe.y;
    half  fgRadius = smoothstep(0.0h, 0.85h, -fgCoc) * _BlurCoe.x;
    half2 radius = _MainTex_TexelSize.xy * max(bgRadius, fgRadius);

    half radOtherFgRad = radius/fgRadius;
    half radOtherBgRad = radius/bgRadius;

    half fgWeight = 0.001h;
    half bgWeight = 0.001h;
    half3 fgSum = half3(0,0,0);
    half3 bgSum = half3(0,0,0);

    for (int k = 0; k < numSample; k++)
    {
        half t = (half)k / half(numSample-1);
        half2 kVal = lerp(_Delta.xy, -_Delta.xy, t);
        half2 offset = kVal * radius;
        half2 texCoord = uv + offset;
        half4 sample0 = tex2Dlod(_MainTex, half4(texCoord,0,0));

        half isNear = max(0.0h, -sample0.a);
        half isFar  = max(0.0h, sample0.a);
        isNear *= 1- smoothstep(1.0h, 2.0h, radOtherFgRad);
        isFar  *= 1- smoothstep(1.0h, 2.0h, radOtherBgRad);

        fgWeight += isNear;
        fgSum += sample0.rgb * isNear;
        bgWeight += isFar;
        bgSum += sample0.rgb * isFar;
    }

    half3 fgColor = fgSum / (fgWeight + 0.0001h);
    half3 bgColor = bgSum / (bgWeight + 0.0001h);
    half bgBlend = saturate (2.0h * bgWeight / numSample);
    half fgBlend = saturate (2.0h * fgWeight / numSample);

    half3 finalBg = lerp(centerTap.rgb, bgColor, bgBlend);
    half3 finalColor = lerp(finalBg, fgColor, max(max(0.0h , -centerTap.a) , fgBlend));

    if (mergePass)
    {
        finalColor = min(finalColor, tex2Dlod(_ThirdTex, half4(uv,0,0)).rgb);
    }

    finalColor = lerp(centerTap.rgb, finalColor, saturate(bgBlend+fgBlend));
    return half4(finalColor, mergePass?-fgCoc:centerTap.a);
}

half4 fragShapeLowQuality(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, false, SAMPLE_NUM_L, false);
}

half4 fragShapeLowQualityDilateFG(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, false, SAMPLE_NUM_L, true);
}

half4 fragShapeLowQualityMerge(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, true, SAMPLE_NUM_L, false);
}

half4 fragShapeLowQualityMergeDilateFG(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, true, SAMPLE_NUM_L, true);
}

half4 fragShapeMediumQuality(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, false, SAMPLE_NUM_M, false);
}

half4 fragShapeMediumQualityDilateFG(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, false, SAMPLE_NUM_M, true);
}

half4 fragShapeMediumQualityMerge(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, true, SAMPLE_NUM_M, false);
}

half4 fragShapeMediumQualityMergeDilateFG(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, true, SAMPLE_NUM_M, true);
}

half4 fragShapeHighQuality(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, false, SAMPLE_NUM_H, false);
}

half4 fragShapeHighQualityDilateFG(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, false, SAMPLE_NUM_H, true);
}

half4 fragShapeHighQualityMerge(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, true, SAMPLE_NUM_H, false);
}

half4 fragShapeHighQualityMergeDilateFG(v2fDepth i) : COLOR
{
    return shapeDirectionalBlur(i.uv, true, SAMPLE_NUM_H, true);
}

//TODO remove the following

#define SAMPLE_NUM_L1   5.0h
#define SAMPLE_NUM_M1   10.0h
#define SAMPLE_NUM_H1   15.0h

half4 fragShape0(v2fDepth i) : COLOR
{
    half4 output = half4(0.0h, 0.0h, 0.0h, 0.0h);
    half totalWeight = 0.00000001h;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half2 radius = (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y)) * _MainTex_TexelSize.xy;
    for (int k = 0; k < SAMPLE_NUM_L; k++)
    {
        half t = (half)k / SAMPLE_NUM_L1;
        half2 kVal = lerp(_Delta.xy, -_Delta.xy, t);
        half2 offset = kVal * radius;
        half2 texCoord = i.uv + offset;
        half blur = tex2D (_SecondTex, texCoord).y;
        half weight = tex2D (_SecondTex, texCoord).x >= centerDepth ? 1.0h:abs(blur);
        weight = blur * blurriness >= 0.0h ? weight:0.0h;
        output += half4(weight, weight, weight, weight) * tex2D (_MainTex, texCoord);
        totalWeight += weight;
    }
    output *= (1.0h/totalWeight);

    return half4(output.xyz, 1.0h);
}

half4 fragShape1(v2fDepth i) : COLOR
{
    half4 output = half4(0.0h, 0.0h, 0.0h, 0.0h);
    half totalWeight = 0.00000001h;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half2 radius = (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y)) * _MainTex_TexelSize.xy;
    for (int k = 0; k < SAMPLE_NUM_L; k++)
    {
        half t = (half)k / SAMPLE_NUM_L1;
        half2 kVal = lerp(_Delta.xy, -_Delta.xy, t);
        half2 offset = kVal * radius;
        half2 texCoord = i.uv + offset;
        half blur = tex2D (_SecondTex, texCoord).y;
        half weight = tex2D (_SecondTex, texCoord).x >= centerDepth ? 1.0h:abs(blur);
        weight = blur * blurriness >= 0.0h ? weight:0.0h;
        output += half4(weight, weight, weight, weight) * tex2D (_MainTex, texCoord);
        totalWeight += weight;
    }

    output *= (1.0h/totalWeight);
    output = min(output, tex2D (_ThirdTex, i.uv));
    return half4(output.xyz, 1.0h);
}

half4 fragShape2(v2fDepth i) : COLOR
{
    half4 output = half4(0.0h, 0.0h, 0.0h, 0.0h);
    half totalWeight = 0.00000001h;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half2 radius = (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y)) * _MainTex_TexelSize.xy;
    for (int k = 0; k < SAMPLE_NUM_M; k++)
    {
        half t = (half)k / SAMPLE_NUM_M1;
        half2 kVal = lerp(_Delta.xy, -_Delta.xy, t);
        half2 offset = kVal * radius;
        half2 texCoord = i.uv + offset;
        half blur = tex2D (_SecondTex, texCoord).y;
        half weight = tex2D (_SecondTex, texCoord).x >= centerDepth ? 1.0h:abs(blur);
        weight = blur * blurriness >= 0.0h ? weight:0.0h;
        output += half4(weight, weight, weight, weight) * tex2D (_MainTex, texCoord);
        totalWeight += weight;
    }
    output *= (1.0h/totalWeight);
    return half4(output.xyz, 1.0h);
}

half4 fragShape3(v2fDepth i) : COLOR
{
    half4 output = half4(0.0h, 0.0h, 0.0h, 0.0h);
    half totalWeight = 0.00000001h;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half2 radius = (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y)) * _MainTex_TexelSize.xy;
    for (int k = 0; k < SAMPLE_NUM_M; k++)
    {
        half t = (half)k / SAMPLE_NUM_M1;
        half2 kVal = lerp(_Delta.xy, -_Delta.xy, t);
        half2 offset = kVal * radius;
        half2 texCoord = i.uv + offset;
        half blur = tex2D (_SecondTex, texCoord).y;
        half weight = tex2D (_SecondTex, texCoord).x >= centerDepth ? 1.0h:abs(blur);
        weight = blur * blurriness >= 0.0h ? weight:0.0h;
        output += half4(weight, weight, weight, weight) * tex2D (_MainTex, texCoord);
        totalWeight += weight;
    }

    output *= (1.0h/totalWeight);
    output = min(output, tex2D (_ThirdTex, i.uv));
    return half4(output.xyz, 1.0h);
}

half4 fragShape4(v2fDepth i) : COLOR
{
    half4 output = half4(0.0h, 0.0h, 0.0h, 0.0h);
    half totalWeight = 0.00000001h;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half2 radius = (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y)) * _MainTex_TexelSize.xy;
    for (int k = 0; k < SAMPLE_NUM_H; k++)
    {
        half t = (half)k / SAMPLE_NUM_H1;
        half2 kVal = lerp(_Delta.xy, -_Delta.xy, t);
        half2 offset = kVal * radius;
        half2 texCoord = i.uv + offset;
        half blur = tex2D (_SecondTex, texCoord).y;
        half weight = tex2D (_SecondTex, texCoord).x >= centerDepth ? 1.0h:abs(blur);
        weight = blur * blurriness >= 0.0h ? weight:0.0h;
        output += half4(weight, weight, weight, weight) * tex2D (_MainTex, texCoord);
        totalWeight += weight;
    }
    output *= (1.0h/totalWeight);
    return half4(output.xyz, 1.0h);
}

half4 fragShape5(v2fDepth i) : COLOR
{
    half4 output = half4(0.0h, 0.0h, 0.0h, 0.0h);
    half totalWeight = 0.00000001h;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half2 radius = (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y)) * _MainTex_TexelSize.xy;
    for (int k = 0; k < SAMPLE_NUM_H; k++)
    {
        half t = (half)k / SAMPLE_NUM_H1;
        half2 kVal = lerp(_Delta.xy, -_Delta.xy, t);
        half2 offset = kVal * radius;
        half2 texCoord = i.uv + offset;
        half blur = tex2D (_SecondTex, texCoord).y;
        half weight = tex2D (_SecondTex, texCoord).x >= centerDepth ? 1.0h:abs(blur);
        weight = blur * blurriness >= 0.0h ? weight:0.0h;
        output += half4(weight, weight, weight, weight) * tex2D (_MainTex, texCoord);
        totalWeight += weight;
    }

    output *= (1.0h/totalWeight);
    output = min(output, tex2D (_ThirdTex, i.uv));
    return half4(output.xyz, 1.0h);
}

///////////////////////////////////////////////////////////////////////////////
// Disk Bokeh
///////////////////////////////////////////////////////////////////////////////

static const half3 DiscBokeh48[48] =
{
    //48 tap regularly spaced circular kernel (x,y, length)
    //fill free to change the shape to try other bokehs style :)
    half3( 0.99144h, 0.13053h, 1.0h),
    half3( 0.92388h, 0.38268h, 1.0h),
    half3( 0.79335h, 0.60876h, 1.0h),
    half3( 0.60876h, 0.79335h, 1.0h),
    half3( 0.38268h, 0.92388h, 1.0h),
    half3( 0.13053h, 0.99144h, 1.0h),
    half3(-0.13053h, 0.99144h, 1.0h),
    half3(-0.38268h, 0.92388h, 1.0h),
    half3(-0.60876h, 0.79335h, 1.0h),
    half3(-0.79335h, 0.60876h, 1.0h),
    half3(-0.92388h, 0.38268h, 1.0h),
    half3(-0.99144h, 0.13053h, 1.0h),
    half3(-0.99144h,-0.13053h, 1.0h),
    half3(-0.92388h,-0.38268h, 1.0h),
    half3(-0.79335h,-0.60876h, 1.0h),
    half3(-0.60876h,-0.79335h, 1.0h),
    half3(-0.38268h,-0.92388h, 1.0h),
    half3(-0.13053h,-0.99144h, 1.0h),
    half3( 0.13053h,-0.99144h, 1.0h),
    half3( 0.38268h,-0.92388h, 1.0h),
    half3( 0.60876h,-0.79335h, 1.0h),
    half3( 0.79335h,-0.60876h, 1.0h),
    half3( 0.92388h,-0.38268h, 1.0h),
    half3( 0.99144h,-0.13053h, 1.0h),
    half3( 0.64732h, 0.12876h, 0.66h),
    half3( 0.54877h, 0.36668h, 0.66h),
    half3( 0.36668h, 0.54877h, 0.66h),
    half3( 0.12876h, 0.64732h, 0.66h),
    half3(-0.12876h, 0.64732h, 0.66h),
    half3(-0.36668h, 0.54877h, 0.66h),
    half3(-0.54877h, 0.36668h, 0.66h),
    half3(-0.64732h, 0.12876h, 0.66h),
    half3(-0.64732h,-0.12876h, 0.66h),
    half3(-0.54877h,-0.36668h, 0.66h),
    half3(-0.36668h,-0.54877h, 0.66h),
    half3(-0.12876h,-0.64732h, 0.66h),
    half3( 0.12876h,-0.64732h, 0.66h),
    half3( 0.36668h,-0.54877h, 0.66h),
    half3( 0.54877h,-0.36668h, 0.66h),
    half3( 0.64732h,-0.12876h, 0.66h),
    half3( 0.30488h, 0.12629h, 0.33h),
    half3( 0.12629h, 0.30488h, 0.33h),
    half3(-0.12629h, 0.30488h, 0.33h),
    half3(-0.30488h, 0.12629h, 0.33h),
    half3(-0.30488h,-0.12629h, 0.33h),
    half3(-0.12629h,-0.30488h, 0.33h),
    half3( 0.12629h,-0.30488h, 0.33h),
    half3( 0.30488h,-0.12629h, 0.33h)
};

inline float4 circleSignedCocBokeh(float2 uv, bool sampleDilatedFG, int increment)
{
    half4 centerTap = tex2Dlod(_MainTex, half4(uv,0,0));
    half  fgCoc  = centerTap.a;
    if (sampleDilatedFG)
    {
        fgCoc  = min(tex2Dlod(_SecondTex, half4(uv,0,0)).r, fgCoc);
    }

    half  bgRadius = 0.5h * smoothstep(0.0h, 0.85h, centerTap.a)  * _BlurCoe.y;
    half  fgRadius = 0.5h * smoothstep(0.0h, 0.85h, -fgCoc) * _BlurCoe.x;
    half radius = max(bgRadius, fgRadius);
    if (radius < 1e-2f )
    {
        return half4(centerTap.rgb, 0);
    }

    half2 poissonScale = radius * _MainTex_TexelSize.xy;
    half fgWeight = max(0,-centerTap.a);
    half bgWeight = max(0, centerTap.a);
    half3 fgSum = centerTap.rgb * fgWeight;
    half3 bgSum = centerTap.rgb * bgWeight;

    half radOtherFgRad = radius/fgRadius;
    half radOtherBgRad = radius/bgRadius;

    for (int l = 0; l < 48; l+= increment)
    {
        half2 sampleUV = uv + DiscBokeh48[l].xy * poissonScale.xy;
        half4 sample0  = tex2Dlod(_MainTex, half4(sampleUV,0,0));

        half isNear = max(0.0h, -sample0.a);
        half isFar  = max(0.0h, sample0.a);
        isNear *= 1- smoothstep(1.0h, 2.0h, DiscBokeh48[l].z * radOtherFgRad);
        isFar  *= 1- smoothstep(1.0h, 2.0h, DiscBokeh48[l].z * radOtherBgRad);

        fgWeight += isNear;
        fgSum += sample0.rgb * isNear;
        bgWeight += isFar;
        bgSum += sample0.rgb * isFar;
    }

    half3 fgColor = fgSum / (fgWeight + 0.0001h);
    half3 bgColor = bgSum / (bgWeight + 0.0001h);
    half bgBlend = saturate (2.0h * bgWeight / 49.0h);
    half fgBlend = saturate (2.0h * fgWeight / 49.0h);

    half3 finalBg = lerp(centerTap.rgb, bgColor, bgBlend);
    half3 finalColor = lerp(finalBg, fgColor, max(max(0.0h , -centerTap.a) , fgBlend));
    half4 returnValue = half4(finalColor, fgBlend );

    return returnValue;
}

float4 fragCircleSignedCocBokehWithDilatedFG (v2fDepth i) : SV_Target
{
    return circleSignedCocBokeh(i.uv, true, 1);
}

float4 fragCircleSignedCocBokeh (v2fDepth i) : SV_Target
{
    return circleSignedCocBokeh(i.uv, false, 1);
}

float4 fragCircleSignedCocBokehWithDilatedFGLow (v2fDepth i) : SV_Target
{
    return circleSignedCocBokeh(i.uv, true, 2);
}

float4 fragCircleSignedCocBokehLow (v2fDepth i) : SV_Target
{
    return circleSignedCocBokeh(i.uv, false, 2);
}

//TODO remove me
#define DISC_SAMPLE_NUM0    12
static const half3 DiscKernel0[DISC_SAMPLE_NUM0] =
{
    half3(-0.326212h, -0.405810h, 0.520669h),
    half3(-0.840144h, -0.073580h, 0.843360h),
    half3(-0.695914h, 0.457137h, 0.832629h),
    half3(-0.203345h, 0.620716h, 0.653175h),
    half3(0.962340h, -0.194983h, 0.981894h),
    half3(0.473434h, -0.480026h, 0.674214h),
    half3(0.519456h, 0.767022h, 0.926368h),
    half3(0.185461h, -0.893124h, 0.912177h),
    half3(0.507431h, 0.064425h, 0.511504h),
    half3(0.896420h, 0.412458h, 0.986758h),
    half3(-0.321940h, -0.932615h, 0.986619h),
    half3(-0.791559h, -0.597710h, 0.991878h)
};

#define DISC_SAMPLE_NUM1    28
static const half3 DiscKernel1[DISC_SAMPLE_NUM1] =
{
    half3(0.62463h, 0.54337h, 0.82790h),
    half3(-0.13414h, -0.94488h, 0.95435h),
    half3(0.38772h, -0.43475h, 0.58253h),
    half3(0.12126h, -0.19282h, 0.22778h),
    half3(-0.20388h, 0.11133h, 0.23230h),
    half3(0.83114h, -0.29218h, 0.88100h),
    half3(0.10759h, -0.57839h, 0.58831h),
    half3(0.28285h, 0.79036h, 0.83945h),
    half3(-0.36622h, 0.39516h, 0.53876h),
    half3(0.75591h, 0.21916h, 0.78704h),
    half3(-0.52610h, 0.02386h, 0.52664h),
    half3(-0.88216h, -0.24471h, 0.91547h),
    half3(-0.48888h, -0.29330h, 0.57011h),
    half3(0.44014h, -0.08558h, 0.44838h),
    half3(0.21179h, 0.51373h, 0.55567h),
    half3(0.05483h, 0.95701h, 0.95858h),
    half3(-0.59001h, -0.70509h, 0.91938h),
    half3(-0.80065h, 0.24631h, 0.83768h),
    half3(-0.19424h, -0.18402h, 0.26757h),
    half3(-0.43667h, 0.76751h, 0.88304h),
    half3(0.21666h, 0.11602h, 0.24577h),
    half3(0.15696h, -0.85600h, 0.87027h),
    half3(-0.75821h, 0.58363h, 0.95682h),
    half3(0.99284h, -0.02904h, 0.99327h),
    half3(-0.22234h, -0.57907h, 0.62029h),
    half3(0.55052h, -0.66984h, 0.86704h),
    half3(0.46431h, 0.28115h, 0.54280h),
    half3(-0.07214h, 0.60554h, 0.60982h),
};

half4 fragCircleBokehLow(v2fDepth i) : COLOR
{
    half4 centerTap = tex2D(_MainTex, i.uv.xy);
    half4 sum = centerTap;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half radius = 0.5h * (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y));
    half2 poissonScale = radius * _MainTex_TexelSize.xy;
    blurriness = abs(blurriness);
    half2 weights;

    half sampleCount = max(blurriness * 0.25h, 0.025h);
    sum *= sampleCount;


    for (int l = 0; l < DISC_SAMPLE_NUM0; l++)
    {
        half4 sampleUV = i.uv.xyxy + DiscKernel0[l].xyxy * poissonScale.xyxy / half4(1.2h, 1.2h, DiscKernel0[l].zz);
        half4 sample0 = tex2D(_MainTex, sampleUV.xy);
        half4 sample1 = tex2D(_MainTex, sampleUV.zw);
        half sample0Blur = abs(tex2D (_SecondTex, sampleUV.xy).y);
        half sample1Blur = abs(tex2D (_SecondTex, sampleUV.zw).y);

        if (sample0Blur + sample1Blur != 0.0)
        {
            weights = BokehWeightDisc2(sample0Blur, sample1Blur, half2(DiscKernel0[l].z/1.2h, 1.0h), blurriness);
            sum += sample0 * weights.x + sample1 * weights.y;
            sampleCount += dot(weights, 1);
        }
    }

    half4 returnValue = sum / sampleCount;

    return returnValue;
}

half4 fragCircleMergeLow(v2fDepth i) : COLOR
{
    half4 blur = tex2D(_ThirdTex, i.uv.xy);
    half4 centerTap = tex2D(_MainTex, i.uv.xy);
    half4 sum = centerTap;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half radius = 0.5h * (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y));
    half2 poissonScale = radius * _MainTex_TexelSize.xy;
    blurriness = abs(blurriness);
    half weights;

    half sampleCount = max(blurriness * 0.25h, 0.025h);
    sum *= sampleCount;


    for (int l = 0; l < DISC_SAMPLE_NUM0; l++)
    {
        half2 sampleUV = i.uv.xy + DiscKernel0[l].xy * poissonScale.xy;
        half4 sample = tex2D(_MainTex, sampleUV);
        half sampleBlur = abs(tex2D (_SecondTex, sampleUV).y);

        if (sampleBlur != 0.0 )
        {
            weights = BokehWeightDisc(sampleBlur, DiscKernel0[l].z, blurriness);
            sum += sample * weights;
            sampleCount += weights;
        }
    }

    half4 returnValue = sum / sampleCount;
    returnValue = lerp(returnValue, blur, smoothstep(0.0, 0.85, blurriness));
    return (blurriness < 1e-2f ? centerTap:returnValue);
}

half4 fragCircleBokehHigh(v2fDepth i) : COLOR
{
    half4 centerTap = tex2D(_MainTex, i.uv.xy);
    half4 sum = centerTap;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half radius = 0.5h * (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y));
    half2 poissonScale = radius * _MainTex_TexelSize.xy;
    blurriness = abs(blurriness);
    half2 weights;

    half sampleCount = max(blurriness * 0.25h, 0.025h);
    sum *= sampleCount;


    for (int l = 0; l < DISC_SAMPLE_NUM1; l++)
    {
        half4 sampleUV = i.uv.xyxy + DiscKernel1[l].xyxy * poissonScale.xyxy / half4(1.2h, 1.2h, DiscKernel1[l].zz);
        half4 sample0 = tex2D(_MainTex, sampleUV.xy);
        half4 sample1 = tex2D(_MainTex, sampleUV.zw);
        half sample0Blur = abs(tex2D (_SecondTex, sampleUV.xy).y);
        half sample1Blur = abs(tex2D (_SecondTex, sampleUV.zw).y);

        if (sample0Blur + sample1Blur != 0.0)
        {
            weights = BokehWeightDisc2(sample0Blur, sample1Blur, half2(DiscKernel1[l].z/1.2h, 1.0h), blurriness);
            sum += sample0 * weights.x + sample1 * weights.y;
            sampleCount += dot(weights, 1);
        }
    }

    half4 returnValue = sum / sampleCount;

    return returnValue;
}

half4 fragCircleMergeHigh(v2fDepth i) : COLOR
{
    half4 blur = tex2D(_ThirdTex, i.uv.xy);
    half4 centerTap = tex2D(_MainTex, i.uv.xy);
    half4 sum = centerTap;
    const half centerDepth = tex2D (_SecondTex, i.uv).x;
    half blurriness = tex2D (_SecondTex, i.uv).y;
    half radius = 0.5h * (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y));
    half2 poissonScale = radius * _MainTex_TexelSize.xy;
    blurriness = abs(blurriness);
    half weights;

    half sampleCount = max(blurriness * 0.25h, 0.025h);
    sum *= sampleCount;


    for (int l = 0; l < DISC_SAMPLE_NUM1; l++)
    {
        half2 sampleUV = i.uv.xy + DiscKernel1[l].xy * poissonScale.xy;
        half4 sample = tex2D(_MainTex, sampleUV);
        half sampleBlur = abs(tex2D (_SecondTex, sampleUV).y);

        if (sampleBlur != 0.0 )
        {
            weights = BokehWeightDisc(sampleBlur, DiscKernel1[l].z, blurriness);
            sum += sample * weights;
            sampleCount += weights;
        }
    }

    half4 returnValue = sum / sampleCount;
    returnValue = lerp(returnValue, blur, smoothstep(0.0, 0.85, blurriness));
    return (blurriness < 1e-2f ? centerTap:returnValue);
}

///////////////////////////////////////////////////////////////////////////////
// Prefilter blur
///////////////////////////////////////////////////////////////////////////////

#define DISC_PREFILTER_SAMPLE   9
static const half2 DiscPrefilter[DISC_PREFILTER_SAMPLE] =
{
    half2(0.01288369f, 0.5416069f),
    half2(-0.9192798f, -0.09529364f),
    half2(0.7596578f, 0.1922738f),
    half2(-0.14132f, -0.2880242f),
    half2(-0.5249333f, 0.7777638f),
    half2(-0.5871695f, -0.7403569f),
    half2(0.3202196f, -0.6442268f),
    half2(0.8553214f, -0.3920982f),
    half2(0.5827708f, 0.7599297f)
};

float4 fragCircleSignedCocPrefilter (v2fDepth i) : SV_Target
{
    half4 centerTap = tex2Dlod(_MainTex, half4(i.uv.xy,0,0));
    half  radius = 0.1h * (centerTap.a < 0.0f ? -(centerTap.a * _BlurCoe.x):(centerTap.a * _BlurCoe.y));
    half2 poissonScale = radius * _MainTex_TexelSize.xy;

    if (radius < 0.01h )
    {
        return centerTap;
    }

    half  sampleCount = 1;
    half3 sum = centerTap.rgb * 1;
    for (int l = 0; l < DISC_PREFILTER_SAMPLE; l++)
    {
        half2 sampleUV = i.uv + DiscPrefilter[l].xy * poissonScale.xy;
        half4 sample0 = tex2Dlod(_MainTex, half4(sampleUV.xy,0,0));

        half weight = max(sample0.a * centerTap.a,0.0h);
        sum += sample0.rgb * weight;
        sampleCount += weight;
    }

    half4 returnValue = half4(sum / sampleCount, centerTap.a);
    return returnValue;
}

///////////////////////////////////////////////////////////////////////////////
// Final merge and upsample
///////////////////////////////////////////////////////////////////////////////

float4 upSampleConvolved(half2 uv, bool useBicubic)
{
    if (useBicubic)
    {
        //bicubic upsampling (B-spline)
        half2 convolvedTexelPos    = uv * _Convolved_TexelSize.xy;
        half2 convolvedTexelCenter = floor( convolvedTexelPos - 0.5h ) + 0.5h;
        half2 f  = convolvedTexelPos - convolvedTexelCenter;
        half2 f2 = f * f;
        half2 f3 = f * f2;

        half2 w0 = -0.166h * f3 + 0.5h * f2 - 0.5h * f + 0.166h;
        half2 w1 =  0.5h   * f3 - f2 + 0.666h;
        half2 w3 =  0.166h * f3;
        half2 w2 =  1.0h - w0 - w1 - w3;

        half2 s0 = w0 + w1;
        half2 s1 = w2 + w3;
        half2 f0 = w1 / s0;
        half2 f1 = w3 / s1;

        half2 t0 = _Convolved_TexelSize.zw * (convolvedTexelCenter - 1.0h + f0);
        half2 t1 = _Convolved_TexelSize.zw * (convolvedTexelCenter + 1.0h + f1);

        return tex2Dlod(_SecondTex, half4(t0.x, t0.y, 0, 0)) * s0.x * s0.y +
               tex2Dlod(_SecondTex, half4(t1.x, t0.y, 0, 0)) * s1.x * s0.y +
               tex2Dlod(_SecondTex, half4(t0.x, t1.y, 0, 0)) * s0.x * s1.y +
               tex2Dlod(_SecondTex, half4(t1.x, t1.y, 0, 0)) * s1.x * s1.y;
    }
    else
    {
        return tex2Dlod(_SecondTex, half4(uv,0,0));
    }
}

float4 circleSignedCocMerge (half2 uv, bool useExplicit, bool useBicubic)
{
    half4 convolvedTap = upSampleConvolved(uv, useBicubic);
    half4 sourceTap    = tex2Dlod(_MainTex, half4(uv,0,0));
    half  coc          = GetSignedCocFromDepth(uv, useExplicit);

    sourceTap.rgb += getBoostAmount(half4(sourceTap.rgb, coc));

    coc = (coc * _BlurCoe.y > 1.0h )?coc:0.0h;
    half  blendValue = smoothstep(0.0, 0.33h, max(coc, convolvedTap.a));
    half3 returnValue = lerp(sourceTap.rgb, convolvedTap.rgb, blendValue);
    return (blendValue < 1e-2f) ? sourceTap : half4(returnValue.rgb, sourceTap.a);
}

float4 fragCircleSignedCocMergeBicubic (v2fDepth i) : SV_Target
{
    return circleSignedCocMerge(i.uv, false, true);
}

float4 fragCircleSignedCocMergeExplicitBicubic (v2fDepth i) : SV_Target
{
    return circleSignedCocMerge(i.uv, true, true);
}

float4 fragCircleSignedCocMerge (v2fDepth i) : SV_Target
{
    return circleSignedCocMerge(i.uv, false, false);
}

float4 fragCircleSignedCocMergeExplicit (v2fDepth i) : SV_Target
{
    return circleSignedCocMerge(i.uv, true, false);
}

///////////////////////////////////////////////////////////////////////////////
// Downsampling and COC computation
///////////////////////////////////////////////////////////////////////////////

half4 captureSignedCoc(half2 uvColor, half2 uvDepth, bool useExplicit)
{
    half4 color = tex2Dlod (_MainTex, half4(uvColor, 0, 0 ));

    //TODO should use gather4 on supported platform!
    //TODO do only 1 tap on high resolution mode
    half cocA = GetSignedCocFromDepth(uvDepth + _MainTex_TexelSize.xy * half2(+0.25f,+0.25f), useExplicit);
    half cocB = GetSignedCocFromDepth(uvDepth + _MainTex_TexelSize.xy * half2(+0.25f,-0.25f), useExplicit);
    half cocC = GetSignedCocFromDepth(uvDepth + _MainTex_TexelSize.xy * half2(-0.25f,+0.25f), useExplicit);
    half cocD = GetSignedCocFromDepth(uvDepth + _MainTex_TexelSize.xy * half2(-0.25f,-0.25f), useExplicit);

    half cocAB = (abs(cocA)<abs(cocB))?cocA:cocB;
    half cocCD = (abs(cocC)<abs(cocD))?cocC:cocD;
    color.a    = (abs(cocAB)<abs(cocCD))?cocAB:cocCD;

    color.rgb += getBoostAmount(color);
    return color;
}

half4 fragCaptureSignedCoc (v2f i) : SV_Target
{
    return captureSignedCoc(i.uv, i.uv1, false);
}

half4 fragCaptureSignedCocExplicit (v2f i) : SV_Target
{
    return captureSignedCoc(i.uv, i.uv1, true);
}

//TODO remove the following
half4 fragCaptureCoc (v2fDepth i) : SV_Target
{
    half4 color = tex2D (_MainTex, half2(i.uv.x, 1.0h - i.uv.y));
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half f = _BlurParams.x * abs(d - _BlurParams.z) / (d + 1e-5f) - _BlurParams.w;
    f = f * (d < _BlurParams.z ? _BlurCoe.z:_BlurCoe.w);
    f = clamp(f, 0.0f, 1.0f);
    color.a = f;
    return color;
}

half4 fragBlurinessAmount(v2fDepth i) : COLOR
{
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half f = _BlurParams.x * abs(d - _BlurParams.z) / (d + 1e-5f) - _BlurParams.w;
    f = (d < _BlurParams.z ? -1.0h:1.0h) * clamp(f, 0.0f, 1.0f);
    return half4(d, f, 0, 0);
}

half4 fragBlurinessAmountExplicit(v2fDepth i) : COLOR
{
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half f = d < _BlurCoe.z ? clamp((_BlurParams.x * d + _BlurParams.y), -1.0f, 0.0f):clamp((_BlurParams.z * d + _BlurParams.w), 0.0f, 1.0f);
    return half4(d, f, 0, 0);
}

///////////////////////////////////////////////////////////////////////////////
// Coc visualisation
///////////////////////////////////////////////////////////////////////////////

inline float4 visualizeCoc(half2 uv, bool useExplicit)
{
    half coc = GetSignedCocFromDepth(uv, useExplicit);
    return (coc < 0)? half4(-coc, -coc, 0, 1.0) : half4(0, coc, coc, 1.0);
}

float4 fragVisualizeCoc(v2fDepth i) : SV_Target
{
    return visualizeCoc(i.uv, false);
}

float4 fragVisualizeCocExplicit(v2fDepth i) : SV_Target
{
    return visualizeCoc(i.uv, true);
}

//TODO remove the following

half4 fragBlurinessAmountVisualisation0(v2fDepth i) : COLOR
{
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half f = _BlurParams.x * abs(d - _BlurParams.z) / (d + 1e-5f) - _BlurParams.w;
    f = (d < _BlurParams.z ? -1.0h:1.0h) * clamp(f, 0.0f, 1.0f);
    return (f < 0)? half4(-f, -f, 0, 1.0) : half4(0, f, f, 1.0);
}

half4 fragBlurinessAmountVisualisation2(v2fDepth i) : COLOR
{
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half f = d < _BlurCoe.z ? clamp((_BlurParams.x * d + _BlurParams.y), -1.0f, 0.0f):clamp((_BlurParams.z * d + _BlurParams.w), 0.0f, 1.0f);
    return (f < 0)? half4(-f, -f, 0, 1.0) : half4(0, f, f, 1.0);
}

//TODO remove me
half4 fragCaptureCocExplicit (v2fDepth i) : SV_Target
{
    half4 color = tex2D (_MainTex, half2(i.uv.x, 1.0h - i.uv.y));
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half f = d < _BlurCoe.z ? (_BlurParams.x * d + _BlurParams.y):(_BlurParams.z * d + _BlurParams.w);
    f = clamp(f, 0.0f, 1.0f);
    color.a = f;
    return color;
}

half fragOverBlurinessAmount0(v2fDepth i) : COLOR
{
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half f = _BlurParams.x * abs(d - _BlurParams.z) / (d + 1e-5f) - _BlurParams.w;
    f = f * (d < _BlurParams.z ? _BlurCoe.z:0.0h);
    f = clamp(f, 0.0f, 1.0f);
    return f;
}

half4 fragOverMergeBlurinessAmount0(v2fDepth i) : COLOR
{
    half f = tex2D(_MainTex, i.uv);
    half d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
    d = Linear01Depth (d);
    half b = _BlurParams.x * abs(d - _BlurParams.z) / (d + 1e-5f) - _BlurParams.w;
    b = b * (d < _BlurParams.z ? 0.0h:_BlurCoe.w);
    b = clamp(b, 0.0f, 1.0f);
    f = max(f, b);
    return half4(f, f, f, 1.0);
}

half4 fragCopy(v2fDepth i) : COLOR
{
    half4 color = tex2D (_MainTex, half2(i.uv.x, 1.0h - i.uv.y));
    return color;
}

half4 fragBoostThresh(v2fDepth i) : SV_Target
{
    half4 color = tex2D(_MainTex, i.uv);
    half blurriness = tex2D(_SecondTex, i.uv).y;
    half blur = (blurriness < 0.0f ? -(blurriness * _BlurCoe.x):(blurriness * _BlurCoe.y));
    half luma = dot(color, half4(0.3h, 0.59h, 0.11h, 0.0h));
    return luma < _Param1 ? half4(0.0h, 0.0h, 0.0h, 0.0h):color * blur;
}

half4 fragBoost(v2fDepth i) : SV_Target
{
    half4 color = tex2D(_MainTex, i.uv);
    half blurriness = tex2D(_SecondTex, i.uv).y;
    half4 blur = blurriness < 0.0f ? tex2D(_ThirdTex, i.uv) * _Param0.x:tex2D(_ThirdTex, i.uv) * _Param0.y;
    return color + blur;
}

float4 fragBlurAlphaWeighted (v2fBlur i) : SV_Target
{
    const float ALPHA_WEIGHT = 2.0f;
    float4 sum = float4 (0,0,0,0);
    float w = 0;
    float weights = 0;
    const float G_WEIGHTS[6] = {1.0, 0.8, 0.675, 0.5, 0.2, 0.075};

    float4 sampleA = tex2D(_MainTex, i.uv.xy);

    float4 sampleB = tex2D(_MainTex, i.uv01.xy);
    float4 sampleC = tex2D(_MainTex, i.uv01.zw);
    float4 sampleD = tex2D(_MainTex, i.uv23.xy);
    float4 sampleE = tex2D(_MainTex, i.uv23.zw);
    float4 sampleF = tex2D(_MainTex, i.uv45.xy);
    float4 sampleG = tex2D(_MainTex, i.uv45.zw);
    float4 sampleH = tex2D(_MainTex, i.uv67.xy);
    float4 sampleI = tex2D(_MainTex, i.uv67.zw);
    float4 sampleJ = tex2D(_MainTex, i.uv89.xy);
    float4 sampleK = tex2D(_MainTex, i.uv89.zw);

    w = sampleA.a * G_WEIGHTS[0]; sum += sampleA * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleB.a) * G_WEIGHTS[1]; sum += sampleB * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleC.a) * G_WEIGHTS[1]; sum += sampleC * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleD.a) * G_WEIGHTS[2]; sum += sampleD * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleE.a) * G_WEIGHTS[2]; sum += sampleE * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleF.a) * G_WEIGHTS[3]; sum += sampleF * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleG.a) * G_WEIGHTS[3]; sum += sampleG * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleH.a) * G_WEIGHTS[4]; sum += sampleH * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleI.a) * G_WEIGHTS[4]; sum += sampleI * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleJ.a) * G_WEIGHTS[5]; sum += sampleJ * w; weights += w;
    w = saturate(ALPHA_WEIGHT*sampleK.a) * G_WEIGHTS[5]; sum += sampleK * w; weights += w;

    sum /= weights + 1e-4f;

    sum.a = sampleA.a;
    if(sampleA.a<1e-2f) sum.rgb = sampleA.rgb;

    return sum;
}

float4 fragAlphaMask(v2f i) : SV_Target
{
    float4 c = tex2D(_MainTex, i.uv1.xy);
    c.a = saturate(c.a*100.0);
    return c;
}

float4 fragBlurBox (v2f i) : SV_Target
{
    const int TAPS = 12;

    float4 centerTap = tex2D(_MainTex, i.uv1.xy);


    float sampleCount =  centerTap.a;
    float4 sum = centerTap * sampleCount;

    float2 lenStep = centerTap.aa * (1.0 / (TAPS-1.0));
    float4 steps = (_Offsets.xyxy * _MainTex_TexelSize.xyxy) * lenStep.xyxy * float4(1,1, -1,-1);

    for(int l=1; l<TAPS; l++)
    {
        float4 sampleUV = i.uv1.xyxy + steps * (float)l;

        float4 sample0 = tex2D(_MainTex, sampleUV.xy);
        float4 sample1 = tex2D(_MainTex, sampleUV.zw);

        float2 maxLen01 = float2(sample0.a, sample1.a);
        float2 r = lenStep.xx * (float)l;

        float2 weight01 = smoothstep(float2(-0.4,-0.4),float2(0.0,0.0), maxLen01-r);
        sum += sample0 * weight01.x + sample1 * weight01.y;

        sampleCount += dot(weight01,1);
    }

    float4 returnValue = sum / (1e-5f + sampleCount);

    return returnValue;
}

float4 fragGaussBlurCoc (v2f i) : SV_Target
{
    //gaussian around a box to avoid the blocky look but keep a wide kernel
    const float G_WEIGHTS[13] = {1.0, 1.0, 1.0, 1.0, 0.9, 0.8, 0.65, 0.5, 0.4, 0.2, 0.1, 0.05, 0.025 };
    const int TAPS = 13;
    float4 centerTap = tex2D(_MainTex, i.uv1.xy);

    float sampleCount =  centerTap.a * G_WEIGHTS[0];
    float4 sum = centerTap * sampleCount;

    float2 lenStep = centerTap.aa * (1.0 / (TAPS-1.0));
    float4 steps = (_Offsets.xyxy * _MainTex_TexelSize.xyxy) * lenStep.xyxy * float4(1,1, -1,-1);

    for(int l=1; l<TAPS; l++)
    {
        float4 sampleUV = i.uv1.xyxy + steps * (float)l;

        float4 sample0 = tex2D(_MainTex, sampleUV.xy);
        float4 sample1 = tex2D(_MainTex, sampleUV.zw);

        float2 maxLen01 = float2(sample0.a, sample1.a);
        float2 r = lenStep.xx * (float)l;

        float2 weight01 = smoothstep(float2(-0.4,-0.4),float2(0.0,0.0), maxLen01-r);
        weight01 *= G_WEIGHTS[l];
        sum += sample0 * weight01.x + sample1 * weight01.y;

        sampleCount += dot(weight01,1);
    }

    float4 returnValue = sum / (1e-5f + sampleCount);
    return returnValue;
}

float4 fragBoxDownsample (v2f i) : SV_Target
{
    float4 returnValue = tex2D(_MainTex, i.uv1.xy + 0.75*_MainTex_TexelSize.xy);
    returnValue += tex2D(_MainTex, i.uv1.xy - 0.75*_MainTex_TexelSize.xy);
    returnValue += tex2D(_MainTex, i.uv1.xy + 0.75*_MainTex_TexelSize.xy * float2(1,-1));
    returnValue += tex2D(_MainTex, i.uv1.xy - 0.75*_MainTex_TexelSize.xy * float2(1,-1));

    return returnValue/4;
}

///////////////////////////////////////////////////////////////////////////////
// Foreground blur dilatation
///////////////////////////////////////////////////////////////////////////////

inline half fgCocSourceChannel(half2 uv, bool fromAlpha)
{
    if (fromAlpha)
        return tex2Dlod(_MainTex, half4(uv,0,0)).a;
    else
        return tex2Dlod(_MainTex, half4(uv,0,0)).r;
}

inline half weigthedFGCocBlur(v2fBlur i, bool fromAlpha)
{
    half  fgCocA = fgCocSourceChannel(i.uv.xy, fromAlpha);
    half  fgCocC = fgCocSourceChannel(i.uv01.zw, fromAlpha);
    half  fgCocB = fgCocSourceChannel(i.uv01.xy, fromAlpha);
    half  fgCocD = fgCocSourceChannel(i.uv23.xy, fromAlpha);
    half  fgCocE = fgCocSourceChannel(i.uv23.zw, fromAlpha);
    half  fgCocF = fgCocSourceChannel(i.uv45.xy, fromAlpha);
    half  fgCocG = fgCocSourceChannel(i.uv45.zw, fromAlpha);
    half  fgCocH = fgCocSourceChannel(i.uv67.xy, fromAlpha);
    half  fgCocI = fgCocSourceChannel(i.uv67.zw, fromAlpha);
    half  fgCocJ = fgCocSourceChannel(i.uv89.xy, fromAlpha);
    half  fgCocK = fgCocSourceChannel(i.uv89.zw, fromAlpha);

    half fgCoc = 0;
    fgCoc = min(0, fgCocA);
    fgCoc = min(fgCoc, fgCocB);
    fgCoc = min(fgCoc, fgCocC);
    fgCoc = min(fgCoc, fgCocD);
    fgCoc = min(fgCoc, fgCocE);
    fgCoc = min(fgCoc, fgCocF);
    fgCoc = min(fgCoc, fgCocG);
    fgCoc = min(fgCoc, fgCocH);
    fgCoc = min(fgCoc, fgCocI);
    fgCoc = min(fgCoc, fgCocJ);
    fgCoc = min(fgCoc, fgCocK);

    return fgCoc;
}

float4 fragBlurFgCocFromColor (v2fBlur i) : SV_Target
{
    half fgCoc = weigthedFGCocBlur(i,true);
    return fgCoc.rrrr;
}

float4 fragBlurFgCoc (v2fBlur i) : SV_Target
{
    half fgCoc = weigthedFGCocBlur(i,false);
    return fgCoc.rrrr;
}

ENDCG

///////////////////////////////////////////////////////////////////////////////

SubShader
{

    ZTest Always Cull Off ZWrite Off Fog { Mode Off } Lighting Off Blend Off

    // 1
    Pass
    {
        CGPROGRAM
        #pragma vertex vertBlurPlusMinus
        #pragma fragment fragBlurAlphaWeighted
        ENDCG
    }

    // 2
    Pass
    {
        CGPROGRAM
        #pragma vertex vert_d
        #pragma fragment fragBoxDownsample
        ENDCG
    }

    // 3
    Pass
    {
      CGPROGRAM
      #pragma vertex vertBlurPlusMinus
      #pragma fragment fragBlurFgCocFromColor
      ENDCG
    }

    // 4
    Pass
    {
      CGPROGRAM
      #pragma vertex vertBlurPlusMinus
      #pragma fragment fragBlurFgCoc
      ENDCG
    }

    // 5
    Pass
    {
        CGPROGRAM
        #pragma vertex vert_d
        #pragma fragment fragCaptureSignedCoc
        ENDCG
    }

    // 6
    Pass
    {
        CGPROGRAM
        #pragma vertex vert_d
        #pragma fragment fragCaptureSignedCocExplicit
        ENDCG
    }

    // 7
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragVisualizeCoc
        ENDCG
    }

    // 8
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragVisualizeCocExplicit
        ENDCG
    }

    // 9
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleSignedCocPrefilter
        ENDCG
    }

    // 10
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleSignedCocBokeh
        ENDCG
    }

    // 11
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleSignedCocBokehWithDilatedFG
        ENDCG
    }

    // 12
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleSignedCocBokehLow
        ENDCG
    }

    // 13
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleSignedCocBokehWithDilatedFGLow
        ENDCG
    }

    // 14
    Pass
    {
        CGPROGRAM
        #pragma vertex vertNoFlip
        #pragma fragment fragCircleSignedCocMerge
        ENDCG
    }

    // 15
    Pass
    {
        CGPROGRAM
        #pragma vertex vertNoFlip
        #pragma fragment fragCircleSignedCocMergeExplicit
        ENDCG
    }

    // 16
    Pass
    {
        CGPROGRAM
        #pragma vertex vertNoFlip
        #pragma fragment fragCircleSignedCocMergeBicubic
        ENDCG
    }

    // 17
    Pass
    {
        CGPROGRAM
        #pragma vertex vertNoFlip
        #pragma fragment fragCircleSignedCocMergeExplicitBicubic
        ENDCG
    }

    // 18
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeLowQuality
        ENDCG
    }

    // 19
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeLowQualityDilateFG
        ENDCG
    }

    // 20
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeLowQualityMerge
        ENDCG
    }

    // 21
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeLowQualityMergeDilateFG
        ENDCG
    }

    // 22
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeMediumQuality
        ENDCG
    }

    // 23
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeMediumQualityDilateFG
        ENDCG
    }

    // 24
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeMediumQualityMerge
        ENDCG
    }

    // 25
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeMediumQualityMergeDilateFG
        ENDCG
    }

    // 26
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeHighQuality
        ENDCG
    }

    // 27
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeHighQualityDilateFG
        ENDCG
    }

    // 28
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeHighQualityMerge
        ENDCG
    }

    // 29
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShapeHighQualityMergeDilateFG
        ENDCG
    }

    ///////////////////////////////////////////////////////////
    // TODO remove these
    ///////////////////////////////////////////////////////////

    // 30
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragBlurinessAmountVisualisation0
        ENDCG
    }

    // 31
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragBlurinessAmount
        ENDCG
    }

    // 32
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragBlurinessAmountVisualisation2
        ENDCG
    }

    // 33
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragBlurinessAmountExplicit
        ENDCG
    }

    // 34
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCaptureCoc
        ENDCG
    }

    // 35
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCaptureCocExplicit
        ENDCG
    }

    // 36
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCopy
        ENDCG
    }

    // 37
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShape0
        ENDCG
    }

    // 38
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShape1
        ENDCG
    }

    // 39
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShape2
        ENDCG
    }

    // 40
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShape3
        ENDCG
    }

    // 41
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShape4
        ENDCG
    }

    // 42
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragShape5
        ENDCG
    }

    // 43
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleBokehLow
        ENDCG
    }

    // 44
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleMergeLow
        ENDCG
    }

    // 45
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleBokehHigh
        ENDCG
    }

    // 46
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCircleMergeHigh
        ENDCG
    }

    // 47
    Pass
    {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragBoostThresh
        ENDCG
    }

    // 48
    Pass
    {
        CGPROGRAM
        #pragma vertex vertNoFlip
        #pragma fragment fragBoost
        ENDCG
    }

    // 49
    Pass
    {
        CGPROGRAM
        #pragma vertex vert_d
        #pragma fragment fragAlphaMask
        ENDCG
    }

    // 50
    Pass
    {
        BlendOp Add, Add
        Blend DstAlpha OneMinusDstAlpha, Zero One

        CGPROGRAM
        #pragma vertex vertFlip
        #pragma fragment fragBlurBox
        ENDCG
    }

    // 51
    Pass
    {
        CGPROGRAM
        #pragma vertex vert_d
        #pragma fragment fragBlurBox
        ENDCG
    }

}

FallBack Off
}
