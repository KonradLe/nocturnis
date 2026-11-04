Shader "Nocturnis/WeatheredMasonry" {
 Properties {
  _MainTex ("Scanned stone colour", 2D) = "white" {}
  _BumpMap ("Stone and joint relief", 2D) = "bump" {}
  _RoughnessMap ("Roughness", 2D) = "white" {}
  _OcclusionMap ("Joint occlusion", 2D) = "white" {}
  _Color ("Stone tint", Color) = (1,1,1,1)
  _DirtColor ("Settled grime", Color) = (.66,.64,.60,1)
  _BumpScale ("Normal strength", Range(0,2)) = 1.05
  _Saturation ("Natural colour", Range(0,1)) = .45
  _DirtStrength ("Large-scale weathering", Range(0,1)) = .23
  _Smoothness ("Maximum smoothness", Range(0,.5)) = .22
  _TileMeters ("Texture width in metres", Float) = 2.7
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  LOD 250
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  #include "UnityStandardUtils.cginc"
  sampler2D _MainTex, _BumpMap, _RoughnessMap, _OcclusionMap;
  fixed4 _Color,_DirtColor;
  half _BumpScale,_Saturation,_DirtStrength,_Smoothness;
  struct Input {float2 uv_MainTex;float3 worldPos;};
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float2 uv=IN.uv_MainTex;
   fixed3 colour=tex2D(_MainTex,uv).rgb;
   half luminance=dot(colour,half3(.2126,.7152,.0722));
   colour=lerp(luminance.xxx,colour,_Saturation)*_Color.rgb;
   // Low-frequency variation is continuous across baked tiles and remains
   // stable while moving. No extra texture fetches or tessellation.
   half macro=.5+.5*sin(IN.worldPos.x*.47+sin(IN.worldPos.z*.21)*1.9)*cos(IN.worldPos.z*.39-IN.worldPos.x*.13);
   colour*=lerp(half3(1,1,1),_DirtColor.rgb,macro*_DirtStrength);
   o.Albedo=colour;
   o.Normal=UnpackScaleNormal(tex2D(_BumpMap,uv),_BumpScale);
   half rough=tex2D(_RoughnessMap,uv).r;
   o.Smoothness=clamp((1-rough)*_Smoothness,.015,.26);
   o.Metallic=0;
   o.Occlusion=lerp(1,tex2D(_OcclusionMap,uv).r,.8);
   o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
