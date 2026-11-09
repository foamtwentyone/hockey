// Постобработка для встроенного рендера: мягкое свечение (bloom), цветокоррекция, виньетка
// и радиальное размытие на большой скорости.
Shader "Hockey/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex;
    float4 _MainTex_TexelSize;
    float _Threshold, _Knee, _Bloom, _Saturation, _Contrast, _Vignette, _Speed, _Exposure;
    float4 _Tint;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    half Luma(half3 c) { return dot(c, half3(0.299, 0.587, 0.114)); }

    // сглаживание краёв в духе FXAA: размываем вдоль найденной границы
    half3 Fxaa(float2 uv)
    {
        float2 t = _MainTex_TexelSize.xy;
        half3 cM = tex2D(_MainTex, uv).rgb;
        half lM = Luma(cM);
        half lN = Luma(tex2D(_MainTex, uv + float2(0, t.y)).rgb), lS = Luma(tex2D(_MainTex, uv - float2(0, t.y)).rgb);
        half lE = Luma(tex2D(_MainTex, uv + float2(t.x, 0)).rgb), lW = Luma(tex2D(_MainTex, uv - float2(t.x, 0)).rgb);
        half mx = max(lM, max(max(lN, lS), max(lE, lW))), mn = min(lM, min(min(lN, lS), min(lE, lW)));
        if (mx - mn < max(0.04, mx * 0.12)) return cM;
        float2 dir = float2(-(lN - lS), (lE - lW));
        float len = max(abs(dir.x), abs(dir.y));
        dir = dir / max(len, 1e-4) * t;
        half3 a = 0.5 * (tex2D(_MainTex, uv + dir * (1.0 / 3.0 - 0.5)).rgb + tex2D(_MainTex, uv + dir * (2.0 / 3.0 - 0.5)).rgb);
        half3 b = a * 0.5 + 0.25 * (tex2D(_MainTex, uv - dir * 0.5).rgb + tex2D(_MainTex, uv + dir * 0.5).rgb);
        half lb = Luma(b);
        return (lb < mn || lb > mx) ? a : b;
    }

    half3 Box(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        // 0: выделение ярких мест
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                half3 c = Box(i.uv, 1);
                half br = max(c.r, max(c.g, c.b));
                half soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
                soft = soft * soft / (4 * _Knee + 1e-4);
                half k = max(soft, br - _Threshold) / max(br, 1e-4);
                return half4(c * k, 1);
            }
            ENDCG
        }
        // 1: уменьшение с размытием
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
            ENDCG
        }
        // 2: увеличение со сложением
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
            ENDCG
        }
        // 3: итог
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                half3 c = Fxaa(uv);
                // радиальное размытие по краям на скорости
                if (_Speed > 0.001)
                {
                    float2 dir = uv - 0.5;
                    half w = saturate(length(dir) * 2 - 0.35);
                    half3 acc = c;
                    for (int k = 1; k < 6; k++) acc += tex2D(_MainTex, uv - dir * k * 0.012 * _Speed * w).rgb;
                    c = acc / 6;
                }
                c += tex2D(_BloomTex, uv).rgb * _Bloom;
                c *= _Exposure;
                // мягкое «плечо»: всё ярче 0.8 плавно сжимается, без белых пятен
                half3 over = max(c - 0.8, 0);
                c = min(c, 0.8) + over / (1 + over * 2.5);
                // цветокоррекция: тёплый оттенок, насыщенность, контраст
                c *= _Tint.rgb;
                half l = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(l.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                // виньетка
                float2 d = (uv - 0.5) * float2(1.0, 0.8);
                c *= 1 - _Vignette * smoothstep(0.25, 0.85, dot(d, d) * 2.2);
                return half4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
