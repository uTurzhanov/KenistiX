Shader "Custom/UIChromaKey"
{
    // Вырезает однотонный фон (зелёный/синий экран и т.п.) из видео прямо во время рендера,
    // без пересборки самого видеофайла. Используется как материал на RawImage.
    //
    // MP4 физически не хранит альфа-канал - это единственный практичный способ "убрать фон"
    // у уже готового видео без его пересъёмки/пересведения.
    //
    // ВАЖНО: включена поддержка стерео-рендеринга (Single Pass Instanced) для VR - без неё
    // шейдер рисуется только в одном "глазу" (обычно правый/второй остаётся пустым), потому что
    // стандартные UI-шейдеры Unity это умеют "из коробки", а кастомные HLSL-шейдеры - только если
    // явно добавить нужные макросы (см. #pragma multi_compile_instancing и UNITY_*STEREO* ниже).

    Properties
    {
        _MainTex ("Texture (сюда Render Texture из VideoPlayer)", 2D) = "white" {}
        _KeyColor ("Key Color (цвет фона, который вырезаем)", Color) = (0, 1, 0, 1)
        _Threshold ("Threshold (насколько близко к KeyColor считать фоном)", Range(0, 1)) = 0.35
        _Smoothness ("Edge Smoothness (мягкость края)", Range(0, 1)) = 0.08
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

            float4 _KeyColor;
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

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Расстояние текущего пикселя от заданного цвета фона в RGB-пространстве.
                // Чем ближе к _KeyColor - тем прозрачнее становится пиксель.
                float dist = distance(texColor.rgb, _KeyColor.rgb);
                float alpha = smoothstep(_Threshold, _Threshold + _Smoothness, dist);

                texColor.a *= alpha;
                return texColor * IN.color;
            }
            ENDHLSL
        }
    }
}
