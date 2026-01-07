Shader "Custom/UI_PolarRingFrame"
{
Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0, 1, 0, 1)
        
        _InnerRadius ("Inner Radius", Range(0.0, 0.5)) = 0.3
        _OuterRadius ("Outer Radius", Range(0.0, 0.5)) = 0.4
        _AngleStart ("Start Angle", Range(0.0, 360.0)) = 0.0
        _AngleEnd ("End Angle", Range(0.0, 360.0)) = 90.0
        _OutlineWidth ("Outline Width", Range(0.0, 0.05)) = 0.005
        _EdgeSoftness ("Softness", Range(0.0, 0.01)) = 0.002
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

            float _InnerRadius, _OuterRadius, _AngleStart, _AngleEnd, _OutlineWidth, _EdgeSoftness;
            fixed4 _Color;

            v2f vert (appdata_full v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy - 0.5; 
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float d = length(i.uv);
                float ang = degrees(atan2(i.uv.y, i.uv.x));
                if (ang < 0) ang += 360.0;

                // 1. 计算到内外圆周边界的距离
                float distToInner = abs(d - _InnerRadius);
                float distToOuter = abs(d - _OuterRadius);
                float minDistToCircle = min(distToInner, distToOuter);

                // 2. 计算到起始/结束角度边界的距离
                // 将当前角度与边界角度的差值转为弧长距离，近似计算边界感
                float dAngStart = abs(ang - _AngleStart);
                if (dAngStart > 180) dAngStart = 360 - dAngStart;
                float distToStartEdge = radians(dAngStart) * d;

                float dAngEnd = abs(ang - _AngleEnd);
                if (dAngEnd > 180) dAngEnd = 360 - dAngEnd;
                float distToEndEdge = radians(dAngEnd) * d;

                float minDistToEdge = min(distToStartEdge, distToEndEdge);

                // 3. 确定是否在圆弧的范围内（用于裁剪非边界部分）
                float inRadius = (d >= _InnerRadius - _OutlineWidth && d <= _OuterRadius + _OutlineWidth);
                
                float inAngle = 0;
                if (_AngleStart < _AngleEnd)
                    inAngle = (ang >= _AngleStart - 5.0 && ang <= _AngleEnd + 5.0); // 略微放宽判断逻辑
                else
                    inAngle = (ang >= _AngleStart - 5.0 || ang <= _AngleEnd + 5.0);

                // 4. 组合描边逻辑
                // 在圆周边界上 且 在角度范围内
                float circleOutline = smoothstep(_OutlineWidth + _EdgeSoftness, _OutlineWidth, minDistToCircle);
                float isWithinAngleWindow = 0;
                if (_AngleStart < _AngleEnd) isWithinAngleWindow = (ang >= _AngleStart && ang <= _AngleEnd);
                else isWithinAngleWindow = (ang >= _AngleStart || ang <= _AngleEnd);
                
                // 在角度边界上 且 在内外半径范围内
                float edgeOutline = smoothstep(_OutlineWidth + _EdgeSoftness, _OutlineWidth, minDistToEdge);
                float isWithinRadiusWindow = (d >= _InnerRadius && d <= _OuterRadius);

                float finalOutline = (circleOutline * isWithinAngleWindow) + (edgeOutline * isWithinRadiusWindow);
                finalOutline = saturate(finalOutline);

                return fixed4(i.color.rgb, i.color.a * finalOutline);
            }
            ENDCG
        }
    }
}