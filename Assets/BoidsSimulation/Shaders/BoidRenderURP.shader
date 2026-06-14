// BoidRenderURP.shader
// GPU上のBoidsバッファを頂点シェーダーから直接読み、
// DrawMeshInstancedIndirectで大量インスタンス描画するためのURPシェーダー。
// 各インスタンスは自分の速度方向を向く（簡易ライティング付き）。
Shader "Boids/BoidRenderURP"
{
    Properties
    {
        _ColorSlow ("Color (低速)", Color) = (0.1, 0.4, 0.9, 1)
        _ColorFast ("Color (高速)", Color) = (0.9, 0.95, 1.0, 1)
        _SpeedRange ("色変化の基準速度", Float) = 8.0
        _Scale ("個体スケール", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // StructuredBufferを頂点シェーダーで読むため SM4.5 以上が必要
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // C#/ComputeShaderと同一レイアウトのBoid構造体
            struct Boid
            {
                float3 position;
                float3 velocity;
            };

            StructuredBuffer<Boid> boidsBuffer;

            float4 _ColorSlow;
            float4 _ColorFast;
            float  _SpeedRange;
            float  _Scale;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float  speed01     : TEXCOORD1;
            };

            // 速度方向を向くための正規直交基底を作る
            void BuildBasis(float3 velocity, out float3 right, out float3 up, out float3 fwd)
            {
                float speed = length(velocity);
                fwd = speed > 0.0001 ? velocity / speed : float3(0, 0, 1);
                // forwardがほぼ真上/真下のときは別の参照軸を使う
                float3 refUp = abs(fwd.y) > 0.99 ? float3(0, 0, 1) : float3(0, 1, 0);
                right = normalize(cross(refUp, fwd));
                up = cross(fwd, right);
            }

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Boid boid = boidsBuffer[instanceID];

                float3 right, up, fwd;
                BuildBasis(boid.velocity, right, up, fwd);

                // オブジェクト空間の頂点を基底で回転＋スケール＋平行移動してワールド配置
                float3 v = IN.positionOS.xyz * _Scale;
                float3 worldPos = boid.position + right * v.x + up * v.y + fwd * v.z;

                // 法線も同じ基底で回転
                float3 n = IN.normalOS;
                float3 worldNormal = right * n.x + up * n.y + fwd * n.z;

                Varyings OUT;
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.normalWS = worldNormal;
                OUT.speed01 = saturate(length(boid.velocity) / max(_SpeedRange, 0.0001));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // メインライト方向で簡易ランバート
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                float lighting = 0.3 + 0.7 * ndotl; // 環境光込み

                // 速度で色を補間（速いほど明るく）
                float4 col = lerp(_ColorSlow, _ColorFast, IN.speed01);
                return half4(col.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
