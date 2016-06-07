Shader "Hidden/Temporal Anti-aliasing"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	CGINCLUDE
		#pragma exclude_renderers gles
		#include "UnityCG.cginc"

		#define TAA_REMOVE_JITTER 1

		#define TAA_USE_YCOCG 1

		#define TAA_DEPTH_NEIGHBORHOOD_SAMPLE_PATTERN 0
		#define TAA_COLOR_NEIGHBORHOOD_SAMPLE_PATTERN 0
		#define TAA_HISTORY_NEIGHBORHOOD_SAMPLE_PATTERN 0

		#define TAA_DEPTH_NEIGHBORHOOD_SAMPLE_SPREAD 1.
		#define TAA_COLOR_NEIGHBORHOOD_SAMPLE_SPREAD 1.

		#define TAA_ADJUST_CHROMA_EXTENTS 1

		#define TAA_USE_NEIGHBORHOOD_CLIPPING 0

		struct Input
		{
			float4 vertex : POSITION;
			float2 uv : TEXCOORD0;
		};

		struct Varyings
		{
			float4 vertex : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		Varyings vertex(Input input)
		{
			Varyings output;

			output.vertex = mul(UNITY_MATRIX_MVP, input.vertex);
			output.uv = input.uv;

			return output;
		}

		sampler2D _MainTex;
		sampler2D _HistoryTex;
		sampler2D _CameraDepthTexture;
		sampler2D _CameraMotionVectorsTexture;

		float4 _MainTex_TexelSize;
		float4 _CameraDepthTexture_TexelSize;

		float4 _Fuzz;

		float2 _FeedbackBounds;

		#if TAA_USE_YCOCG == 1
			float4 convertToYCoCg(float4 rgba)
			{
				return float4(
					dot(rgba, float3(.25, .5, .25)),
					dot(rgba, float3(.5, 0., -.5)),
					dot(rgba, float3(-.25, .5, -.25)),
					rgba.a);
			}

			float4 convertToRGBA(float4 yCoCg)
			{
				return saturate(float4(
					yCoCg.x + yCoCg.y - yCoCg.z,
					yCoCg.x + yCoCg.z,
					yCoCg.x - yCoCg.y - yCoCg.z,
					yCoCg.a));
			}
		#endif

		float3 getClosestFragment(in float2 uv)
		{
			const float2 k = TAA_DEPTH_NEIGHBORHOOD_SAMPLE_SPREAD * _CameraDepthTexture_TexelSize.xy;

		#if TAA_DEPTH_NEIGHBORHOOD_SAMPLE_PATTERN == 0
			const float4 neighborhood = float4(
				tex2D(_CameraDepthTexture, uv - k).r,
				tex2D(_CameraDepthTexture, uv + float2(k.x, -k.y)).r,
				tex2D(_CameraDepthTexture, uv + float2(-k.x, k.y)).r,
				tex2D(_CameraDepthTexture, uv + k).r);

			float3 result = float3(0., 0., tex2D(_CameraDepthTexture, uv).r);

			if (neighborhood.x < result.z)
				result = float3(-1., -1., neighborhood.x);

			if (neighborhood.y < result.z)
				result = float3(1., -1., neighborhood.y);

			if (neighborhood.z < result.z)
				result = float3(-1., 1., neighborhood.z);

			if (neighborhood.w < result.z)
				result = float3(1., 1., neighborhood.w);
		#else
			const float3x3 neighborhood = float3x3(
				tex2D(_CameraDepthTexture, uv - k).r,
				tex2D(_CameraDepthTexture, uv - float2(0., k.y)).r,
				tex2D(_CameraDepthTexture, uv + float2(k.x, -k.y)).r,

				tex2D(_CameraDepthTexture, uv - float2(k.x, 0.)).r,
				tex2D(_CameraDepthTexture, uv).r,
				tex2D(_CameraDepthTexture, uv + float2(k.x, 0.)).r,

				tex2D(_CameraDepthTexture, uv + float2(-k.x, k.y)).r,
				tex2D(_CameraDepthTexture, uv + float2(0., k.y)).r,
				tex2D(_CameraDepthTexture, uv + k).r);

			float3 result = float3(-1., -1., neighborhood._m00);

			if (neighborhood._m01 < result.z)
				result = float3(0., -1., neighborhood._m01);

			if (neighborhood._m02 < result.z)
				result = float3(1., -1., neighborhood._m02);

			if (neighborhood._m10 < result.z)
				result = float3(-1., 0., neighborhood._m10);

			if (neighborhood._m11 < result.z)
				result = float3(0., 0., neighborhood._m11);

			if (neighborhood._m12 < result.z)
				result = float3(1., 0., neighborhood._m12);

			if (neighborhood._m20 < result.z)
				result = float3(-1., 1., neighborhood._m20);

			if (neighborhood._m21 < result.z)
				result = float3(0., 1., neighborhood._m21);

			if (neighborhood._m22 < result.z)
				result = float3(1., 1., neighborhood._m22);
		#endif

			return float3(uv + result.xy * k, result.z);
		}

		float4 getFilteredHistorySample(in float2 uv)
		{
		#if TAA_HISTORY_NEIGHBORHOOD_SAMPLE_PATTERN == 1
			return
    				(tex2D(_HistoryTex, uv + float2(0., 0.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(1., 0.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(1., 1.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(0., 1.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(-1., 1.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(-1., 0.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(-1., -1.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(0., -1.) * _MainTex_TexelSize.xy) +
    				tex2D(_HistoryTex, uv + float2(1., -1.) * _MainTex_TexelSize.xy)) * .111111;
		#elif TAA_HISTORY_NEIGHBORHOOD_SAMPLE_PATTERN == 2
			// Bicubic Catmull-Rom filtering
			// http://vec3.ca/bicubic-filtering-in-fewer-taps/
			uv *= _MainTex_TexelSize.zw;

			const float2 texelCenter = floor(uv - .5) + .5;
			const float2 offset = uv - texelCenter;

			const float2 k = offset * offset;
			const float2 l = k * offset;

			float2 weights[4];

			weights[0] = k - .5 * (l + offset);
			weights[1] = 1.5 * l - 2.5 * k + 1.;
    			weights[3] = .5 * (l - k);
    			weights[2] = 1. - weights[0] - weights[1] - weights[3];

    			float4 samples[3];

    			samples[0].xy = weights[0];
    			samples[0].zw = texelCenter - 1.;

    			samples[1].xy = weights[1] + weights[2];
    			samples[1].zw = texelCenter + weights[2] / samples[1].xy;

    			samples[2].xy = texelCenter + 2.;
    			samples[2].zw = weights[3];

    			samples[0].zw *= _MainTex_TexelSize.xy;
    			samples[1].zw *= _MainTex_TexelSize.xy;
    			samples[2].zw *= _MainTex_TexelSize.xy;

    			samples[0].w = 1. - samples[0].w;
    			samples[1].w = 1. - samples[1].w;
    			samples[2].w = 1. - samples[2].w;

    			return
    				tex2D(_HistoryTex, samples[0].zw) * samples[0].x * samples[0].y +
    				tex2D(_HistoryTex, float2(samples[1].z, samples[0].w)) * samples[1].x * samples[0].y +
    				tex2D(_HistoryTex, float2(samples[2].z, samples[0].w)) * samples[2].x * samples[0].y +

    				tex2D(_HistoryTex, float2(samples[0].z, samples[1].w)) * samples[0].x * samples[1].y +
				tex2D(_HistoryTex, samples[1].zw) * samples[1].x * samples[1].y +
				tex2D(_HistoryTex, float2(samples[2].z, samples[1].w)) * samples[2].x * samples[1].y +

				tex2D(_HistoryTex, float2(samples[0].z, samples[2].w)) * samples[0].x * samples[2].y +
				tex2D(_HistoryTex, float2(samples[1].z, samples[2].w)) * samples[1].x * samples[2].y +
				tex2D(_HistoryTex, samples[2].zw) * samples[2].x * samples[2].y;
		#endif
		}

		float intersectAABB(in float3 origin, in float3 direction, in float3 extents)
		{
			float3 reciprocal = rcp(direction);

			float3 minimum = (extents - origin) * reciprocal;
			float3 maximum = (-extents - origin) * reciprocal;

			return max(max(min(minimum.x, maximum.x), min(minimum.y, maximum.y)), min(minimum.z, maximum.z));
		}

		float4 reproject(in float2 uv, in float3 motion)
		{
			float2 historyUV = uv;

			uv -= _Fuzz.xy;
			float4 color = convertToYCoCg(tex2D(_MainTex, uv));

			const float2 k = TAA_COLOR_NEIGHBORHOOD_SAMPLE_SPREAD * _MainTex_TexelSize.xy;

		#if TAA_COLOR_NEIGHBORHOOD_SAMPLE_PATTERN == 0
			const float4x4 neighborhood = float4x4(
				convertToYCoCg(tex2D(_MainTex, uv + float2(0., -k.y))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(-k.x, 0.))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(k.x, 0.))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(0., k.y))));

			float4 minimum = min(min(min(min(neighborhood[0], neighborhood[1]), neighborhood[2]), neighborhood[3]), color);
			float4 maximum = max(max(max(max(neighborhood[0], neighborhood[1]), neighborhood[2]), neighborhood[3]), color);

			float4 average = (neighborhood[0] + neighborhood[1] + neighborhood[2] + neighborhood[3] + color) * .2;
		#else
			const float3x4 top = float3x4(
				convertToYCoCg(tex2D(_MainTex, uv + float2(-k.x, -k.y))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(0., -k.y))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(k.x, -k.y))));

			const float2x4 middle = float2x4(
				convertToYCoCg(tex2D(_MainTex, uv + float2(-k.x, 0.))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(k.x, 0.))));

			const float3x4 bottom = float3x4(
				convertToYCoCg(tex2D(_MainTex, uv + float2(-k.x, k.y))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(0., k.y))),
				convertToYCoCg(tex2D(_MainTex, uv + float2(k.x, k.y))));

			float4 minimum = min(min(min(min(min(min(min(min(top[0], top[1]), top[2]), middle[0]), middle[1]), bottom[0]), bottom[1]), bottom[2]), color);
			float4 maximum = max(max(max(max(max(max(max(max(top[0], top[1]), top[2]), middle[0]), middle[1]), bottom[0]), bottom[1]), bottom[2]), color);

			float4 average = (top[0] + top[1] + top[2] + middle[0] + middle[1] + bottom[0] + bottom[1] + bottom[2] + color) * .111111;
		#endif

		// Chroma manipulation idea inspired from Playdead's implementation
		#if TAA_ADJUST_CHROMA_EXTENTS
			const float contrast = maximum.x - minimum.x;

			minimum.yz = color.yz - .125 * contrast;
			maximum.yz = color.yz + .125 * contrast;

			average.yz = color.yz;
		#endif

		#if TAA_HISTORY_NEIGHBORHOOD_SAMPLE_PATTERN == 0
			float4 history = convertToYCoCg(tex2D(_HistoryTex, historyUV - motion.xy));
		#else
			float4 history = convertToYCoCg(getFilteredHistorySample(historyUV - motion.xy));
		#endif

		#if TAA_USE_NEIGHBORHOOD_CLIPPING
			const float3 origin = history.rgb - .5 * (minimum.rgb + maximum.rgb);
			const float3 direction = average.rgb - history.rgb;
			const float3 extents = maximum.rgb - .5 * (minimum.rgb + maximum.rgb);

			// SDF AABB-intersection code might work better for this task
			// I am still not satisfied with clipping... Maybe I am doing it wrong
			history = lerp(history, average, saturate(intersectAABB(origin, direction, extents)));
		#else
			history = clamp(history, minimum, maximum);
		#endif

			float difference = abs(color.x - history.x) / max(color.x, max(history.x, minimum.x));
			//float difference = min(abs(minimum.x - history.x), abs(maximum.x - history.x));

			float factor = 1. - difference;
			//float factor = saturate(.125 * difference / (difference + maximum.x - minimum.x));

			factor = lerp(_FeedbackBounds.x, _FeedbackBounds.y, factor * factor);

			// Off-screen check, can't trust a history sample from nowhere
			if (max(abs(motion.x), abs(motion.y)) >= 1.)
			{
				factor = 1.;
			}

			return convertToRGBA(lerp(color, history, factor));
		}

		float4 fragment(Varyings input) : SV_Target
		{
			float2 uv = input.uv;

		#if TAA_REMOVE_JITTER
			// uv -= _Fuzz.xy;
		#endif

			float3 closestFragment = getClosestFragment(uv);

			float3 motion = float3(
				tex2D(_CameraMotionVectorsTexture, uv).xy, closestFragment.z);

			// return lerp(tex2D(_MainTex, input.uv), tex2D(_HistoryTex, input.uv), .5);
			// return float4(closestFragment, 1.);
			return reproject(input.uv, motion);

			// float2 motion = tex2D(_CameraMotionVectorsTexture, input.uv).xy;
			// return lerp(tex2D(_MainTex, input.uv), tex2D(_HistoryTex, input.uv - motion), .5);

			// return float4(motion.xy, 0., 1.) * 200.;

			// return lerp(tex2D(_MainTex, input.uv), tex2D(_HistoryTex, input.uv + motion.xy), .9);
		}
	ENDCG

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
				#pragma vertex vertex
				#pragma fragment fragment
			ENDCG
		}
	}
}
