Shader "Planet Survival/Mountain"
{
    // Real 3D mountain meshes built per terrain chunk. Flat parts take the mountain-top texture projected
    // from above; steep parts take the cliff texture projected from the side along X and Z, so bedding stays
    // horizontal on every wall whichever way it faces. Shading uses the shared terrain light rather than
    // Unity lighting, matching the unlit sprites and ground. Under the near-vertical camera relief only
    // shows through light: slopes are lit _ReliefShading times steeper than they are (a hillshade
    // z-factor) and hollows darken, measured in the chunk's height map. Cast shadows across the tops were
    // tried and dropped: exaggerated, they turned every slope facing away from the light into a black blob.
    // Meshes are built _VerticalScale times taller than their nominal shape so walls read at sprite scale
    // under the steep camera; normals, texture heights and occlusion undo that scale.
    Properties
    {
        [HideInInspector] _MountainTex ("Mountain Top", 2D) = "white" {}
        [HideInInspector] _CliffTex ("Cliff", 2D) = "white" {}
        [HideInInspector] _Elevation ("Landform Map", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "TerrainCommon.cginc"
            #include "TerrainSurface.cginc"

            sampler2D _MountainTex;
            float _MountainTexScale;
            float4 _MountainTint;
            sampler2D _CliffTex;
            float _CliffTexScale;
            float4 _CliffColor;
            float _VerticalScale;
            float _ReliefShading;
            sampler2D _Elevation;
            float4 _ElevationUv;
            float2 _ChunkOrigin;
            float _ChunkSize;

            #define CAVITY_RADIUS 1.5
            #define LIGHT_WRAP 0.3

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                float3 world = mul(unity_ObjectToWorld, input.vertex).xyz;
                // The buried foot follows the drawn ground, so a mountain beside a crater reaches down to
                // the sunken ground instead of leaving a gap under its edge.
                float foot = 1.0 - step(0.0, input.vertex.y);
                world.y += SurfaceHeight(world.xz) * foot;
                output.vertex = UnityWorldToClipPos(world);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            // True (unexaggerated) mountain height, from the same map the mesh was built from.
            float HeightAt(float2 world)
            {
                float2 chunkUv = (world - _ChunkOrigin) / _ChunkSize;
                return tex2Dlod(_Elevation, float4(chunkUv * _ElevationUv.xy + _ElevationUv.zw, 0, 0)).g;
            }

            // Darkening of hollows and valley floors: how far the surrounding rock stands above the point.
            float Cavity(float2 world, float height)
            {
                float around = (HeightAt(world + float2(CAVITY_RADIUS, 0)) + HeightAt(world - float2(CAVITY_RADIUS, 0))
                    + HeightAt(world + float2(0, CAVITY_RADIUS)) + HeightAt(world - float2(0, CAVITY_RADIUS))) * 0.25;
                return lerp(0.65, 1.0, saturate(1.0 - max(0.0, around - height) * 0.35));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                // A heightfield stretched by k in Y has normals squashed by k; stretch them back for lighting.
                float3 stretched = normalize(input.worldNormal);
                stretched = stretched.y < 0.0 ? -stretched : stretched;
                float3 normal = normalize(float3(stretched.x, stretched.y * _VerticalScale, stretched.z));
                float3 position = input.worldPosition;
                float trueHeight = position.y / max(_VerticalScale, 1.0);

                float3 top = SampleUntiled(_MountainTex, position.xz * _MountainTexScale) * _MountainTint.rgb;

                // Walls facing along Z show the texture laid out across X; walls facing along X across Z.
                float alongZ = abs(normal.z);
                float alongX = abs(normal.x);
                float3 wallZ = SampleUntiled(_CliffTex, float2(position.x, trueHeight) * _CliffTexScale);
                float3 wallX = SampleUntiled(_CliffTex, float2(position.z, trueHeight) * _CliffTexScale);
                float3 cliff = (wallZ * alongZ + wallX * alongX) / max(alongZ + alongX, 1e-4) * _CliffColor.rgb;

                // Only true rock faces show the cliff texture; scree slopes of 50° or so keep the top's rubble,
                // so an edge alternates between faces and slopes instead of wearing one continuous wall.
                float3 albedo = lerp(cliff, top, smoothstep(0.35, 0.6, normal.y));

                // Calibrated so flat rock reads at its texture colour. Slopes towards the light brighten,
                // slopes away and south-facing walls fall towards the ambient level, which tilts a little
                // towards the open sky so walls stay darker than shaded tops. The slight wrap keeps slopes
                // facing the camera, which the view stretches by their height, from going flat black.
                float3 litNormal = normalize(float3(normal.x, normal.y / max(_ReliefShading, 1.0), normal.z));
                float diffuse = saturate((dot(litNormal, LIGHT_DIRECTION) + LIGHT_WRAP) / (1.0 + LIGHT_WRAP));
                float ambient = lerp(0.42, 0.5, saturate(normal.y));
                float light = (ambient + 0.66 * diffuse) * Cavity(position.xz, trueHeight);
                // Occlusion where rock meets the ground. Kept to the bottom metre: a low scree slope lies
                // wholly within a taller band and would be darkened from foot to brink.
                float occlusion = lerp(0.72, 1.0, saturate(trueHeight / 0.8));
                return fixed4(albedo * light * occlusion, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
