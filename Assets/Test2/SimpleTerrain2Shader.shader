Shader "Custom/Simple Terrain 2"
{

	Properties
	{
		[NoScaleOffset] _Detail0("Detail0", 2D) = "black" {}
		[NoScaleOffset] _Detail1("Detail1", 2D) = "black" {}
		[NoScaleOffset] _Detail2("Detail2", 2D) = "black" {}
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

sampler2D _Detail0;
sampler2D _Detail1;
sampler2D _Detail2;

fixed4 frag(v2f i) : SV_Target
{
	fixed control = i.color.a;

	fixed4 detail;
	fixed detailStrength;
	if (control < 0.5) {
		detailStrength = 1 - control * 2;
		detail = tex2D(_Detail0, i.uv) * detailStrength + tex2D(_Detail1, i.uv) * (1 - detailStrength);
	}
	else {
		detailStrength = control * 2 - 1;
		detail = tex2D(_Detail1, i.uv) * (1 - detailStrength) + tex2D(_Detail2, i.uv) * detailStrength;
	}

	fixed blendFactor = detail.a;

	fixed4 col = fixed4(detail.rgb * blendFactor + i.color.rgb * (1 - blendFactor), 1);
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