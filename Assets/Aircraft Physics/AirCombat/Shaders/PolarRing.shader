Shader "Custom/PolarRing"
{
Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _InnerRadius ("Inner Radius", Range(0.0, 0.5)) = 0.3
        _OuterRadius ("Outer Radius", Range(0.0, 0.5)) = 0.5
        _AngleStart ("Start Angle", Range(0.0, 360.0)) = 180.0
        _AngleEnd ("End Angle", Range(0.0, 360.0)) = 360.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off Blend SrcAlpha OneMinusSrcAlpha

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

            float _InnerRadius, _OuterRadius, _AngleStart, _AngleEnd;
            fixed4 _Color;

            v2f vert (appdata_full v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy - 0.5; // 将UV原点移到中心
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. 计算当前像素到中心的距离
                float dist = length(i.uv);
                
                // 2. 计算极坐标角度 (0-360)
                float ang = degrees(atan2(i.uv.y, i.uv.x));
                if (ang < 0) ang += 360.0;

                // 3. 距离检测 (内径与外径之间)
                // 使用 smoothstep 稍微平滑一下边缘，防止锯齿
                float circleMask = smoothstep(_InnerRadius - 0.005, _InnerRadius, dist) 
                                 - smoothstep(_OuterRadius, _OuterRadius + 0.005, dist);

                // 4. 角度检测 (处理跨越 360 度的逻辑)
                float angleMask = 0;
                if (_AngleStart < _AngleEnd) {
                    angleMask = (ang >= _AngleStart && ang <= _AngleEnd) ? 1.0 : 0.0;
                } else {
                    // 如果起始角大于结束角说明跨越了 0 点
                    angleMask = (ang >= _AngleStart || ang <= _AngleEnd) ? 1.0 : 0.0;
                }

                float finalAlpha = circleMask * angleMask * i.color.a;
                return fixed4(i.color.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}