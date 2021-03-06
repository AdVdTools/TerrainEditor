﻿Shader "Custom/TerrainShader4" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0

		// Textures
		_NoiseTex ("Noise", 2D) = "grey" {}

		_Splat0("Layer0 (Base)", 2D) = "white" {}
		_ThresholdSize("ThresholdSize", float) = 0.1
		_Splat1("Layer1 (R)", 2D) = "white" {}
		_Splat2("Layer2 (G)", 2D) = "white" {}
		_Splat3("Hue/Luminance (BA)", 2D) = "white" {}
		_AlphaWeightFactor("Alpha Weight", float) = 0.5
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _NoiseTex;

		sampler2D _Splat0;
		sampler2D _Splat1;
		sampler2D _Splat2;
		sampler2D _Splat3;

		float _ThresholdSize;
		float _AlphaWeightFactor;

		struct Input {
			float3 worldPos;
			float2 uv_Splat0 : TEXCOORD0;
			float2 uv_Splat1 : TEXCOORD1;
			float2 uv_Splat2 : TEXCOORD2;
			float2 uv_Splat3 : TEXCOORD3;
			float4 color : COLOR;
		};

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

		void surf (Input IN, inout SurfaceOutputStandard o) {
			fixed4 control = IN.color;

			//control = control * control;
			//control = control * control;
			
			fixed4 splat0 = tex2D(_Splat0, IN.uv_Splat0);
			fixed4 splat1 = tex2D(_Splat1, IN.uv_Splat1);
			fixed4 splat2 = tex2D(_Splat2, IN.uv_Splat2);
			fixed4 splat3 = tex2D(_Splat3, IN.uv_Splat3);

			fixed controlSum = control.r + control.g + control.b + control.a;
			if (controlSum < 0.1) controlSum = 0.1;
			fixed4 c = splat0 * control.r + splat1 * control.g + splat2 * control.b + splat3 * control.a;//tex2D (_MainTex, IN.uv_MainTex) * _Color;
			c *= 1 / controlSum;

			o.Albedo = c.rgb;// IN.uv_Splat0.xyy;// IN.worldPos.xyz * 0.1;//
			// Metallic and smoothness come from slider variables
			//o.Metallic = _Metallic;
			//o.Smoothness = _Glossiness;
			o.Smoothness = c.a * 0.8 - 0.1;
			o.Alpha = 1;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
