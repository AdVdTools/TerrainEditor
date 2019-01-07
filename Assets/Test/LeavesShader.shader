// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced '_World2Object' with 'unity_WorldToObject'

Shader "Custom/LeavesShader" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Cutoff ("Cutoff", Range(0,1)) = 0.7
		_ShadowOffset ("Shadow Offset", Range(0,0.1)) = 0.05
	}
	SubShader {
		Tags { "Queue" = "Geometry+1" "RenderType" = "TransparentCutout" }
		LOD 200

		Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert keepalpha

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

//#pragma debug

		struct Input {
			float2 uv_MainTex;
			float4 color : COLOR;

			float3 viewDir;

			float3 worldNormal;
			half4 tangent;
		};
			
		void vert(inout appdata_full i, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);

			half4 p_tangent = mul(unity_ObjectToWorld, half4(i.tangent.xyz, 0/*<-- ignore translation!*/));

			o.tangent = half4(normalize(p_tangent.xyz), -1);
		}

		half _Cutoff;
		half _ShadowOffset;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
		// put more per-instance properties here
		UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
		UNITY_INSTANCING_BUFFER_END(Props)


		sampler2D _MainTex;

		void surf (Input IN, inout SurfaceOutputStandard o) {
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			fixed4 instanceColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

			if (c.a < _Cutoff) {
				float2 uvOffset = IN.uv_MainTex;
				half3 binormal = cross(IN.worldNormal.xyz, IN.tangent.xyz) * IN.tangent.w;
				float2 uvSpaceViewDir = float2(dot(IN.viewDir, IN.tangent), dot(IN.viewDir, binormal));
				uvOffset -= uvSpaceViewDir * _ShadowOffset;

				c = tex2D(_MainTex, uvOffset);

				clip(c.a - _Cutoff);

				o.Albedo = fixed3(0, 0, 0);
				o.Alpha = 0.5 * dot(IN.viewDir, IN.worldNormal);//Too many interpolators to get a distance value
			}
			else {
				o.Albedo = c.rgb * IN.color * instanceColor;
				o.Alpha = c.a;
			}

		}
		ENDCG
	}
	FallBack "Diffuse"
}
