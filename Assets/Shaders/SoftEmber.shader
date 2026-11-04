Shader "Nocturnis/SoftEmber"
{
 Properties { _Color ("Tint", Color) = (1,1,1,1) }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Blend SrcAlpha One
  Cull Off ZWrite Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
   struct v2f { float4 position:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
   v2f vert(appdata v) { v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o; }
   fixed4 frag(v2f i):SV_Target
   { float d=length(i.uv*2-1);i.color.a*=pow(saturate(1-d),2);return i.color; }
   ENDCG
  }
 }
}
