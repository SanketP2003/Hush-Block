Shader "Shader Graphs/SH_AcousticWaveRing"
{
    Properties
    {
        [HDR] _RingColor("Ring Color (HDR)", Color) = (0.0, 15.056, 16.0, 1.0) // #00F0FF +4.0 EV
        _ExpansionProgress("Expansion Progress", Range(0.0, 1.0)) = 0.0
        _EdgeSharpness("Edge Sharpness", Float) = 12.0
        _RingRadius("Ring Radius", Range(0.1, 0.5)) = 0.42
        _RingThickness("Ring Thickness", Range(0.01, 0.2)) = 0.05
        _NoiseIntensity("Noise Distortion Intensity", Range(0.0, 0.1)) = 0.025
        _NoiseScale("Noise Scale", Float) = 16.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UnlitAdditive"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _RingColor;
                half   _ExpansionProgress;
                half   _EdgeSharpness;
                half   _RingRadius;
                half   _RingThickness;
                half   _NoiseIntensity;
                half   _NoiseScale;
            CBUFFER_END

            // Fast procedural hash for procedural audio noise
            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            // Procedural Voronoi cellular noise for stepped voxel audio interference
            float Voronoi2D(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float minDist = 1.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 p = g + lattice;
                        float2 offset = float2(Hash21(p), Hash21(p + 19.19));
                        float2 diff = lattice + offset - f;
                        minDist = min(minDist, length(diff));
                    }
                }
                return minDist;
            }

            Varyings UnlitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 UnlitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 centerUV = input.uv - float2(0.5, 0.5);
                float radialDist = length(centerUV);

                // High-frequency procedural audio distortion along ring perimeter
                float angle = atan2(centerUV.y, centerUV.x);
                float audioClipHarmonics = sin(angle * 12.0 + _Time.y * 24.0) * 0.5 + 0.5;
                float cellularNoise = Voronoi2D(input.uv * _NoiseScale);
                float distortion = (audioClipHarmonics * 0.4 + cellularNoise * 0.6) * _NoiseIntensity * (1.0 - _ExpansionProgress);

                float distortedDist = radialDist + distortion;

                // Crisp concentric ring band-pass with zero blurry gradients
                float halfThickness = _RingThickness * 0.5;
                float innerEdge = _RingRadius - halfThickness;
                float outerEdge = _RingRadius + halfThickness;
                float feather = 1.0 / max(1.0, _EdgeSharpness);

                float innerMask = smoothstep(innerEdge - feather, innerEdge, distortedDist);
                float outerMask = 1.0 - smoothstep(outerEdge, outerEdge + feather, distortedDist);
                float ringBand = saturate(innerMask * outerMask);

                // Non-linear acoustic dispersion fade: (1.0 - _ExpansionProgress)^2
                float expansionInv = saturate(1.0 - _ExpansionProgress);
                float dispersionFade = expansionInv * expansionInv;

                // Additive output: Premultiplied HDR color
                half3 emissionColor = _RingColor.rgb * (ringBand * dispersionFade);

                return half4(emissionColor, ringBand * dispersionFade);
            }
            ENDHLSL
        }
    }
    FallBack Off
}

