Shader "Nocturnis/ForestSurface"
{
 Properties {
  _MainTex ("Source atlas", 2D) = "white" {}
  _Color ("Weathered tint", Color) = (0.68,0.7,0.61,1)
  _Cutoff ("Leaf cutout", Range(0,1)) = 0.38
  _Wind ("Wind strength", Range(0,0.15)) = 0.045
  _Glossiness ("Smoothness", Range(0,1)) = 0.08
 }
 SubShader {
  Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
  LOD 200
  Cull Off
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows addshadow vertex:vert alphatest:_Cutoff
  #pragma target 3.0
  #pragma multi_compile_instancing
  sampler2D _MainTex;
  fixed4 _Color;
  half _Glossiness, _Wind;
  float4 _NocturnisHero;
  struct Input { float2 uv_MainTex; float3 worldPos; };
  void vert(inout appdata_full v) {
   float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
   float sway=sin(_Time.y*1.3+p.x*.35+p.z*.27)*.7+sin(_Time.y*2.1+p.z*.8)*.3;
   v.vertex.xz+=float2(sway,sway*.43)*_Wind*saturate(v.vertex.y*.25);
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
   // Cut a small view corridor when branches pass between camera and hero.
   float3 ray=normalize(_WorldSpaceCameraPos.xyz-_NocturnisHero.xyz);
   float3 offset=IN.worldPos-(_NocturnisHero.xyz+float3(0,.8,0));
   float along=dot(offset,ray);
   float d=length(offset-ray*along);
   if(_NocturnisHero.w>.5 && along>1 && along<18 && d<.72)clip(-1);
   o.Albedo=c.rgb;o.Alpha=c.a;o.Smoothness=_Glossiness;o.Metallic=0;
  }
  ENDCG
 }
 Fallback "Transparent/Cutout/Diffuse"
}
