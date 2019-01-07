Shader "Custom/TerrainShader3" {
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
			fixed4 noise = tex2D(_NoiseTex, IN.uv_Splat0 * 0.23);
			//control += (noise - 0.5) * 0.1;

			//fixed hardness = control.a + (noise.r - 0.5) * 0.4;

			float threshold = 0.5 - _ThresholdSize * 0.5;

			fixed controlR = control.r;
			fixed controlG = control.g;
			fixed weightR = 0, weightG = 0;
			fixed4 splatR = fixed4(0, 0, 0, 0);
			if (controlR > threshold) {
				weightR = (controlR - threshold) / _ThresholdSize;
				splatR = tex2D(_Splat1, IN.uv_Splat1);//TODO add weight from splat luminance/alpha?
				weightR += (splatR.a - 0.5) * _AlphaWeightFactor;
			}
			fixed4 splatG = fixed4(0, 0, 0, 0);
			if (controlG > threshold) {
				weightG = (controlG - threshold) / _ThresholdSize;
				splatG = tex2D(_Splat2, IN.uv_Splat2);//TODO add weight from splat luminance/alpha?
				weightG += (splatG.a - 0.5) * _AlphaWeightFactor;
			}
			fixed maxWeight = max(1, weightR + weightG);
			fixed weightBase = maxWeight - weightR - weightG;
			fixed4 splatBase = fixed4(0, 0, 0, 0);
			if (weightBase > 0) {
				splatBase = tex2D(_Splat0, IN.uv_Splat0);
			}

			fixed4 tint = tex2D(_Splat3, control.ba);
			fixed4 c = tint * (splatBase * weightBase + splatR * weightR + splatG * weightG) / maxWeight;
			//c = fixed4(weightR, weightG, weightBase, 1);

			/*fixed4 c;
			fixed am, am2;
			if (hardness > _ThresholdMin2) {
				am = hardness - _ThresholdMin2;
				c = tex2D(_Splat2, IN.uv_Splat2);
			}
			else if (hardness > _ThresholdMin1) {
				am = hardness - _ThresholdMin1;
				c = tex2D(_Splat1, IN.uv_Splat1);
			}
			else {
				am = hardness;
				c = tex2D(_Splat0, IN.uv_Splat0);
			}
			if (hardness < _ThresholdMax0) {
				am2 = _ThresholdMax0 - hardness;
				c = c * am + tex2D(_Splat0, IN.uv_Splat0) * am2;
				c *= 1 / (am + am2);
			}
			else if (hardness < _ThresholdMax1) {
				am2 = _ThresholdMax1 - hardness;
				c = c * am + tex2D(_Splat1, IN.uv_Splat1) * am2;
				c *= 1 / (am + am2);
			}
			else {
				am2 = 1.0 - hardness;
				c = c * am + tex2D(_Splat2, IN.uv_Splat2) * am2;
				c *= 1 / (am + am2);
			}*/

			//fixed4 splat0 = tex2D(_Splat0, IN.uv_Splat0);
			//fixed4 splat1 = tex2D(_Splat1, IN.uv_Splat1);
			//fixed4 splat2 = tex2D(_Splat2, IN.uv_Splat2);
			//fixed4 splat3 = tex2D(_Splat3, IN.uv_Splat3);

			//fixed4 c = splat0 * control.r + splat1 * control.g + splat2 * control.b + splat3 * control.a;//tex2D (_MainTex, IN.uv_MainTex) * _Color;
			//c *= 1 / (control.r + control.g + control.b + control.a);

			o.Albedo = c.rgb;// IN.uv_Splat0.xyy;// IN.worldPos.xyz * 0.1;//
			// Metallic and smoothness come from slider variables
			//o.Metallic = _Metallic;
			//o.Smoothness = _Glossiness;
			o.Alpha = 1;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
