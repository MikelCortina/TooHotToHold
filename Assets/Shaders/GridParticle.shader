Shader "Instanced/URPGridTestParticleShader" {
    Properties {
        _BaseColor("Color", Color) = (0.25, 0.5, 0.5, 1)
        _DensityRange ("Density Range", Range(0.001, 500000)) = 1.0
        _size("Particle Size", Float) = 0.1
    }
    SubShader {
        // Etiquetas específicas de URP
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // En URP usamos HLSLPROGRAM en lugar de CGPROGRAM
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma target 4.5

            // Librería central de URP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float _size;
            float4 _BaseColor;
            float _DensityRange;

           struct Particle {
    float pressure;
    float density;
    float3 currentForce;
    float3 position; // <- Ahora coincide con C#
    float3 velocity; // <- Ahora coincide con C#
};

            #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                StructuredBuffer<Particle> _particlesBuffer;
            #endif

            void setup() {
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    float3 pos = _particlesBuffer[unity_InstanceID].position;
                    float s = max(_size, 0.0001); // Evitar división por cero

                    // Construcción de matriz de transformación
                    unity_ObjectToWorld = float4x4(
                        s, 0, 0, pos.x,
                        0, s, 0, pos.y,
                        0, 0, s, pos.z,
                        0, 0, 0, 1
                    );
                    
                    unity_WorldToObject = float4x4(
                        1.0/s, 0,     0,     -pos.x/s,
                        0,     1.0/s, 0,     -pos.y/s,
                        0,     0,     1.0/s, -pos.z/s,
                        0,     0,     0,     1
                    );
                #endif
            }

            Varyings vert(Attributes input) {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Transformaciones de posición en URP
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);

                // Lógica de color basada en la presión
                #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
                    float dens = _particlesBuffer[unity_InstanceID].pressure;
                    output.color = float4(dens / _DensityRange, 0, 0, 1);
                #else
                    output.color = _BaseColor;
                #endif

                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.color;
            }
            ENDHLSL
        }
    }
}