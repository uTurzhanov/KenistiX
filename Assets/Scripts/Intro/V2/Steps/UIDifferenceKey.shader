Shader "Custom/UIDifferenceKey"
{
    // Альтернатива UIChromaKey.shader для случаев, когда фон и объект в кадре ОДНОГО цвета
    // (например, чёрные контроллеры на чёрном фоне) - обычный хромакей по одному заданному цвету
    // тут бессилен в принципе, т.к. не может отличить "фон" от "объект того же цвета".
    //
    // Вместо сравнения с одним цветом - сравниваем каждый пиксель видео с СООТВЕТСТВУЮЩИМ пикселем
    // эталонного кадра фона (_BackgroundTex, "чистая пластина" - тот же ракурс, но без объекта в
    // кадре). Там, где разница минимальна - это фон (прозрачно), где есть заметная разница - это
    // объект (виден). Работает лучше на монохромных сценах, т.к. ловит даже небольшие отличия
    // (тени, блики, шум сжатия), а не требует точного совпадения с одним конкретным цветом.
    //
    // ВАЖНО: если фон и объект не просто похожи, а АБСОЛЮТНО идентичны пиксель-в-пиксель во всех
    // условиях (что на практике почти никогда не бывает из-за теней/бликов/шума) - никакая техника
    // ключевания по цвету/разнице принципиально не сработает, тут поможет только ручной ротоскопинг
    // или AI-инструмент удаления фона.

    Properties
    {
        _MainTex ("Video Texture (сюда Render Texture из VideoPlayer)", 2D) = "white" {}
        _BackgroundTex ("Background Reference (чистый кадр фона БЕЗ объекта)", 2D) = "black" {}
        _Threshold ("Threshold (порог отличия от фона, чтобы считать объектом)", Range(0, 1)) = 0.08
        _Smoothness ("Edge Smoothness (мягкость края)", Range(0, 1)) = 0.05
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BackgroundTex);
            SAMPLER(sampler_BackgroundTex);

            float _Threshold;
            float _Smoothness;
            float4 _Color;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 videoColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 bgColor = SAMPLE_TEXTURE2D(_BackgroundTex, sampler_BackgroundTex, IN.uv);

                // Разница текущего пикселя видео от того же пикселя эталонного фона.
                // Чем больше разница - тем более "это объект", тем непрозрачнее пиксель.
                float diff = distance(videoColor.rgb, bgColor.rgb);
                float alpha = smoothstep(_Threshold, _Threshold + _Smoothness, diff);

                videoColor.a *= alpha;
                return videoColor * IN.color;
            }
            ENDHLSL
        }
    }
}
