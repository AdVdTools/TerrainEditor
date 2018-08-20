Shader "Hidden/BrushProjector"
{
	Properties
	{
		_MainTex("Texture", 2D) = "black" {}
		_MainColor("Color", Color) = (1, 1, 1, 1)
		_Opacity("Opacity", Float) = 0.25
	}

	CGINCLUDE
	float doBrushShape(float value, float distSqrt)
	{
		float outterThreshold = step(1.0, distSqrt);
		float innerThreshold = step(0.9, distSqrt);
		return value * (1 - innerThreshold) + innerThreshold - outterThreshold;
	}
	ENDCG

	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 100

		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				UNITY_FOG_COORDS(1)
				float4 pos : SV_POSITION;
				float3 vertex : TEXCOORD0;
			};

			float4x4 _ProjMatrix;
			fixed4 _MainColor;
			sampler2D _MainTex;

			float _Opacity;

			v2f vert(appdata v)
			{
				v2f o;
				float4 projVertex = mul(_ProjMatrix, v.vertex);
				o.vertex = projVertex.xyz / projVertex.w;
				o.pos = UnityObjectToClipPos(v.vertex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = _MainColor;
				float zScale = 1 / sqrt(1 - i.vertex.z * i.vertex.z);
				float2 coords = float2(i.vertex.x, i.vertex.y) * zScale;

				fixed texAlpha = tex2D(_MainTex, coords * 0.5 + 0.5).a;
				fixed texLimits = step(0, 1 - abs(coords.x)) * step(0, 1 - abs(coords.y));
				fixed value = texLimits * texAlpha;

				col.a = value * _Opacity;

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				UNITY_FOG_COORDS(1)
				float4 pos : SV_POSITION;
				float3 vertex : TEXCOORD0;
			};

			float4x4 _ProjMatrix;
			fixed4 _MainColor;

			float _Opacity;
			
			v2f vert (appdata v)
			{
				v2f o;
				float4 projVertex = mul(_ProjMatrix, v.vertex);
				o.vertex = projVertex.xyz / projVertex.w;
				o.pos = UnityObjectToClipPos(v.vertex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = _MainColor;
				float distSqrt = dot(i.vertex, i.vertex);
				col.a = doBrushShape(_Opacity, distSqrt);
				//col.a = step(15.0, distSqrt % 20.0);

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				UNITY_FOG_COORDS(1)
				float4 pos : SV_POSITION;
				float3 vertex : TEXCOORD0;
			};

			float4x4 _ProjMatrix;
			fixed4 _MainColor;

			float _Opacity;

			v2f vert(appdata v)
			{
				v2f o;
				float4 projVertex = mul(_ProjMatrix, v.vertex);
				o.vertex = projVertex.xyz / projVertex.w;
				o.pos = UnityObjectToClipPos(v.vertex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = _MainColor;
				float distSqrt = dot(i.vertex, i.vertex);
				col.a = doBrushShape(_Opacity * (1 - distSqrt), distSqrt);
				//col.a = step(15.0, distSqrt % 20.0);

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
