// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

Shader "Hidden/BleedTransparent"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2D(_MainTex);
            float4 _MainTex_TexelSize;

            #define RADIUS 4

            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            v2f vert(appdata_full v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o;
            }

            float4 frag(v2f i):SV_Target
            {
                float4 c = UNITY_SAMPLE_TEX2D(_MainTex, i.uv);

                if (c.a > 0.0001)
                    return c;

                float3 colSum = 0;
                float  aSum   = 0;
                float2 texel = _MainTex_TexelSize.xy;

                for (int y = -RADIUS; y <= RADIUS; ++y)
                for (int x = -RADIUS; x <= RADIUS; ++x)
                {
                    float2 uv2 = saturate(i.uv + float2(x,y) * texel);
                    float4 s   = UNITY_SAMPLE_TEX2D(_MainTex, uv2);
                    if (s.a > 0.0001)
                    {
                        colSum += s.rgb * s.a;
                        aSum   += s.a;
                    }
                }

                if (aSum <= 0.0001)
                    return c;

                return float4(colSum / aSum, 0);
            }
            ENDCG
        }
    }
}