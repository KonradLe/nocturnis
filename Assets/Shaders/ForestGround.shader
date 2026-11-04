Shader "Nocturnis/ForestGround"
{
 Properties {
  _Color ("Damp soil",Color)=(.18,.145,.105,1)
  _Moss ("Faded moss",Color)=(.19,.21,.14,1)
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows
  #pragma target 3.0
  fixed4 _Color,_Moss;
  struct Input {float3 worldPos;};
  float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p) {
   float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
   return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   float2 p=IN.worldPos.xz;
   float patch=noise(p*.32)*.6+noise(p*1.4)*.28+noise(p*6)*.12;
   float grit=noise(p*32);
   o.Albedo=lerp(_Color.rgb,_Moss.rgb,smoothstep(.40,.70,patch))*(.68+.5*patch)*(.87+.22*grit);
   o.Smoothness=.08;o.Occlusion=.85+.15*grit;o.Metallic=0;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
