Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex      ("Sprite Texture", 2D)      = "white" {}
        _Color        ("Tint",           Color)   = (1,1,1,1)
        _OutlineColor ("Outline Color",  Color)   = (1,1,1,1)
        _OutlineSize  ("Outline Size",   Float)   = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "RenderPipeline"    = "UniversalPipeline"
            "IgnoreProjector"   = "True"
        }

        Cull   Off
        ZWrite Off
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpriteOutline"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4  _Color;
                half4  _OutlineColor;
                float  _OutlineSize;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // Already opaque — just return the sprite pixel
                if (sprite.a > 0.01)
                    return sprite;

                // Sample 8 neighbours; if any are opaque, draw the outline colour
                float2 offset = _MainTex_TexelSize.xy * _OutlineSize;

                half maxA = 0;
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( offset.x,  0       )).a);
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x,  0       )).a);
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,        offset.y )).a);
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( 0,       -offset.y )).a);
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( offset.x,  offset.y)).a);
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x,  offset.y)).a);
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( offset.x, -offset.y)).a);
                maxA = max(maxA, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-offset.x, -offset.y)).a);

                if (maxA > 0.01)
                    return half4(_OutlineColor.rgb, _OutlineColor.a);

                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
