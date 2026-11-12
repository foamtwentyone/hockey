// Лёд: текстура разметки + зеркальное отражение (планарное, из камеры отражений) с френелем,
// мягкое мультяшное освещение и тени.
Shader "Hockey/Ice"
{
    Properties
    {
        _MainTex ("Ice", 2D) = "white" {}
        _ReflTex ("Reflection", 2D) = "black" {}
        _Refl ("Reflection Strength", Range(0,1)) = 0.38
        _Gloss ("Gloss", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface surf IceLight fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex, _ReflTex;
        half _Refl, _Gloss;
        struct Input { float2 uv_MainTex; float4 screenPos; float3 viewDir; };

        half4 LightingIceLight(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = saturate(dot(s.Normal, lightDir) * 0.5 + 0.5);
            half3 shade = lerp(half3(0.62, 0.68, 0.9), half3(1, 1, 1), saturate(atten * ndl * 1.2));
            half3 h = normalize(lightDir + viewDir);
            half spec = pow(saturate(dot(s.Normal, h)), 120) * _Gloss * atten;
            half4 c; c.rgb = (s.Albedo * shade + spec) * _LightColor0.rgb; c.a = 1;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            half3 ice = tex2D(_MainTex, IN.uv_MainTex).rgb;
            float2 suv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            half3 refl = tex2D(_ReflTex, suv).rgb;
            half fres = 0.35 + 0.65 * pow(1 - saturate(dot(normalize(IN.viewDir), half3(0, 1, 0))), 3);
            half k = _Refl * fres;
            // отражение замещает часть цвета льда, а не прибавляется к нему — лёд не выгорает
            o.Albedo = ice * 0.92 * (1 - k);
            o.Emission = refl * k * 0.75;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
