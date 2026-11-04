Shader "Shader Graphs/SH_HushBlock_Resonance"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Felt/Charcoal Diffuse)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color (Charcoal)", Color) = (0.11, 0.118, 0.141, 1.0)
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.15
        _StateBlend("State Blend (0=Cyan, 1=Amber)", Range(0.0, 1.0)) = 0.0
        [HDR] _CyanColor("Cyan Color (HDR)", Color) = (0.0, 10.64, 11.31, 1.0)
        [HDR] _AmberColor("Amber Color (HDR)", Color) = (4.0, 2.808, 0.0, 1.0)
        _JitterIntensity("Jitter Intensity", Range(0.0, 0.1)) = 0.035
        _CrystallizationProgress("Crystallization Progress", Range(0.0, 1.0)) = 0.0

        [Header(Procedural Tuning)]
        _JitterSpeed("Jitter Speed", Float) = 35.0
        _PulseSpeed("Pulse Speed", Float) = 24.0
        _CrystallizationScale("Crystallization Scale", Float) = 20.0
        _FlareIntensity("Flare Intensity Spike", Float) = 10.0
        _FlareWidth("Flare Width", Range(0.01, 0.2)) = 0.04
        _SeamWidth("Seam Width", Range(0.01, 0.4)) = 0.14
        _SlitWidth("Indicator Slit Width", Range(0.01, 0.3)) = 0.08
        _FresnelPower("Fresnel Power", Range(0.5, 8.0)) = 3.5
        _MortarColor("Mortar Color", Color) = (0.055, 0.063, 0.082, 1.0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float3 viewDirWS    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _CyanColor;
                half4  _AmberColor;
                half4  _MortarColor;
                half   _Smoothness;
                half   _StateBlend;
                half   _JitterIntensity;
                half   _CrystallizationProgress;
                float  _JitterSpeed;
                float  _PulseSpeed;
                float  _CrystallizationScale;
                half   _FlareIntensity;
                half   _FlareWidth;
                half   _SeamWidth;
                half   _SlitWidth;
                half   _FresnelPower;
            CBUFFER_END

            // Fast procedural hash for noise
            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            // 3D Procedural Gradient/Value Noise
            float Noise3D(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), f.x),
                                 lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                                 lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            // 2D Procedural Voronoi (Cellular F1 Distance)
            float Voronoi2D(float2 uv, float angleOffset)
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
                        // Pseudo-random offset based on sine hash
                        float2 p = g + lattice;
                        float2 rand = frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453);
                        float2 offset = 0.5 + 0.5 * sin(angleOffset + 6.2831 * rand);
                        float2 diff = lattice + offset - f;
                        float d = length(diff);
                        minDist = min(minDist, d);
                    }
                }
                return minDist;
            }

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // --- Vertex Stage: Acoustic Resonance Micro-Jitter ---
                // Noise sampling driven by high-frequency speed and object coordinates
                float timeOffset = _Time.y * _JitterSpeed;
                float3 noiseCoord = input.positionOS.xyz * 12.0 + float3(timeOffset, timeOffset * 0.77, timeOffset * 1.23);
                float rawNoise = Noise3D(noiseCoord) * 2.0 - 1.0;

                // Mask with (1.0 - _StateBlend) so displacement shuts off when stabilized
                half jitterMask = (1.0 - _StateBlend);
                float3 jitterOffset = input.normalOS * (rawNoise * _JitterIntensity * jitterMask);

                float3 displacedPosOS = input.positionOS.xyz + jitterOffset;

                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedPosOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // --- Stylized Felt Base Diffuse ---
                half4 albedoMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 diffuseBase = albedoMap * _BaseColor;

                // --- Fragment Stage B: Window & Core Acoustic Indicator Slits ---
                float2 uvDistFromCenter = abs(uv - 0.5);
                // Slits: narrow horizontal and vertical acoustic channels
                half slitMaskH = step(uvDistFromCenter.y, _SlitWidth * 0.5) * step(uvDistFromCenter.x, 0.42);
                half slitMaskV = step(uvDistFromCenter.x, _SlitWidth * 0.5) * step(uvDistFromCenter.y, 0.42);
                half indicatorSlits = max(slitMaskH, slitMaskV);

                // Central pane window
                half coreWindow = step(max(uvDistFromCenter.x, uvDistFromCenter.y), 0.22);
                half coreFeatureMask = max(indicatorSlits, coreWindow);

                // Stepped Fresnel for faceted voxel edge highlights
                half fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                half steppedFresnel = step(0.62, pow(fresnel, _FresnelPower));
                half featureMask = saturate(coreFeatureMask + steppedFresnel * 0.7);

                // Dual-State Color Modulation
                // 1) Chaotic Cyan: modulated by high-tempo sine pulse and micro audio distortion
                float pulseWave = sin(_Time.y * _PulseSpeed) * 0.35 + 0.95;
                float audioClipNoise = Noise3D(float3(uv * 20.0, _Time.y * 50.0)) * 0.3 + 0.85;
                half4 modulatedCyan = _CyanColor * (pulseWave * audioClipNoise);

                // 2) Muted Amber: steady, deep, weighted calm glow
                float amberCalmBreath = sin(_Time.y * 1.5) * 0.04 + 0.98;
                half4 modulatedAmber = _AmberColor * amberCalmBreath;

                // Interpolate state
                half4 coreColor = lerp(modulatedCyan, modulatedAmber, _StateBlend);
                half3 coreEmission = coreColor.rgb * featureMask;

                // --- Fragment Stage C: Seam Crystallization (Velvet Mason Mortar) ---
                // Outer perimeter bevel/edge proximity: 0 at center, 1 at seams
                float seamProximity = max(uvDistFromCenter.x, uvDistFromCenter.y) * 2.0;
                half seamRegion = smoothstep(1.0 - _SeamWidth, 1.0, seamProximity);

                // Voronoi faceted acoustic crystal structure
                float voronoi = Voronoi2D(uv * _CrystallizationScale, 2.0);
                float crystalStructure = seamRegion * (1.0 - voronoi * 0.65);

                // Sharp faceted crystal mask driven by _CrystallizationProgress
                half crystalMask = step(1.0 - _CrystallizationProgress, crystalStructure) * seamRegion;

                // Leading edge flare (+10.0 emission spike)
                float edgeDelta = abs(crystalStructure - (1.0 - _CrystallizationProgress));
                half flareMask = (1.0 - saturate(edgeDelta / _FlareWidth)) * seamRegion;
                // Only flare while actively transitioning between 0.01 and 0.99
                half transitionActiveGate = smoothstep(0.01, 0.08, _CrystallizationProgress) * 
                                            smoothstep(0.01, 0.08, 1.0 - _CrystallizationProgress);
                flareMask *= transitionActiveGate;

                half4 flareColor = lerp(_CyanColor, _AmberColor, _StateBlend);
                half3 flareEmission = flareColor.rgb * (flareMask * _FlareIntensity);

                // Total combined emission
                half3 finalEmission = coreEmission + flareEmission;

                // Blend Albedo and Smoothness for crystallized velvet mortar seams
                half3 surfaceAlbedo = lerp(diffuseBase.rgb, _MortarColor.rgb, crystalMask);
                half surfaceSmoothness = lerp(_Smoothness, 0.78, crystalMask);

                // --- Lighting Evaluation via Universal Light ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = surfaceAlbedo;
                surfaceData.metallic = 0.0;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = surfaceSmoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.emission = finalEmission;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

                half4 litColor = UniversalFragmentPBR(inputData, surfaceData);
                return litColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _CyanColor;
                half4  _AmberColor;
                half4  _MortarColor;
                half   _Smoothness;
                half   _StateBlend;
                half   _JitterIntensity;
                half   _CrystallizationProgress;
                float  _JitterSpeed;
                float  _PulseSpeed;
                float  _CrystallizationScale;
                half   _FlareIntensity;
                half   _FlareWidth;
                half   _SeamWidth;
                half   _SlitWidth;
                half   _FresnelPower;
            CBUFFER_END

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3D(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), f.x),
                                 lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                                 lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float timeOffset = _Time.y * _JitterSpeed;
                float3 noiseCoord = input.positionOS.xyz * 12.0 + float3(timeOffset, timeOffset * 0.77, timeOffset * 1.23);
                float rawNoise = Noise3D(noiseCoord) * 2.0 - 1.0;
                half jitterMask = (1.0 - _StateBlend);
                float3 jitterOffset = input.normalOS * (rawNoise * _JitterIntensity * jitterMask);

                float3 displacedPosOS = input.positionOS.xyz + jitterOffset;
                float3 positionWS = TransformObjectToWorld(displacedPosOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _CyanColor;
                half4  _AmberColor;
                half4  _MortarColor;
                half   _Smoothness;
                half   _StateBlend;
                half   _JitterIntensity;
                half   _CrystallizationProgress;
                float  _JitterSpeed;
                float  _PulseSpeed;
                float  _CrystallizationScale;
                half   _FlareIntensity;
                half   _FlareWidth;
                half   _SeamWidth;
                half   _SlitWidth;
                half   _FresnelPower;
            CBUFFER_END

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3D(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(lerp(Hash(i + float3(0,0,0)), Hash(i + float3(1,0,0)), f.x),
                                 lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x),
                                 lerp(Hash(i + float3(0,1,1)), Hash(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float timeOffset = _Time.y * _JitterSpeed;
                float3 noiseCoord = input.positionOS.xyz * 12.0 + float3(timeOffset, timeOffset * 0.77, timeOffset * 1.23);
                float rawNoise = Noise3D(noiseCoord) * 2.0 - 1.0;
                half jitterMask = (1.0 - _StateBlend);
                float3 jitterOffset = input.normalOS * (rawNoise * _JitterIntensity * jitterMask);

                float3 displacedPosOS = input.positionOS.xyz + jitterOffset;
                output.positionCS = TransformObjectToHClip(displacedPosOS);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}