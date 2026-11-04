Shader "Nocturnis/SpellLight"
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
   struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; };
   struct v2f { float4 position:SV_POSITION; fixed4 color:COLOR; };
   v2f vert(appdata v) { v2f o;o.position=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;return o; }
   fixed4 frag(v2f i):SV_Target { return i.color; }
   ENDCG
  }
 }
}
