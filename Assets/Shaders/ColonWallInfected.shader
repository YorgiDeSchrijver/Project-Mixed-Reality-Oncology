// Darmwand met ziekte die zich lokaal vanuit de poliepen verspreidt.
// De wand zelf blijft gezond (glanzend, eigen textuur). Rond elke "haard" (max. 4, gezet door ColonDisease):
//  - een ontstoken, rode, nattere rand met donkere adertjes,
//  - een necrotische, matte, donkerbruine kern (alleen bij hogere stadia),
//  - een rode gloed die meeklopt met de hartslag.
// De vlekken krijgen een onregelmatige rand via procedurele noise: geen extra textures nodig.
Shader "Oncologie/ColonWallInfected"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness (glans)", Range(0,1)) = 0.6
        [ToggleOff] _EnvironmentReflections("Omgevingsreflecties (skybox)", Float) = 0
        _InflamedColor("Ontstoken kleur (vermenigvuldigd)", Color) = (0.85, 0.3, 0.32, 1)
        _VeinColor("Aders", Color) = (0.35, 0.03, 0.06, 1)
        _NecroticColor("Necrose", Color) = (0.16, 0.1, 0.08, 1)
        [HDR] _GlowColor("Gloed", Color) = (0.9, 0.03, 0.05, 1)
        _NoiseScale("Noise schaal", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half4 _InflamedColor;
            half4 _VeinColor;
            half4 _NecroticColor;
            half4 _GlowColor;
            float _NoiseScale;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            // In een darm is er geen lucht om te weerspiegelen: standaard uit (ook goedkoper op de Quest)
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            // Globaal gezet door ColonDisease (buiten de material-CBUFFER, dus SRP Batcher blijft werken)
            float4 _ColonInfectPos[4];   // xyz = wereldpositie, w = straal (m)
            float4 _ColonInfectData[4];  // x = ernst 0..1 (stadium), y = opflakkering 0..1
            float  _ColonHeartbeat;      // 0..1 piek per hartslag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(hash13(i + float3(0,0,0)), hash13(i + float3(1,0,0)), f.x),
                                 lerp(hash13(i + float3(0,1,0)), hash13(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(hash13(i + float3(0,0,1)), hash13(i + float3(1,0,1)), f.x),
                                 lerp(hash13(i + float3(0,1,1)), hash13(i + float3(1,1,1)), f.x), f.y), f.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half smooth = _Smoothness;
                half3 emission = 0;

                float3 pw = i.positionWS * _NoiseScale;
                float n1 = noise3(pw);                       // grove vorm van de vlek
                float n2 = noise3(pw * 2.7 + 17.0);          // fijnere structuur / aders

                half inflamed = 0, necrotic = 0, glow = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float4 hp = _ColonInfectPos[k];
                    float4 hd = _ColonInfectData[k];
                    if (hp.w <= 0.001) continue;
                    float d = distance(i.positionWS, hp.xyz);
                    float edge = hp.w * (0.65 + 0.7 * n1);                 // onregelmatige rand
                    float x = saturate(1.0 - d / edge);                    // 0 buiten, 1 in het midden
                    float sev = hd.x;
                    half inf = smoothstep(0.0, 0.3, x) * saturate(0.35 + sev);
                    half nec = smoothstep(0.5, 0.8, x + (n2 - 0.5) * 0.35) * saturate(sev * 1.6 - 0.6);
                    inflamed = max(inflamed, inf);
                    necrotic = max(necrotic, nec);
                    glow = max(glow, inf * (1.0 - nec) * (0.02 + sev * 0.06 + hd.y * _ColonHeartbeat * (0.2 + 0.4 * sev)));
                }

                // ontsteking: roder, donkerder, met adertjes, iets natter
                half vein = 1.0 - smoothstep(0.0, 0.05, abs(n2 - 0.5));
                albedo = lerp(albedo, albedo * _InflamedColor.rgb, inflamed);
                albedo = lerp(albedo, _VeinColor.rgb, vein * inflamed * 0.8);
                smooth = lerp(smooth, min(1.0, smooth + 0.12), inflamed);

                // necrose: donker, gevlekt, mat
                half3 nec = _NecroticColor.rgb * (0.6 + 0.8 * n2);
                albedo = lerp(albedo, nec, necrotic);
                smooth = lerp(smooth, 0.12, necrotic);

                emission = _GlowColor.rgb * glow;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalize(i.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.metallic = 0;
                s.specular = 0;
                s.smoothness = smooth;
                s.occlusion = 1;
                s.emission = emission;
                s.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }
            half frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
