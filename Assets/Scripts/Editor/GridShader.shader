Shader "Hidden/AdVd/GridShader"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	_Color("Color", Color) = (1, 1, 1, 1)
	}
		SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off
		LOD 100

		// Lines
		Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"

		struct appdata
	{
		float4 vertex : POSITION;
		UNITY_VERTEX_INPUT_INSTANCE_ID

	};

	struct v2f
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
		UNITY_VERTEX_INPUT_INSTANCE_ID // necessary only if you want to access instanced properties in fragment Shader.
	};

	//float4 _Color;

	UNITY_INSTANCING_BUFFER_START(Props)
		UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
		UNITY_INSTANCING_BUFFER_END(Props)

		v2f vert(appdata v)
	{
		v2f o;
		UNITY_SETUP_INSTANCE_ID(v);
		UNITY_TRANSFER_INSTANCE_ID(v, o); // necessary only if you want to access instanced properties in the fragment Shader.

										  /*float3 vertex = float3(
										  _Transform.x + _Transform.z * v.vertex.x,
										  _Transform.y + _Transform.w * v.vertex.y, 1);*///Dont forget depth!
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = float2(0, 0);
		return o;
	}

	fixed4 frag(v2f i) : SV_Target
	{
		UNITY_SETUP_INSTANCE_ID(i); // necessary only if any instanced properties are going to be accessed in the fragment Shader.
	fixed4 col = _Color;
	return col;
	}
		ENDCG
	}

		// Area
		Pass
	{
		CGPROGRAM
#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"

		struct appdata
	{
		float4 vertex : POSITION;
	};

	struct v2f
	{
		float2 uv : TEXCOORD0;
		float4 vertex : SV_POSITION;
	};

	sampler2D _MainTex;
	float4 _MainTex_ST;

	float4 _Color;

	v2f vert(appdata v)
	{
		v2f o;
		/*float3 vertex = float3(
		_Transform.x + _Transform.z * v.vertex.x,
		_Transform.y + _Transform.w * v.vertex.y, 1);*/
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = float2(v.vertex.x * 0.5 + 0.5, v.vertex.y * 0.5 + 0.5);
		return o;
	}

	fixed4 frag(v2f i) : SV_Target
	{
		fixed4 col = _Color * tex2D(_MainTex, i.uv);
		col.a *= 0.5;
		return col;
	}
		ENDCG
	}
	}
}
