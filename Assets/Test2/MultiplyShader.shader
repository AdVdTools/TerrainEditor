Shader "CustomMobile/Multiply"
{

	Properties
	{
		[NoScaleOffset] _MainTex("Base (RGB)", 2D) = "white" {}
	}

	SubShader
	{ 
		Tags {"Queue" = "Transparent" "RenderType" = "Transparent" }
		LOD 100

		ZWrite Off
		/*Cull Off*/
		Blend DstColor Zero

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
#pragma multi_compile_instancing
//#pragma target 3.0
// shadow helper functions and macros
#include "AutoLight.cginc"

		
struct appdata
{
	float4 vertex : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
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
	//UNITY_VERTEX_INPUT_INSTANCE_ID // necessary only if you want to access instanced properties in fragment Shader.
};

// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
//#pragma instancing_options assumeuniformscaling
//UNITY_INSTANCING_BUFFER_START(Props)
//// put more per-instance properties here
//UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
//UNITY_INSTANCING_BUFFER_END(Props)

v2f vert(appdata v)
{
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	//UNITY_TRANSFER_INSTANCE_ID(v, o); // necessary only if you want to access instanced properties in the fragment Shader.

	o.pos = UnityObjectToClipPos(v.vertex);
	o.uv = v.uv;

	//fixed4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

	half3 worldNormal = UnityObjectToWorldNormal(v.normal);
	half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
	o.diff = nl * _LightColor0.rgb;
	o.ambient = ShadeSH9(half4(worldNormal,1));//TODO sample ambient?
	o.color = v.color;// *color;
	// compute shadows data
	TRANSFER_SHADOW(o)
	return o;
}

sampler2D _MainTex;

fixed4 frag(v2f i) : SV_Target
{
	//UNITY_SETUP_INSTANCE_ID(i); // necessary only if any instanced properties are going to be accessed in the fragment Shader.

	fixed4 col = tex2D(_MainTex, i.uv) * i.color;
	// compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
	//fixed shadow = SHADOW_ATTENUATION(i);
	// darken light's illumination with shadow, keep ambient intact
	//fixed3 lighting = i.diff * shadow + i.ambient;
	//col.rgb *= lighting;//TODO remove all lighting

	return col;//TODO tree leaves shader with close leaves fade out
}
			ENDCG

		}

		// shadow casting support
		//UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
	}
}