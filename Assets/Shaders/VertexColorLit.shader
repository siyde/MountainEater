// Mountain Eater - URP uyumlu, köşe (vertex) rengi kullanan basit aydınlatmalı shader.
// Neden: URP/Lit vertex rengini göstermez. Dağın malzeme renkleri (Faz 1'de çimen/kaya/kar,
// Faz 5'te taş/bakır/altın/kristal) mesh köşe renklerinde taşınacak; böylece tüm dağ
// tek materyal ve az draw call ile çizilir.
// Destek: ana ışık + gölgeler, ortam ışığı (SH), sis, DepthOnly ve DepthNormals (SSAO) pass'leri.
Shader "MountainEater/VertexColorLit"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.1
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            half _AmbientStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Bölge görünümü (Faz 6). Global değerler RegionOverlayController tarafından ayarlanır:
            // _ME_RegionOverlay: 0 = normal görünüm, 1 = bölge yönetimi modu
            // _ME_RegionColors[id]: rgb = bölge rengi, a = vurgu gücü (seçili bölge daha güçlü)
            #define ME_MAX_REGIONS 64
            float4 _ME_RegionColors[ME_MAX_REGIONS];
            float _ME_RegionOverlay;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 regionData : TEXCOORD1; // x = bölge kimliği, y = sınır
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                nointerpolation float regionId : TEXCOORD3; // üçgen boyunca sabit: net bölge sınırları
                float regionBorder : TEXCOORD4;              // enterpolasyonlu: sınır boyunca çizgi
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.color = input.color * _BaseColor;
                output.regionId = input.regionData.x;
                output.regionBorder = input.regionData.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half attenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 diffuse = mainLight.color * (nDotL * attenuation);
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;

                // Parlaklık: malzeme başına köşe renginin alfa kanalından (maden), en az materyal değeri kadar.
                half smoothness = max(_Smoothness, input.color.a);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half specPower = exp2(10.0h * smoothness + 1.0h);
                half3 specular = mainLight.color * (pow(saturate(dot(normalWS, halfDir)), specPower) * smoothness * attenuation);

                half3 albedo = input.color.rgb;
                half3 overlayGlow = 0;
                if (_ME_RegionOverlay > 0.5)
                {
                    int id = clamp((int)(input.regionId + 0.5), 0, ME_MAX_REGIONS - 1);
                    half4 regionColor = _ME_RegionColors[id];
                    albedo = lerp(albedo, regionColor.rgb, 0.35 + 0.35 * regionColor.a);
                    half border = saturate(input.regionBorder * 2.0);
                    // Gölgede de okunabilsin diye hafif ışıma + sınır çizgisi.
                    overlayGlow = regionColor.rgb * (0.12 + 0.18 * regionColor.a) + border * half3(1.0, 1.0, 1.0) * 0.6;
                }

                half3 color = albedo * (diffuse + ambient) + specular + overlayGlow;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // URP gölge pass'i bu değişkenleri doldurur.
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag(float4 positionCS : SV_POSITION) : SV_Target
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(DepthNormalsVaryings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
