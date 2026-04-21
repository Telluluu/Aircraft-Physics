Shader "Custom/RadarLockon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 1, 0, 1) // 锁定框主色
        
        [Header(Ring Settings)]
        _Radius ("Ring Radius", Range(0.0, 0.5)) = 0.4
        _RingThickness ("Ring Thickness", Range(0.0, 0.05)) = 0.01
        _RingAlpha ("Ring Alpha", Range(0.0, 1.0)) = 1.0
        
        [Header(Noise Settings)]
        _NoiseDensity ("Noise Density", Range(1.0, 50.0)) = 25.0
        _NoiseOpacity ("Noise Opacity", Range(0.0, 1.0)) = 0.5
        _Dissipation ("Dissipation Speed", Range(0.0, 10.0)) = 2.0
        _EdgeSoftness ("Edge Softness", Range(0.0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off Blend SrcAlpha One // 使用线性减淡/开启叠加感
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float _Radius, _RingThickness, _RingAlpha, _NoiseDensity, _NoiseOpacity, _Dissipation, _EdgeSoftness;
            fixed4 _Color;

            // 伪随机函数
            float hash(float2 p) {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            v2f vert (appdata_full v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy - 0.5; // 原点移至中心
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float d = length(i.uv);
                
                // 1. 圆框逻辑
                float halfThick = _RingThickness * 0.5;
                float ringMask = smoothstep(_Radius - halfThick - _EdgeSoftness, _Radius - halfThick, d) 
                               - smoothstep(_Radius + halfThick, _Radius + halfThick + _EdgeSoftness, d);
                
                // 2. 弥散噪点逻辑
                // 极坐标转换
                float angle = atan2(i.uv.y, i.uv.x);
                float2 polarUV = float2(d * _NoiseDensity, angle * (_NoiseDensity / 6.28));
                
                // 基于时间的随机噪点
                float n = hash(floor(polarUV) + floor(_Time.y * _Dissipation));
                
                // 让噪点从中心向外侧衰减：中心亮，外侧暗
                float dissipationMask = saturate(1.0 - (d / _Radius));
                float noiseAlpha = n * _NoiseOpacity * dissipationMask;

                // 3. 合并
                // 圆框部分不受噪点衰减影响，噪点在圆框内部弥散
                float finalAlpha = (ringMask * _RingAlpha) + noiseAlpha;
                
                // 裁剪掉圆框以外的噪点，只保留圆框内部的雾状感
                finalAlpha *= step(d, _Radius + _RingThickness);

                return fixed4(i.color.rgb, finalAlpha * i.color.a);
            }
            ENDCG
        }
    }
}