Shader "HushBlocks/UI/DiegeticVoxelGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0.941, 1, 1)
        _EmissionIntensity ("Emission Multiplier", Range(0.0, 5.0)) = 1.8
        _VoxelGridSnap ("Voxel Grid Steps", Float) = 64.0
        _GlitchIntensity ("Glitch Amplitude", Range(0.0, 1.0)) = 0.0
        _ScanlineStrength ("Scanline Depth", Range(0.0, 1.0)) = 0.12
        _ScanlineFreq ("Scanline Frequency", Float) = 180.0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float4 _EmissionColor;
            float _EmissionIntensity;
            float _VoxelGridSnap;
            float _GlitchIntensity;
            float _ScanlineStrength;
            float _ScanlineFreq;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;

                // Subtle glitch vertex offset on high instability
                if (_GlitchIntensity > 0.01)
                {
                    float jitter = sin(_Time.y * 70.0 + v.vertex.y * 12.0) * _GlitchIntensity * 4.0;
                    if (fmod(floor(v.vertex.y * 0.1), 3.0) < 1.0)
                    {
                        v.vertex.x += jitter;
                    }
                }

                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Stepped Voxel Quantization
                if (_VoxelGridSnap > 4.0)
                {
                    uv = floor(uv * _VoxelGridSnap) / _VoxelGridSnap;
                }

                // Glitch line displacement
                if (_GlitchIntensity > 0.01)
                {
                    float lineNoise = sin(_Time.y * 50.0 + uv.y * 30.0);
                    if (lineNoise > 0.85)
                    {
                        uv.x += (sin(_Time.y * 90.0) * 0.04) * _GlitchIntensity;
                    }
                }

                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // Subtle scanline modulation
                if (_ScanlineStrength > 0.01)
                {
                    float scan = sin(IN.worldPosition.y * _ScanlineFreq + _Time.y * 8.0) * 0.5 + 0.5;
                    color.rgb *= (1.0 - _ScanlineStrength * (1.0 - scan));
                }

                // HDR emission radiance boost
                color.rgb += _EmissionColor.rgb * (_EmissionIntensity * color.a * 0.6);

                return color;
            }
            ENDCG
        }
    }
}

