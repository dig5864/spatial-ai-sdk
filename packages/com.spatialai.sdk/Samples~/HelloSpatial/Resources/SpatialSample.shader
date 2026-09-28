Shader "SpatialAI/SampleUnlit"
{
    Properties { _Color ("Color", Color) = (0.2,0.7,0.9,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color;
            struct Attributes { float4 position : POSITION; };
            struct Varyings { float4 position : SV_POSITION; };
            Varyings vert(Attributes input) { Varyings o; o.position = UnityObjectToClipPos(input.position); return o; }
            float4 frag(Varyings input) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
