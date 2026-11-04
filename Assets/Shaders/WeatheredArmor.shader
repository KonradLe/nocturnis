Shader "Nocturnis/WeatheredArmor"
{
 Properties
 {
  _Color("Tint",Color)=(1,1,1,1)
  _MainTex("Source base color",2D)="white"{}
  _MetallicGlossMap("Source metallic and smoothness",2D)="white"{}
  _Metallic("Metallic",Range(0,1))=.7
  _Glossiness("Smoothness",Range(0,1))=.4
  _Weathering("Oxide and accumulated dirt",Range(0,1))=.72
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" }
  LOD 300
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  #pragma shader_feature_local _METALLICGLOSSMAP
  sampler2D _MainTex,_MetallicGlossMap;
  fixed4 _Color;
  half _Metallic,_Glossiness,_Weathering;
  struct Input { float2 uv_MainTex; float3 worldPos; };
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   fixed4 base=tex2D(_MainTex,IN.uv_MainTex)*_Color;
   half metal=_Metallic,polish=_Glossiness;
   #ifdef _METALLICGLOSSMAP
    half4 map=tex2D(_MetallicGlossMap,IN.uv_MainTex);
    metal=map.r;polish=map.a;
   #endif
   // The source roughness painting also locates tarnish. No additional art atlas.
   half dirt=saturate((.62-polish)*2.1)*_Weathering;
   o.Albedo=base.rgb*lerp(half3(.8,.85,.9),half3(.29,.24,.20),dirt);
   o.Metallic=lerp(metal,.12,dirt);
   o.Smoothness=lerp(min(polish,.43),.12,dirt);
   o.Occlusion=1;
   o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Standard"
}
