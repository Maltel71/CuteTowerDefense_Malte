Shader "Custom/URPSilhouetteToonShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1.0, 0.0, 0.0, 1.0)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.01
        _ShadowThreshold ("Shadow Threshold", Range(0.0, 1.0)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.0, 0.5)) = 0.1
        _DarkColor ("Dark Color", Color) = (0.1, 0.15, 0.25, 1.0)
        _MidColor ("Mid Color", Color) = (0.3, 0.4, 0.5, 1.0)
        _LightColor ("Light Color", Color) = (0.5, 0.6, 0.7, 1.0)
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _ShadowThreshold;
            float _ShadowSoftness;
            float4 _DarkColor;
            float4 _MidColor;
            float4 _LightColor;
        CBUFFER_END
        
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        struct Attributes
        {
            float4 positionOS   : POSITION;
            float3 normalOS     : NORMAL;
            float2 uv           : TEXCOORD0;
        };
        
        struct Varyings
        {
            float4 positionCS   : SV_POSITION;
            float2 uv           : TEXCOORD0;
            float3 normalWS     : NORMAL;
            float3 positionWS   : TEXCOORD1;
        };
        ENDHLSL
        
        // Main Pass - regular toon shading
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample texture
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Light calculation
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 normalWS = normalize(input.normalWS);
                float NdotL = dot(normalWS, mainLight.direction);
                
                // Apply shadows
                float shadowAttenuation = mainLight.shadowAttenuation;
                NdotL = NdotL * shadowAttenuation;
                
                // Create stepped lighting for toon effect
                float lightIntensity = smoothstep(_ShadowThreshold - _ShadowSoftness, 
                                                _ShadowThreshold + _ShadowSoftness, 
                                                NdotL);
                
                // Apply lighting with color bands for toon effect
                float3 finalColor;
                if (lightIntensity < 0.33) {
                    finalColor = _DarkColor.rgb;
                } else if (lightIntensity < 0.66) {
                    finalColor = _MidColor.rgb;
                } else {
                    finalColor = _LightColor.rgb;
                }
                
                // Optional: Multiply by texture color
                finalColor *= texColor.rgb;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // Silhouette Outline Pass - rendered after the main pass
        Pass
        {
            Name "Outline"
            Tags { }
            
            Cull Front // Cull front faces to draw only back faces
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            struct OutlineAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };
            
            struct OutlineVaryings
            {
                float4 positionCS   : SV_POSITION;
            };
            
            OutlineVaryings vert(OutlineAttributes input)
            {
                OutlineVaryings output;
                
                // Transform normal to clip space and normalize the xy components
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                
                float3 normalWS = normalInputs.normalWS;
                float3 normalCS = TransformWorldToHClipDir(normalWS);
                normalCS.xy = normalize(normalCS.xy);
                
                // Get position in clip space
                float4 positionCS = positionInputs.positionCS;
                
                // Calculate the amount to extrude based on clip space position
                // This keeps outline width consistent in screen space
                float2 offset = normalCS.xy * _OutlineWidth * positionCS.w;
                
                // Apply offset to create outline
                positionCS.xy += offset;
                output.positionCS = positionCS;
                
                return output;
            }
            
            half4 frag(OutlineVaryings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
        
        // Shadow casting support
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            float3 _LightDirection;
            
            struct ShadowAttributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
            };
            
            struct ShadowVaryings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };
            
            ShadowVaryings ShadowPassVertex(ShadowAttributes input)
            {
                ShadowVaryings output;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                
                output.positionCS = positionCS;
                output.uv = input.texcoord;
                
                return output;
            }
            
            half4 ShadowPassFragment(ShadowVaryings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
        
        // DepthOnly pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct DepthOnlyAttributes
            {
                float4 position     : POSITION;
                float2 texcoord     : TEXCOORD0;
            };
            
            struct DepthOnlyVaryings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };
            
            DepthOnlyVaryings DepthOnlyVertex(DepthOnlyAttributes input)
            {
                DepthOnlyVaryings output;
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                output.uv = input.texcoord;
                return output;
            }
            
            half4 DepthOnlyFragment(DepthOnlyVaryings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
}