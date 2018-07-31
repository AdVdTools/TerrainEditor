// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Hidden/BrushProjector"
{
	Properties
	{
		_MainColor ("Color", Color) = (1, 1, 1, 1)
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 100

		//Blend SrcAlpha OneMinusSrcAlpha
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
				// sample the texture
				fixed4 col = _MainColor;
				float distSqrt = dot(i.vertex, i.vertex);
				col.a = 0.25 - step(1.0, distSqrt) + step(0.9, distSqrt) * 0.75;
				//col.a = step(15.0, distSqrt % 20.0);

				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
