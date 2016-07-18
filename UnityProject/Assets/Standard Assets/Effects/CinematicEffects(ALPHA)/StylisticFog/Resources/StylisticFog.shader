Shader "Hidden/Image Effects/StylisticFog"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	CGINCLUDE
	// If enabled: Distance fog is applied
	#pragma shader_feature USE_DISTANCE

	// If enabled: Height fog is applied
	#pragma shader_feature USE_HEIGHT

	// If enabled: Distance and heght fog contributes with the same color
	#pragma shader_feature SHARED_COLOR_SETTINGS

	// SHARED_COLOR_PICKER:  The shared color is a single 4-component color
	// SHARED_COLOR_TEXTURE: The shared color is a sample from a (1D) texture
	#pragma multi_compile SHARED_COLOR_PICKER SHARED_COLOR_TEXTURE

	// DIST_COLOR_PICKER:  The distance color is a single 4-component color
	// DIST_COLOR_TEXTURE: The distance color is a sample from a (1D) texture
	#pragma multi_compile DIST_COLOR_PICKER DIST_COLOR_TEXTURE

	// HEIGHT_COLOR_PICKER:  The height color is a single 4-component color
	// HEIGHT_COLOR_TEXTURE: The height color is a sample from a (1D) texture
	#pragma multi_compile HEIGHT_COLOR_PICKER HEIGHT_COLOR_TEXTURE

	#include "UnityCG.cginc"

	#define SKYBOX_THREASHOLD_VALUE 0.9999
	#define FOG_AMOUNT_CONTRIBUTION_THREASHOLD 0.0001

	bool _ApplyDistToSkybox;
	bool _ApplyHeightToSkybox;

	bool _UseDistanceFog;
	bool _UseHeightFog;

	bool _SharedColorSettings;

	bool _ColorSourceOneIsTexture;
	bool _ColorSourceTwoIsTexture;

	half4 _MainTex_TexelSize;

	sampler2D _MainTex;
	sampler2D _CameraDepthTexture;

	sampler2D _FogFactorIntensityTexture;
	sampler2D _FogColorTexture0;
	sampler2D _FogColorTexture1;

	half4 _FogPickerColor0;
	half4 _FogPickerColor1;

	float4x4 _InverseViewMatrix;

	uniform float _FogStartDist;
	uniform float _FogEndDist;

	uniform float _Height;
	uniform float _BaseDensity;
	uniform float _DensityFalloff;

	struct v2f_multitex
	{
		float4 pos : SV_POSITION;
		float2 uv0 : TEXCOORD0;
		float2 uv1 : TEXCOORD1;
	};

	v2f_multitex vert_img_fog(appdata_img v)
	{
		// Handles vertically-flipped case.
		float vflip = sign(_MainTex_TexelSize.y);

		v2f_multitex o;
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv0 = v.texcoord.xy;
		o.uv1 = (v.texcoord.xy - 0.5) * float2(1, vflip) + 0.5;
		return o;
	}

	// from https://github.com/keijiro/DepthToWorldPos
	inline float4 DepthToWorld(float depth, float2 uv, float4x4 inverseViewMatrix)
	{
		float viewDepth = LinearEyeDepth(depth);
		float2 p11_22 = float2(unity_CameraProjection._11, unity_CameraProjection._22);
		float3 vpos = float3((uv * 2 - 1) / p11_22, -1) * viewDepth;
		float4 wpos = mul(inverseViewMatrix, float4(vpos, 1));
		return wpos;
	}

	// Compute how intense the distance fog is according to the distance
	// and the fog intensity curve.
	inline float ComputeDistanceFogAmount(float distance)
	{
		float f = (distance - _FogStartDist) / (_FogEndDist - _FogStartDist);
		f =  DecodeFloatRGBA(tex2D(_FogFactorIntensityTexture, float2(f, 0.)));
		return saturate(f);
	}

	// Computes the amount of fog treversed based on a desnity function d(h)
	// where d(h) = _BaseDensity * exp2(-DensityFalloff * h) <=> d(h) = a * exp2(b * h)
	inline float ComputeHeightFogAmount(float viewDirY, float effectiveDistance)
	{
		float relativeHeight = min(127., _WorldSpaceCameraPos.y - _Height);
		return _BaseDensity * exp2(-relativeHeight * _DensityFalloff) * (1. - exp2(-effectiveDistance * viewDirY * _DensityFalloff)) / viewDirY;
	}

	inline half4 GetColorFromPicker(half4 pickerColor, float fogAmount)
	{
		half4 fogCol = pickerColor;
		fogCol.a = saturate(fogAmount * pickerColor.a);
		return fogCol;
	}

	inline half4 GetColorFromTexture(sampler2D source, float fogAmount)
	{
		return tex2D(source, float2(fogAmount, 0));
	}

	half4 fragment(v2f_img i) : SV_Target
	{
		half4 sceneColor = tex2D(_MainTex, i.uv);
		float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
		
		float4 wpos = DepthToWorld(depth, i.uv, _InverseViewMatrix);

		float4 cameraToFragment = wpos - float4(_WorldSpaceCameraPos, 1.);
		float3 viewDir = normalize(cameraToFragment);
		float totalDistance = length(cameraToFragment);

		float effectiveDistance = max(totalDistance - _FogStartDist, 0.0);

		float fogFactor = 0.;
		float fogAmount = 0.;
		float distanceFogAmount = 0.;
		float heightFogAmount = 0.;

		float linDepth = Linear01Depth(depth);

		if (_UseDistanceFog && (_ApplyDistToSkybox || linDepth < SKYBOX_THREASHOLD_VALUE))
			distanceFogAmount = ComputeDistanceFogAmount(effectiveDistance);

		if (_UseHeightFog && (_ApplyHeightToSkybox || linDepth < SKYBOX_THREASHOLD_VALUE))
			heightFogAmount = ComputeHeightFogAmount(viewDir.y, totalDistance);

		half4 finalFogColor = half4(0., 0., 0., 0.);
		half4 fogCol = 0.;

		// If shared settings are applied, add the two fog contributions
		// and pick the color from the shared color source.
		if (_SharedColorSettings)
		{
			fogAmount = heightFogAmount + distanceFogAmount;
			if (_ColorSourceOneIsTexture)
			{
				fogCol = GetColorFromTexture(_FogColorTexture0, fogAmount);
			}
			else
			{
				return 1.;
				fogCol = GetColorFromPicker(_FogPickerColor0, fogAmount);
			}
			finalFogColor = lerp(sceneColor, half4(fogCol.xyz, 1.), fogCol.a * step(FOG_AMOUNT_CONTRIBUTION_THREASHOLD, max(distanceFogAmount, heightFogAmount)));
		}
		// seperate color settings
		// Pick each of the colors and combine
		else 
		{
			half4 distanceColor = 0.;
			half4 heightColor = 0.;

			if (_UseDistanceFog)
			{
				if (_ColorSourceOneIsTexture)
					distanceColor = GetColorFromTexture(_FogColorTexture0, fogAmount);
				else
					distanceColor = GetColorFromPicker(_FogPickerColor0, fogAmount);
			}

			if (_UseHeightFog)
			{
				if (_ColorSourceOneIsTexture)
					heightColor = GetColorFromTexture(_FogColorTexture1, fogAmount);
				else
					heightColor = GetColorFromPicker(_FogPickerColor1, fogAmount);
			}
			finalFogColor = lerp(sceneColor, half4(distanceColor.xyz, 1.), distanceColor.a * step(FOG_AMOUNT_CONTRIBUTION_THREASHOLD, distanceFogAmount));
			finalFogColor = lerp(finalFogColor, half4(heightColor.xyz, 1.), heightColor.a * step(FOG_AMOUNT_CONTRIBUTION_THREASHOLD, heightFogAmount));
		}


		finalFogColor.a = 1.;
		return finalFogColor;

	}

	ENDCG
	SubShader
	{
		// 0: Height and distance fog with shared color
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			CGPROGRAM
			#pragma vertex vert_img_fog
			#pragma fragment fragment
			ENDCG
		}
	}
}
