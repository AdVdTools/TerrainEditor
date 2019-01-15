Shader "Custom/Simple Terrain"
{

	Properties
	{
		[NoScaleOffset] _Tex0("Texture0", 2D) = "white" {}
		[NoScaleOffset] _Tex1("Texture1", 2D) = "white" {}
		[NoScaleOffset] _Tex2("Texture2", 2D) = "white" {}
		[NoScaleOffset] _Tex3("Texture3", 2D) = "white" {}
		_Scale("Scale", Float) = 1
	}

	SubShader
	{
		Pass
		{
			Tags {"LightMode" = "ForwardBase"}

			CGPROGRAM
		
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "Lighting.cginc"

// compile shader into multiple variants, with and without shadows
// (we don't care about any lightmaps yet, so skip these variants)
#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
// shadow helper functions and macros
#include "AutoLight.cginc"

		
struct appdata
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
	float3 normal : NORMAL;
	fixed4 color : COLOR;
};

struct v2f
{
	float2 uv : TEXCOORD0;
	SHADOW_COORDS(1) // put shadows data into TEXCOORD1
	fixed4 color : COLOR0;
	fixed3 diff : COLOR1;
	fixed3 ambient : COLOR2;
	float4 pos : SV_POSITION;
};
			
float _Scale;

v2f vert(appdata v)
{
	v2f o;
	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.uv * _Scale;

	half3 worldNormal = UnityObjectToWorldNormal(v.normal);
	half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
	o.diff = nl * _LightColor0.rgb;
	o.ambient = ShadeSH9(half4(worldNormal,1));//TODO sample ambient?
	o.color = v.color;
	// compute shadows data
	TRANSFER_SHADOW(o)
	return o;
}

sampler2D _Tex0;
sampler2D _Tex1;
sampler2D _Tex2;
sampler2D _Tex3;

fixed4 frag(v2f i) : SV_Target
{
	fixed control = i.color.a;
	fixed4 tex;
	if (control < 0.3) {
		tex = ((6 - control * 20) * tex2D(_Tex0, i.uv) + (control * 20) * tex2D(_Tex1, i.uv)) * 0.167;
	}
	else if (control < 0.35) {
		tex = tex2D(_Tex1, i.uv);
	}
	else if (control < 0.65) {
		tex = ((13 - control * 20) * tex2D(_Tex1, i.uv) + (control * 20 - 7) * tex2D(_Tex2, i.uv)) * 0.167;
	}
	else if (control < 0.7) {
		tex = tex2D(_Tex2, i.uv);
	}
	else {
		tex = ((20 - control * 20) * tex2D(_Tex2, i.uv) + (control * 20 - 14) * tex2D(_Tex3, i.uv)) * 0.167;
	}
	//fixed4 tex = tex2D(_Tex0, i.uv);

	fixed4 col = fixed4(tex.rgb * i.color.rgb, 1);
	// compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
	fixed shadow = SHADOW_ATTENUATION(i);
	// darken light's illumination with shadow, keep ambient intact
	fixed3 lighting = i.diff * shadow + i.ambient;
	col.rgb *= lighting;
	return col;
}
			ENDCG

		}

		// shadow casting support
		UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
	}
}