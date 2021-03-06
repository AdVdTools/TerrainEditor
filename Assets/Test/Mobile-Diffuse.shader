// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

// Simplified Diffuse shader. Differences from regular Diffuse one:
// - no Main Color
// - fully supports only 1 directional light. Other lights can affect it, but it will be per-vertex/SH.

Shader "CustomMobile/Diffuse" {
Properties {
	_Color("Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB)", 2D) = "white" {}
	[HideInInspector]_LODFade("LOD Fade", Float) = 1
}
SubShader {
    Tags { "RenderType"="Opaque" }
    LOD 150

CGPROGRAM
#pragma surface surf Lambert noforwardadd vertex:vert

sampler2D _MainTex;

// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
// #pragma instancing_options assumeuniformscaling
UNITY_INSTANCING_BUFFER_START(Props)
// put more per-instance properties here
UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
UNITY_DEFINE_INSTANCED_PROP(fixed, _LODFade)
UNITY_INSTANCING_BUFFER_END(Props)

struct Input {
    float2 uv_MainTex;
	float4 color : COLOR;
};

void vert(inout appdata_full i)
{
	fixed4 instanceColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

	float fadeMultiplier = UNITY_ACCESS_INSTANCED_PROP(Props, _LODFade);

	i.vertex *= fadeMultiplier;//TODO alt: fade to color (sample)
	i.color *= instanceColor;
}

void surf (Input IN, inout SurfaceOutput o) 
{
    fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * IN.color;
    o.Albedo = c.rgb;
    o.Alpha = c.a;
}
ENDCG
}

Fallback "Mobile/VertexLit"
}
