Shader "Planet Survival/Talus"
{
    // Loose rocks (fallen at the foot of the mountains, thrown out of craters), drawn as camera-facing
    // billboards like every other object in the world. Each vertex carries its rock's foot position; the quad is grown here along the camera's
    // right and up axes. For depth the rock is treated as an upright board standing at its near edge (see
    // StandingDepth.cginc), so a rock lying in front of a wall is drawn whole in front of it.
    Properties
    {
        [HideInInspector] _PatchArray ("Artwork", 2DArray) = "" {}
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
            #pragma target 3.5
            #pragma require 2darray
            #include "UnityCG.cginc"
            #include "StandingDepth.cginc"
            #include "TerrainSurface.cginc"

            Texture2DArray _PatchArray;
            SamplerState sampler_PatchArray;
            float4 _TalusTint;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float2 sizeAndSlice : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 uvAndSlice : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float3 foot = mul(unity_ObjectToWorld, float4(input.vertex.xyz, 1.0)).xyz;
                // Rocks lie on the drawn ground, down in a crater as well.
                foot.y += SurfaceHeight(foot.xz);
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float size = input.sizeAndSlice.x;
                float3 world = foot + (cameraRight * input.corner.x + cameraUp * input.corner.y) * size;
                // The near edge of the rock, where its image meets the ground on screen.
                float nearEdgeZ = foot.z - size * 0.55;
                output.vertex = StandingClipPosition(world, nearEdgeZ);
                output.uvAndSlice = float3(input.corner + 0.5, input.sizeAndSlice.y);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float4 rock = _PatchArray.Sample(sampler_PatchArray, input.uvAndSlice);
                // The array holds straight alpha; the blend expects premultiplied colour.
                return float4(rock.rgb * _TalusTint.rgb * rock.a, rock.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
