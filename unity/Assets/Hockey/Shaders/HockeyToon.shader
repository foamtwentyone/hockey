// Мягкий мультяшный шейдер в стиле «игрушечных» low-poly ассетов:
// плавный переход свет/тень с цветным оттенком теней, лёгкий блик, подсветка контура (rim),
// цвет вершин (запечённые градиенты/затенение) и режим «без освещения» для неба и светящихся объектов.
Shader "Hockey/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _ShadeColor ("Shade Tint", Color) = (0.58,0.62,0.86,1)
        _ShadeSoft ("Shade Softness", Range(0.02,1)) = 0.32
        _RimColor ("Rim Color", Color) = (1,1,1,0.28)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.2
        _Gloss ("Gloss", Range(0,1)) = 0.18
        _Unlit ("Unlit", Range(0,1)) = 0
        _UseVColor ("Use Vertex Color", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _ShadeColor;
        half _ShadeSoft;
        fixed4 _RimColor;
        half _RimPower;
        half _Gloss;
        half _Unlit;
        half _UseVColor;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
            float3 viewDir;
        };

        half4 LightingToon (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half wrapped = ndl * 0.5h + 0.5h;
            half ramp = smoothstep(0.5h - _ShadeSoft * 0.5h, 0.5h + _ShadeSoft * 0.5h, wrapped);
            half lit = ramp * atten;
            half3 shade = lerp(_ShadeColor.rgb, half3(1, 1, 1), lit);
            half3 h = normalize(lightDir + viewDir);
            half spec = pow(saturate(dot(s.Normal, h)), 40.0h) * _Gloss * lit;
            half4 c;
            c.rgb = (s.Albedo * shade + spec) * _LightColor0.rgb * (1 - _Unlit);
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color * lerp(fixed4(1, 1, 1, 1), IN.color, _UseVColor);
            half rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Albedo = tex.rgb * (1 - _Unlit);
            o.Emission = _EmissionColor.rgb + tex.rgb * _Unlit + _RimColor.rgb * pow(rim, _RimPower) * _RimColor.a * (1 - _Unlit);
            o.Alpha = tex.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
