Shader "Hidden/Image Effects/StylisticFog"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	CGINCLUDE
	#pragma shader_feature OMMIT_SKYBOX
	#pragma shader_feature USE_HEIGHT
	#pragma shader_feature SELECT_COLOR_BY_FOG_AMOUNT
	#include "UnityCG.cginc"

	half4 _MainTex_TexelSize;

	sampler2D _MainTex;
	sampler2D _CameraDepthTexture;
	sampler2D _FogPropertyTexture;

	// Row 0: Fog density function
	// Row 1: Numerically integrated density function
	sampler2D _FogHeightDensityTexture;

	float4x4 _InverseViewMatrix;

	uniform float _FogStartDist;
	uniform float _FogEndDist;

	uniform float _Height;
	uniform float _BaseDensity;
	uniform float _DensityFalloff;


	// from https://github.com/keijiro/DepthToWorldPos
	inline float4 DepthToWorld(float depth, float2 uv, float4x4 inverseViewMatrix)
	{
		float viewDepth = LinearEyeDepth(depth);
		float2 p11_22 = float2(unity_CameraProjection._11, unity_CameraProjection._22);
		float3 vpos = float3((uv * 2 - 1) / p11_22, -1) * viewDepth;
		float4 wpos = mul(inverseViewMatrix, float4(vpos, 1));
		return wpos;
	}

	inline float ComputeFogAmount(float distance)
	{
		float f = (distance - _FogStartDist) / (_FogEndDist - _FogStartDist);
		return saturate(f);
	}

	half4 fragment(v2f_img i) : SV_Target
	{
		half4 sceneColor = tex2D(_MainTex, i.uv);
		float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);

#if defined(OMMIT_SKYBOX)
		if (depth < 0.00001)
		{
			return sceneColor;
		}
#endif // defined(OMMIT_SKYBOX)
		
		float4 wpos = DepthToWorld(depth, i.uv, _InverseViewMatrix);

		float4 fragmentToCamera = wpos - float4(_WorldSpaceCameraPos,1.);
		float3 viewDir = normalize(fragmentToCamera);
		float totalDistance = length(fragmentToCamera);

		float effectiveDistance = max(totalDistance - _FogStartDist, 0.0);

		float fogFactor = 0.;
		float fogAmount = 0.;

#if defined(USE_HEIGHT)

		// Density function = d(y) = GlobalDensity * exp(-DensityFalloff * y)
		float relativeHeight = _WorldSpaceCameraPos.y - _Height;

		float falloffFactor = min(127., _DensityFalloff * relativeHeight);
		float falloffAngle = _DensityFalloff * viewDir.y;

		float heightIntegrale = _BaseDensity * exp2(-falloffFactor);
		heightIntegrale *= (1 - exp2(-falloffAngle * effectiveDistance)) / (falloffAngle);

		fogAmount = 1.0 - saturate(exp2(-heightIntegrale));

#else // defined(USE_HEIGHT)

		fogAmount = ComputeFogAmount(effectiveDistance);

#endif // defined(USE_HEIGHT)

		fogFactor = DecodeFloatRGBA(tex2D(_FogHeightDensityTexture, float2(fogAmount, 0.)));

		float4 fogCol;
		fogCol = tex2D(_FogPropertyTexture, float2(fogFactor, 0));
		return lerp(sceneColor, half4(fogCol.xyz, 1.), fogCol.a);
	}

	ENDCG
	SubShader
	{
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			CGPROGRAM
			#pragma vertex vert_img
			#pragma fragment fragment
			ENDCG
		}
	}
}
