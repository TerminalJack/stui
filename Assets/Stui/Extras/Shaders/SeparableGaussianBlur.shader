// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

Shader "Hidden/SeparableGaussianBlur"
{
    Properties
    {
        _MainTex    ("SourceTex",       2D)     = "white" {}
        _Direction  ("Blur Direction",  Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2D(_MainTex);

            float4   _MainTex_TexelSize;  // x=1/width, y=1/height
            float2   _Direction;          // (1,0)=horiz, (0,1)=vert

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // 5-tap Catmull–Rom weights: [1,4,6,4,1]/16
                const float w0 = 0.0625;  // 1/16
                const float w1 = 0.25;    // 4/16
                const float w2 = 0.375;   // 6/16

                float2 uv = i.uv;
                float2 off = _Direction * _MainTex_TexelSize.xy;

                // Precompute & clamp each sample coord
                float2 uv0 = saturate(uv);
                float2 uv1 = saturate(uv + off);
                float2 uv2 = saturate(uv - off);
                float2 uv3 = saturate(uv + off * 2);
                float2 uv4 = saturate(uv - off * 2);

                float4 sum  = UNITY_SAMPLE_TEX2D(_MainTex, uv0) * w2;
                sum        += UNITY_SAMPLE_TEX2D(_MainTex, uv1) * w1;
                sum        += UNITY_SAMPLE_TEX2D(_MainTex, uv2) * w1;
                sum        += UNITY_SAMPLE_TEX2D(_MainTex, uv3) * w0;
                sum        += UNITY_SAMPLE_TEX2D(_MainTex, uv4) * w0;

                return sum;
            }
            ENDCG
        }
    }
}

