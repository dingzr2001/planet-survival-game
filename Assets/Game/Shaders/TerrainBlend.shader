Shader "Planet Survival/Terrain Blend"
{
    Properties
    {
        [HideInInspector] _Control0 ("Control 0", 2D) = "black" {}
        [HideInInspector] _Control1 ("Control 1", 2D) = "black" {}
        [HideInInspector] _ControlUv ("Control UV", Vector) = (1, 1, 0, 0)
        [HideInInspector] _TilesPerChunk ("Tiles Per Chunk", Float) = 8
        [HideInInspector] _TileOrigin ("Tile Origin", Vector) = (0, 0, 0, 0)
        [HideInInspector] _VariantSeed ("Variant Seed", Float) = 0
        [HideInInspector] _LayerCount ("Layer Count", Float) = 0
        [HideInInspector] _VariantLayerIndex ("Variant Layer Index", Float) = -1
        [HideInInspector] _VariantCount ("Variant Count", Float) = 1
        [HideInInspector] _Variant1 ("Variant 1", 2D) = "white" {}
        [HideInInspector] _Variant2 ("Variant 2", 2D) = "white" {}
        [HideInInspector] _SecondVariantLayerIndex ("Second Variant Layer Index", Float) = -1
        [HideInInspector] _SecondVariantCount ("Second Variant Count", Float) = 1
        [HideInInspector] _SecondVariant1 ("Second Variant 1", 2D) = "white" {}
        [HideInInspector] _SecondVariant2 ("Second Variant 2", 2D) = "white" {}
        [HideInInspector] _Layer0 ("Layer 0", 2D) = "white" {}
        [HideInInspector] _Layer1 ("Layer 1", 2D) = "white" {}
        [HideInInspector] _Layer2 ("Layer 2", 2D) = "white" {}
        [HideInInspector] _Layer3 ("Layer 3", 2D) = "white" {}
        [HideInInspector] _Layer4 ("Layer 4", 2D) = "white" {}
        [HideInInspector] _Layer5 ("Layer 5", 2D) = "white" {}
        [HideInInspector] _Layer6 ("Layer 6", 2D) = "white" {}
        [HideInInspector] _Layer7 ("Layer 7", 2D) = "white" {}
        [HideInInspector] _Layer0Scale ("Layer 0 Scale", Float) = 1
        [HideInInspector] _Layer1Scale ("Layer 1 Scale", Float) = 1
        [HideInInspector] _Layer2Scale ("Layer 2 Scale", Float) = 1
        [HideInInspector] _Layer3Scale ("Layer 3 Scale", Float) = 1
        [HideInInspector] _Layer4Scale ("Layer 4 Scale", Float) = 1
        [HideInInspector] _Layer5Scale ("Layer 5 Scale", Float) = 1
        [HideInInspector] _Layer6Scale ("Layer 6 Scale", Float) = 1
        [HideInInspector] _Layer7Scale ("Layer 7 Scale", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _Control0;
            sampler2D _Control1;
            sampler2D _Variant1;
            sampler2D _Variant2;
            sampler2D _SecondVariant1;
            sampler2D _SecondVariant2;
            sampler2D _Layer0;
            sampler2D _Layer1;
            sampler2D _Layer2;
            sampler2D _Layer3;
            sampler2D _Layer4;
            sampler2D _Layer5;
            sampler2D _Layer6;
            sampler2D _Layer7;
            float4 _ControlUv;
            float _TilesPerChunk;
            float2 _TileOrigin;
            float _VariantSeed;
            float _LayerCount;
            float _VariantLayerIndex;
            float _VariantCount;
            float _SecondVariantLayerIndex;
            float _SecondVariantCount;
            float _Layer0Scale;
            float _Layer1Scale;
            float _Layer2Scale;
            float _Layer3Scale;
            float _Layer4Scale;
            float _Layer5Scale;
            float _Layer6Scale;
            float _Layer7Scale;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 chunkUv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.chunkUv = input.uv;
                return output;
            }

            void Composite(inout fixed3 premultiplied, inout fixed alpha, fixed4 surface, fixed weight)
            {
                fixed sourceAlpha = saturate(surface.a * weight);
                premultiplied = surface.rgb * sourceAlpha + premultiplied * (1.0h - sourceAlpha);
                alpha = sourceAlpha + alpha * (1.0h - sourceAlpha);
            }

            fixed4 SampleSurfaceVariant(fixed4 primary, float layerIndex, float2 tileUv,
                float2 absoluteTile)
            {
                // Integer world-tile coordinates make the choice stable across chunk boundaries and
                // streaming reloads. Mixing in the world seed keeps separate expeditions distinct.
                float2 seededTile = absoluteTile
                    + float2(_VariantSeed * 0.1031, _VariantSeed * 0.0973)
                    + layerIndex * float2(17.17, 29.29);
                float randomValue = frac(sin(dot(seededTile, float2(12.9898, 78.233))) * 43758.5453);

                if (abs(_VariantLayerIndex - layerIndex) < 0.25 && _VariantCount > 1.5)
                {
                    float variantIndex = floor(randomValue * _VariantCount);
                    if (variantIndex < 0.5)
                    {
                        return primary;
                    }

                    return variantIndex < 1.5 ? tex2D(_Variant1, tileUv) : tex2D(_Variant2, tileUv);
                }

                if (abs(_SecondVariantLayerIndex - layerIndex) < 0.25 && _SecondVariantCount > 1.5)
                {
                    float variantIndex = floor(randomValue * _SecondVariantCount);
                    if (variantIndex < 0.5)
                    {
                        return primary;
                    }

                    return variantIndex < 1.5
                        ? tex2D(_SecondVariant1, tileUv)
                        : tex2D(_SecondVariant2, tileUv);
                }

                return primary;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // Terrain identity and digging are tile-based. Sampling the control maps at the tile
                // centre keeps one visual on the entire gameplay tile instead of cutting an illustration
                // wherever the continuous field happens to cross it.
                float tileCount = max(1.0, _TilesPerChunk);
                float2 tilePosition = input.chunkUv * tileCount;
                float2 tileIndex = min(floor(tilePosition), tileCount - 1.0);
                float2 absoluteTile = _TileOrigin + tileIndex;
                float2 tileCentreUv = (tileIndex + 0.5) / tileCount;
                float2 controlUv = tileCentreUv * _ControlUv.xy + _ControlUv.zw;
                fixed4 control0 = step(0.5h, tex2D(_Control0, controlUv));
                fixed4 control1 = step(0.5h, tex2D(_Control1, controlUv));

                // Every tile receives one complete copy of its square terrain artwork. At the positive
                // outer edge tilePosition reaches tileCount exactly, so subtracting the clamped index
                // deliberately yields 1 rather than wrapping that final edge back to 0.
                float2 tileUv = saturate(tilePosition - tileIndex);
                fixed3 premultiplied = 0;
                fixed alpha = 0;

                // Exactly one terrain owns a gameplay tile. Preserve that invariant even at control-map
                // filtering boundaries: transparent pixels reveal base regolith, never a lower-priority
                // resource such as rock beneath iron.
                if (_LayerCount > 0.5 && control0.r > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer0, tileUv), 0.0, tileUv, absoluteTile), control0.r);
                else if (_LayerCount > 1.5 && control0.g > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer1, tileUv), 1.0, tileUv, absoluteTile), control0.g);
                else if (_LayerCount > 2.5 && control0.b > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer2, tileUv), 2.0, tileUv, absoluteTile), control0.b);
                else if (_LayerCount > 3.5 && control0.a > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer3, tileUv), 3.0, tileUv, absoluteTile), control0.a);
                else if (_LayerCount > 4.5 && control1.r > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer4, tileUv), 4.0, tileUv, absoluteTile), control1.r);
                else if (_LayerCount > 5.5 && control1.g > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer5, tileUv), 5.0, tileUv, absoluteTile), control1.g);
                else if (_LayerCount > 6.5 && control1.b > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer6, tileUv), 6.0, tileUv, absoluteTile), control1.b);
                else if (_LayerCount > 7.5 && control1.a > 0.5h) Composite(premultiplied, alpha, SampleSurfaceVariant(tex2D(_Layer7, tileUv), 7.0, tileUv, absoluteTile), control1.a);

                return fixed4(premultiplied, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
