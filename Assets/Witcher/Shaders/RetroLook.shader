Shader "Witcher/RetroLook"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _PixelSize ("Pixel Size", Float) = 4
        _ColorSteps ("Color Steps", Float) = 12
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.35
        _Contrast ("Contrast", Float) = 1.15
        _Saturation ("Saturation", Float) = 0.85
        _Vignette ("Vignette", Range(0, 1)) = 0.45
        _Tint ("Tint", Color) = (0.85, 0.9, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            Name "RetroLook"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;
            float _ColorSteps;
            float _DitherStrength;
            float _Contrast;
            float _Saturation;
            float _Vignette;
            float4 _Tint;

            // 4x4 Bayer matrix
            static const float Bayer[16] = {
                0,  8,  2, 10,
                12, 4, 14, 6,
                3, 11, 1,  9,
                15, 7, 13, 5
            };

            float3 ApplySaturation(float3 c, float sat)
            {
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                return lerp(luma.xxx, c, sat);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 pixelUV = uv;

                // Chunky pixels
                float2 res = _ScreenParams.xy / max(_PixelSize, 1.0);
                pixelUV = floor(uv * res) / res;

                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, pixelUV);

                // Mild contrast + tint
                float3 rgb = (col.rgb - 0.5) * _Contrast + 0.5;
                rgb *= _Tint.rgb;
                rgb = ApplySaturation(rgb, _Saturation);

                // Ordered dither before posterize
                float2 pixelCoord = floor(uv * _ScreenParams.xy / max(_PixelSize, 1.0));
                int bx = (int)pixelCoord.x & 3;
                int by = (int)pixelCoord.y & 3;
                float threshold = (Bayer[by * 4 + bx] / 16.0) - 0.5;
                rgb += threshold * _DitherStrength / max(_ColorSteps, 1.0);

                // Posterize / color banding (retro sky look)
                float steps = max(_ColorSteps, 2.0);
                rgb = floor(rgb * steps + 0.5) / steps;

                // Vignette
                float2 vc = uv * 2.0 - 1.0;
                float vig = saturate(1.0 - dot(vc, vc) * _Vignette);
                rgb *= vig;

                return float4(saturate(rgb), 1);
            }
            ENDHLSL
        }
    }
}
