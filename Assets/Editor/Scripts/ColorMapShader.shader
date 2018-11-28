Shader "Hidden/AdVd/ColorMapShader"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}

	
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off
		LOD 100


		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma target 3.0

			#include "UnityCG.cginc"


			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv2 : TEXCOORD1;
			};

			struct v2f
			{
				float4 color : COLOR;
				float2 uv2 : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;//Vector4(1 / width, 1 / height, width, height)
			float4 _MainTex_ST;

			v2f vert(appdata v)
			{
				v2f o;

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = float4(1,1,0,1);
				//o.color = tex2dlod(_MainTex, float4(v.uv2.xy, 0, 0/*mip level?*/));
				//TODO sampling? build uv2, 
				o.uv2 = v.uv2;

				//TODO: Eventually move to rect grid (less tex reads)


				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = i.color;

				float2 texSize = _MainTex_TexelSize.zw;
				float2 invTexSize = _MainTex_TexelSize.xy;

				int2 intUV2 = i.uv2;

				float2 dUV2 = i.uv2 - intUV2;

				/////

				float dx = dUV2.x, dy = dUV2.y;
				float2 off0, off1, off2;
				float3 barCoords;

				if ((intUV2.y & 1) == 0)
				{
					float dXY = dx + dy;
					if (dXY < 1)
					{
						//indices = new Vector3Int(index, index + 1, index + width);
						//barycentricCoordinate = new Vector3(1 - dXY, dx, dy);
						off0 = float2(0, 0); off1 = float2(1, 0); off2 = float2(0, 1);
						barCoords = float3(1 - dXY, dx, dy);
					}
					else
					{
						//indices = new Vector3Int(index + width + 1, index + 1, index + width);
						//barycentricCoordinate = new Vector3(dXY - 1, 1 - dy, 1 - dx);
						off0 = float2(1, 1); off1 = float2(1, 0); off2 = float2(0, 1);
						barCoords = float3(dXY - 1, 1 - dy, 1 - dx);
					}
				}
				else
				{
					float dXY = 1 - dx + dy;
					if (dx > dy)
					{
						//indices = new Vector3Int(index, index + 1, index + width + 1);
						//barycentricCoordinate = new Vector3(1 - dx, 1 - dXY, dy);
						off0 = float2(0, 0); off1 = float2(1, 0); off2 = float2(1, 1);
						barCoords = float3(1 - dx, 1 - dXY, dy);
					}
					else
					{
						//indices = new Vector3Int(index, index + width, index + width + 1);
						//barycentricCoordinate = new Vector3(1 - dy, dXY - 1, dx);
						off0 = float2(0, 0); off1 = float2(0, 1); off2 = float2(1, 1);
						barCoords = float3(1 - dy, dXY - 1, dx);
						
					}
				}

				float2 uv2 = intUV2 + float2(0.5, 0.5);
				float4 color = tex2D(_MainTex, (uv2 + off0) * invTexSize) * barCoords.x +
					tex2D(_MainTex, (uv2 + off1) * invTexSize) * barCoords.y + 
					tex2D(_MainTex, (uv2 + off2) * invTexSize) * barCoords.z;

				//color = tex2D(_MainTex, (uv2 + off0) * invTexSize);


				/////


				//float2 coord = (i.uv2 + float2(0.5, 0.5)) * invTexSize;
				float2 coord = (intUV2 + float2(0.5, 0.5)) * invTexSize;

				fixed4 c0 = tex2D(_MainTex, float4(coord.xy, 0, 0/*mip level?*/));

				col = color;

				col.rgb *= col.a;
				col.a = 0.5;
				return col;
			}
			ENDCG
		}
	}
}
