// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

Shader "Hidden/BicubicResize"
{
  Properties
  {
    _MainTex ("SourceTex", 2D) = "white" {}
  }
  SubShader
  {
    Cull Off ZWrite Off ZTest Always

    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"

      UNITY_DECLARE_TEX2D(_MainTex);

      float4    _MainTex_TexelSize; // x=1/width, y=1/height

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

      // Catmull–Rom weight
      float CubicWeight(float x)
      {
        x = abs(x);
        float x2 = x*x;
        float x3 = x2*x;
        if (x <= 1.0)      return 1.5*x3 - 2.5*x2 + 1.0;
        else if (x < 2.0)  return -0.5*x3 + 2.5*x2 - 4.0*x + 2.0;
        return 0.0;
      }

      float4 frag(v2f i) : SV_Target
      {
        // texSize in pixels
        float2 texSize = float2(1.0/_MainTex_TexelSize.x,
                                 1.0/_MainTex_TexelSize.y);

        // map uv to pixel space, offset by half-texel
        float2 coord  = i.uv * texSize - 0.5;
        float2 basePx = floor(coord);
        float2 frac   = coord - basePx;

        float3 sumPremult = float3(0,0,0);
        float  sumAlphaW  = 0;
        float  sumW       = 0;

        // 4×4 sample
        for (int yy=-1; yy<=2; yy++)
        for (int xx=-1; xx<=2; xx++)
        {
          float2 samplePx = basePx + float2(xx,yy) + 0.5;
          float2 sampleUV = samplePx / texSize;
          sampleUV = saturate(sampleUV); // clamp to 0..1

          float4 c = UNITY_SAMPLE_TEX2D(_MainTex, sampleUV);

          float w = CubicWeight(xx - frac.x)
                  * CubicWeight(yy - frac.y);

          // premultiply color by alpha
          sumPremult += c.rgb * c.a * w;
          sumAlphaW  += c.a * w;
          sumW       += w;
        }

        // avoid divide-by-zero
        if (sumAlphaW <= 0.0001)
          return float4(0,0,0,0);

        float  outA = sumAlphaW / sumW;
        float3 outRGB = sumPremult / sumAlphaW;

        return float4(outRGB, outA);
      }
      ENDCG
    }
  }
}
