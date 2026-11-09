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
                half3 c = tex2D(_MainTex, uv).rgb;
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
