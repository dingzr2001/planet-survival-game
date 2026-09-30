Shader "Planet Survival/Terrain Blend"
{
    // Draws the ground of one streamed terrain chunk over the base regolith disc, in premultiplied alpha:
    //   base ground (regolith, or bare rock over rock ground) ->
    //   relief shading, basin tint and craters -> ice lakes -> fissures, lava glow and lava lakes ->
    //   continuous patches (sheet ice) -> scattered patch artwork -> mountain shadows and occlusion.
    // Mountains (Mountain.shader) and loose rocks (Talus.shader) are meshes of their own; this pass only
    // draws the ground itself. Landform borders come from one continuous field measured in metres, so they
    // follow the terrain rather than any grid. Patch artwork is scattered per gameplay tile with a random offset and
    // size and may overlap its neighbours, which hides the tile lattice while keeping one tile = one deposit.
    Properties
    {
        [HideInInspector] _Elevation ("Landform Map", 2D) = "black" {}
        [HideInInspector] _PatchIds ("Patch Ids", 2D) = "black" {}
        [HideInInspector] _PatchArray ("Patch Artwork", 2DArray) = "" {}
        [HideInInspector] _IceTex ("Ice", 2D) = "white" {}
        [HideInInspector] _CraterFloorTex ("Crater Floor", 2D) = "white" {}
        [HideInInspector] _Volcanic ("Volcanic Map", 2D) = "black" {}
        [HideInInspector] _LavaTex ("Lava", 2D) = "black" {}
        [HideInInspector] _RockGroundTex ("Rock Ground", 2D) = "gray" {}
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
            #include "TerrainCommon.cginc"
            #include "TerrainSurface.cginc"

            #define MAX_LAYERS 16
            // Elevation units are "lake shore to mountain foot"; relief is exaggerated so gentle slopes read.
            #define RELIEF_SCALE 55.0
            // Metres between fallen rocks at the foot of a cliff; each cell may hold one scattered cutout.
            // Steps of the shadow march towards the light.
            #define SHADOW_STEPS 10

            // R = normalised elevation, G = mountain mesh height (m), B = metres inside the mountain border.
            sampler2D _Elevation;
            float4 _ElevationUv;
            float _ElevationStep;
            float _HasLandforms;
            float _BasinLevel;

            sampler2D _PatchIds;
            float _PatchTextureSize;
            float _TilesPerChunk;
            float2 _TileOrigin;
            float _TileSize;
            float2 _ChunkOrigin;
            float _ChunkSize;
            float _LayerCount;
            float _LayerFirstSlice[MAX_LAYERS];
            float _LayerVariantCount[MAX_LAYERS];
            float _LayerContinuous[MAX_LAYERS];
            float _LayerTextureScale[MAX_LAYERS];
            Texture2DArray _PatchArray;
            SamplerState sampler_PatchArray;

            float _ShadowReach;
            float _ShadowStrength;
            sampler2D _IceTex;
            float _IceTexScale;
            float4 _BasinTint;
            sampler2D _CraterFloorTex;
            float _CraterFloorTexScale;
            float4 _CraterFloorTint;
            float4 _CraterEjectaTint;
            float _CraterReliefStrength;
            sampler2D _Volcanic;
            sampler2D _LavaTex;
            float _LavaTexScale;
            float _LavaFlowSpeed;
            float4 _LavaGlowColor;
            float _LavaGlowReach;
            float4 _LavaCrustColor;
            sampler2D _RockGroundTex;
            float _RockGroundTexScale;
            float4 _RockGroundTint;
            float _FissureFirstSlice;
            float _FissureSliceCount;
            float _FissureDensity;
            float4 _FissureSizeRange;
            float _EjectaReach;
            float _HillshadeStrength;
            float _EdgeRoughness;

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
                // The chunk is a fine grid so it can follow the drawn ground down into craters.
                float3 world = mul(unity_ObjectToWorld, input.vertex).xyz;
                world.y += SurfaceHeight(world.xz);
                output.vertex = UnityWorldToClipPos(world);
                output.chunkUv = input.uv;
                return output;
            }

            // The base regolith, the same texture and tiling as the far ground disc. Chunks draw it
            // themselves, opaque, because where they sink into a crater the flat disc is no longer behind them.
            sampler2D _BaseGroundTex;
            float _BaseGroundTexScale;
            float _BaseGroundEnabled;

            // Everything drawn so far laid over the base ground: bare rock where rockCoverage reaches 1,
            // regolith elsewhere. Rock ground spans hundreds of metres, so its texture is drawn untiled.
            fixed4 OverBaseGround(float3 premultiplied, float alpha, float2 world, float rockCoverage)
            {
                float3 rock = rockCoverage > 0.0
                    ? SampleUntiled(_RockGroundTex, world * _RockGroundTexScale) * _RockGroundTint.rgb
                    : float3(0, 0, 0);
                if (_BaseGroundEnabled < 0.5)
                {
                    float covered = rockCoverage * (1.0 - alpha);
                    return fixed4(premultiplied + rock * covered, alpha + covered);
                }

                float3 regolith = tex2D(_BaseGroundTex, world * _BaseGroundTexScale).rgb;
                return fixed4(premultiplied + lerp(regolith, rock, rockCoverage) * (1.0 - alpha), 1.0);
            }

            float4 LandformAt(float2 world)
            {
                float2 chunkUv = (world - _ChunkOrigin) / _ChunkSize;
                return tex2Dlod(_Elevation, float4(chunkUv * _ElevationUv.xy + _ElevationUv.zw, 0, 0));
            }

            float ElevationAt(float2 world) { return LandformAt(world).r; }
            float MountainHeightAt(float2 world) { return LandformAt(world).g; }
            float BorderDistanceAt(float2 world) { return LandformAt(world).b; }

            // Layer owning a tile, or -1; tiles are addressed in world tile coordinates.
            int PatchLayerAt(int2 tile)
            {
                float2 texel = (float2)(tile - (int2)_TileOrigin) + 1.5;
                return (int)round(tex2Dlod(_PatchIds, float4(texel / _PatchTextureSize, 0, 0)).r * 255.0) - 1;
            }

            void Composite(inout float3 premultiplied, inout float alpha, float3 color, float coverage)
            {
                float source = saturate(coverage);
                premultiplied = color * source + premultiplied * (1.0 - source);
                alpha = source + alpha * (1.0 - source);
            }

            // Scatters patch artwork: every tile around this pixel may paint its deposit here, shifted and
            // resized by the tile's hash. Northern tiles paint first so southern ones overlap them, as a
            // standing object nearer the camera would.
            void CompositePatches(inout float3 premultiplied, inout float alpha, float2 world, float2 worldDx,
                float2 worldDy)
            {
                if (_LayerCount < 0.5)
                {
                    return;
                }

                int2 tile = (int2)floor(world / _TileSize);
                [unroll]
                for (int dz = 1; dz >= -1; dz--)
                {
                    [unroll]
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int2 neighbour = tile + int2(dx, dz);
                        float2 texel = (float2)(neighbour - (int2)_TileOrigin) + 1.5;
                        float4 patch = tex2Dlod(_PatchIds, float4(texel / _PatchTextureSize, 0, 0));
                        int layer = (int)round(patch.r * 255.0) - 1;
                        if (layer < 0 || _LayerContinuous[layer] > 0.5)
                        {
                            continue;
                        }

                        float size = lerp(0.9, 1.2, Hash01(neighbour, 1u)) * lerp(0.55, 1.0, patch.g);
                        float2 offset = (float2(Hash01(neighbour, 2u), Hash01(neighbour, 3u)) - 0.5) * 0.7;
                        float2 centre = ((float2)neighbour + 0.5 + offset) * _TileSize;
                        float extent = _TileSize * size;
                        float2 uv = (world - centre) / extent + 0.5;
                        if (any(uv < 0.0) || any(uv > 1.0))
                        {
                            continue;
                        }

                        float variant = floor(Hash01(neighbour, 4u) * _LayerVariantCount[layer]);
                        float slice = _LayerFirstSlice[layer] + min(variant, _LayerVariantCount[layer] - 1.0);
                        float4 art = _PatchArray.SampleGrad(sampler_PatchArray, float3(uv, slice),
                            worldDx / extent, worldDy / extent);
                        // The array holds straight alpha; the composite expects colour and coverage apart.
                        Composite(premultiplied, alpha, art.rgb, art.a);
                    }
                }
            }

            // Coverage of a sheet (ice) from how far a point belongs to it, 0.5 on its nominal border. Two noise
            // octaves throw the edge into lobes and bays; ice lakes and sheet-ice patches share it so every
            // expanse of ice has the same ragged shore.
            float RaggedSheetCoverage(float2 world, float membership)
            {
                float noise = (ValueNoise(world * 0.9) - 0.5) * 0.45 + (ValueNoise(world * 2.9) - 0.5) * 0.2;
                return smoothstep(0.4, 0.55, membership + noise);
            }

            // Seamless surfaces such as sheet ice: the tile ownership around the pixel is blended bilinearly
            // between tile centres and thresholded with noise, which rounds a run of tiles into one sheet
            // with a ragged rim instead of a staircase of squares.
            void CompositeContinuousPatches(inout float3 premultiplied, inout float alpha, float2 world)
            {
                if (_LayerCount < 0.5)
                {
                    return;
                }

                float2 lattice = world / _TileSize - 0.5;
                int2 corner = (int2)floor(lattice);
                float2 t = lattice - floor(lattice);
                int layers[4];
                layers[0] = PatchLayerAt(corner);
                layers[1] = PatchLayerAt(corner + int2(1, 0));
                layers[2] = PatchLayerAt(corner + int2(0, 1));
                layers[3] = PatchLayerAt(corner + int2(1, 1));
                float weights[4];
                weights[0] = (1.0 - t.x) * (1.0 - t.y);
                weights[1] = t.x * (1.0 - t.y);
                weights[2] = (1.0 - t.x) * t.y;
                weights[3] = t.x * t.y;

                int bestLayer = -1;
                float bestMembership = 0.0;
                [unroll]
                for (int candidate = 0; candidate < 4; candidate++)
                {
                    int layer = layers[candidate];
                    if (layer < 0 || _LayerContinuous[layer] < 0.5)
                    {
                        continue;
                    }

                    float membership = 0.0;
                    [unroll]
                    for (int other = 0; other < 4; other++)
                    {
                        membership += layers[other] == layer ? weights[other] : 0.0;
                    }

                    if (membership > bestMembership)
                    {
                        bestMembership = membership;
                        bestLayer = layer;
                    }
                }

                if (bestLayer < 0)
                {
                    return;
                }

                float coverage = RaggedSheetCoverage(world, bestMembership);
                float2 uv = world * _LayerTextureScale[bestLayer];
                float slice = _LayerFirstSlice[bestLayer];
                float4 art = lerp(
                    _PatchArray.Sample(sampler_PatchArray, float3(uv, slice)),
                    _PatchArray.Sample(sampler_PatchArray, float3(UntiledSecondUv(uv), slice)),
                    UntiledBlend(uv));
                Composite(premultiplied, alpha, art.rgb, coverage * max(art.a, 0.85));
            }

            // Craters. The landform map's alpha holds the distance to the nearest crater's centre in its radii,
            // CRATER_DISTANCE_CAP where none is near. From it and its gradient (which points away from the
            // centre, and whose length is one over the radius) the crater profile (TerrainSurface.cginc, the one
            // the ground is sunk by) is lit. Its light
            // and shade are what make a crater read from above.
            // Floor and ejecta radii match CraterMarkings on the C# side, which scatters the ejecta rocks.
            #define CRATER_DISTANCE_CAP 3.0
            // How much steeper than the drawn relief crater walls are lit: the drawn walls are real but gentle,
            // and from almost straight above only light shows them.
            #define CRATER_RELIEF 1.6
            #define CRATER_FLOOR_FULL 0.5
            #define CRATER_FLOOR_END 0.85
            #define CRATER_EJECTA_START 0.9
            #define CRATER_EJECTA_FULL 1.05
            // Metres between the samples that give the direction to the centre; far enough apart that the
            // half-precision distance still yields a clean direction on the largest craters.
            #define CRATER_GRADIENT_STEP 1.5

            float CraterDistanceAt(float2 world) { return LandformAt(world).a; }

            // Change in crater distance per metre. It points away from the centre and its length is one over
            // the crater's radius.
            float2 CraterGradient(float2 world)
            {
                float2 stepX = float2(CRATER_GRADIENT_STEP, 0);
                float2 stepZ = float2(0, CRATER_GRADIENT_STEP);
                return float2(
                    CraterDistanceAt(world + stepX) - CraterDistanceAt(world - stepX),
                    CraterDistanceAt(world + stepZ) - CraterDistanceAt(world - stepZ)) / (2.0 * CRATER_GRADIENT_STEP);
            }

            // Brightening (positive) or darkening (negative) of the ground by the crater's relief.
            float CraterShade(float2 outward, float distance)
            {
                // Heights scale with the radius, so the slope in metres per metre is the profile's own slope.
                float slope = (CraterReliefProfile(distance + 0.01) - CraterReliefProfile(distance - 0.01)) / 0.02;
                float2 tilt = -slope * outward * CRATER_RELIEF;
                float3 normal = normalize(float3(tilt.x, 1.0, tilt.y));
                return dot(normal, LIGHT_DIRECTION) / LIGHT_DIRECTION.y - 1.0;
            }

            // The volcanic fields (VolcanicField): x = rock ground, 0 on its border; y = lava activity, 1 on a
            // lava shore and always below 0 off rock ground. Laid out like the landform map, so it shares its UV transform.
            float2 VolcanicAt(float2 world)
            {
                float2 chunkUv = (world - _ChunkOrigin) / _ChunkSize;
                return tex2Dlod(_Volcanic, float4(chunkUv * _ElevationUv.xy + _ElevationUv.zw, 0, 0)).rg;
            }

            // Metres per unit of each volcanic field, so their thresholds can be turned into distances.
            float2 VolcanicMetresPerUnit(float2 world)
            {
                float2 stepX = float2(_ElevationStep, 0);
                float2 stepZ = float2(0, _ElevationStep);
                float2 alongX = (VolcanicAt(world + stepX) - VolcanicAt(world - stepX)) / (2.0 * _ElevationStep);
                float2 alongZ = (VolcanicAt(world + stepZ) - VolcanicAt(world - stepZ)) / (2.0 * _ElevationStep);
                return 1.0 / max(float2(length(float2(alongX.x, alongZ.x)), length(float2(alongX.y, alongZ.y))), 1e-4);
            }

            // How fully bare rock replaces the regolith. The border is thrown into broad lobes and frayed
            // finely, so from above it reads as regolith drifted over the rock rather than as a contour line.
            #define ROCK_EDGE_LOBES 7.0
            #define ROCK_EDGE_SOFTNESS 0.5
            float RockGroundCoverage(float2 world, float rockGround, float metresPerUnit)
            {
                float lobes = (ValueNoise(world * 0.06) - 0.5) * 2.0 * ROCK_EDGE_LOBES;
                float fray = (ValueNoise(world * 0.8) - 0.5) * 1.6 + (ValueNoise(world * 2.6) - 0.5) * 0.6;
                return smoothstep(-ROCK_EDGE_SOFTNESS, ROCK_EDGE_SOFTNESS, rockGround * metresPerUnit + lobes + fray);
            }

            // Glowing fissures scattered over rock ground around the lava, one per cell at most, turned and
            // sized at random so the few cutouts never repeat visibly. They crowd towards the lava and never
            // leave rock ground. Purely decorative: gameplay never asks about them.
            #define FISSURE_CELL 6.0
            void CompositeFissures(inout float3 premultiplied, inout float alpha, float2 world, float2 worldDx,
                float2 worldDy)
            {
                if (_FissureSliceCount < 0.5)
                {
                    return;
                }

                int2 home = (int2)floor(world / FISSURE_CELL);
                [unroll]
                for (int dz = 1; dz >= -1; dz--)
                {
                    [unroll]
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int2 cell = home + int2(dx, dz);
                        float2 jitter = (float2(Hash01(cell, 31u), Hash01(cell, 32u)) - 0.5) * 0.8;
                        float2 centre = ((float2)cell + 0.5 + jitter) * FISSURE_CELL;
                        float activity = VolcanicAt(centre).y;
                        float density = smoothstep(0.0, 0.6, activity) * (1.0 - smoothstep(0.95, 1.05, activity));
                        if (Hash01(cell, 33u) >= density * _FissureDensity)
                        {
                            continue;
                        }

                        float size = lerp(_FissureSizeRange.x, _FissureSizeRange.y, Hash01(cell, 34u));
                        float angle = Hash01(cell, 35u) * 6.2831853;
                        float2x2 turn = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
                        float2 uv = mul(turn, world - centre) / size + 0.5;
                        if (any(uv < 0.0) || any(uv > 1.0))
                        {
                            continue;
                        }

                        float slice = _FissureFirstSlice
                            + min(floor(Hash01(cell, 36u) * _FissureSliceCount), _FissureSliceCount - 1.0);
                        float4 art = _PatchArray.SampleGrad(sampler_PatchArray, float3(uv, slice),
                            mul(turn, worldDx) / size, mul(turn, worldDy) / size);
                        Composite(premultiplied, alpha, art.rgb, art.a);
                    }
                }
            }

            // Molten rock, drifting slowly as two offset layers so the surface never reads as painted.
            float3 LavaColour(float2 world)
            {
                float2 uv = world * _LavaTexScale;
                float drift = _Time.y * _LavaFlowSpeed * _LavaTexScale;
                float3 first = tex2D(_LavaTex, uv + float2(drift, drift * 0.4)).rgb;
                float3 second = tex2D(_LavaTex, UntiledSecondUv(uv) + float2(-drift * 0.6, drift * 0.3)).rgb;
                return lerp(first, second, UntiledBlend(uv));
            }

            // The shadow of the mountain meshes, by marching from the ground point towards the light through
            // the same heights the meshes were built from, so shadow and rock always line up.
            float MountainShadow(float2 world)
            {
                float2 towardLight = normalize(LIGHT_DIRECTION.xz);
                float lightRise = LIGHT_DIRECTION.y / length(LIGHT_DIRECTION.xz);
                float shadow = 0.0;
                [unroll]
                for (int i = 1; i <= SHADOW_STEPS; i++)
                {
                    float travel = _ShadowReach * i / SHADOW_STEPS;
                    float rockAbove = MountainHeightAt(world + towardLight * travel) - travel * lightRise;
                    // Faded towards the end of the march, so a summit taller than the reach never ends its
                    // shadow in a line.
                    float fade = 1.0 - smoothstep(0.7 * _ShadowReach, _ShadowReach, travel);
                    shadow = max(shadow, saturate(rockAbove / 0.35) * fade);
                }

                return shadow;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 world = _ChunkOrigin + input.chunkUv * _ChunkSize;
                float2 worldDx = ddx(world);
                float2 worldDy = ddy(world);
                float3 premultiplied = 0;
                float alpha = 0;

                int2 ownTile = (int2)floor(world / _TileSize);
                float2 ownTexel = (float2)(ownTile - (int2)_TileOrigin) + 1.5;
                bool dugAway = tex2Dlod(_PatchIds, float4(ownTexel / _PatchTextureSize, 0, 0)).b > 0.5;

                if (_HasLandforms < 0.5)
                {
                    CompositeContinuousPatches(premultiplied, alpha, world);
                    CompositePatches(premultiplied, alpha, world, worldDx, worldDy);
                    return OverBaseGround(premultiplied, alpha, world, 0.0);
                }

                float2 volcanic = VolcanicAt(world);
                float2 volcanicMetres = VolcanicMetresPerUnit(world);
                float rockCoverage = RockGroundCoverage(world, volcanic.x, volcanicMetres.x);

                float texelStep = _ElevationStep;
                float elevation = ElevationAt(world);
                float2 gradient = float2(
                    ElevationAt(world + float2(texelStep, 0)) - ElevationAt(world - float2(texelStep, 0)),
                    ElevationAt(world + float2(0, texelStep)) - ElevationAt(world - float2(0, texelStep)))
                    / (2.0 * texelStep);
                float slope = max(length(gradient), 1e-4);
                // Signed distance in metres to the basin border, roughened by a shared noise so it wanders
                // instead of tracing a smooth contour line. The mountain border distance is stored already
                // roughened, identical to the one the mountain mesh was built from.
                float rough = (ValueNoise(world * 0.45) - 0.5) * 2.0 * _EdgeRoughness
                    + (ValueNoise(world * 1.7) - 0.5) * 0.5 * _EdgeRoughness;
                float basinDistance = (_BasinLevel - elevation) / slope + rough * 2.0;
                float mountainDistance = BorderDistanceAt(world);

                // Relief shading and basin tint, laid over the base ground as translucent light and shadow.
                // Inside and around a crater both give way to the crater's own shading: blurred into its bowl
                // they only drew a soft, even dark ring.
                float craterDistance = CraterDistanceAt(world);
                float craterWeight = 1.0 - smoothstep(1.3, 1.6, craterDistance);
                float3 normal = normalize(float3(-gradient.x * RELIEF_SCALE, 1.0, -gradient.y * RELIEF_SCALE));
                float shade = dot(normal, LIGHT_DIRECTION) / LIGHT_DIRECTION.y - 1.0;
                Composite(premultiplied, alpha, float3(1, 1, 1) * step(0.0, shade),
                    abs(shade) * _HillshadeStrength * (shade > 0.0 ? 0.5 : 1.0) * (1.0 - craterWeight));
                Composite(premultiplied, alpha, _BasinTint.rgb,
                    _BasinTint.a * smoothstep(-1.5, 1.5, basinDistance) * (1.0 - craterWeight));

                float2 craterGradient = craterDistance < CRATER_DISTANCE_CAP - 0.5 ? CraterGradient(world) : float2(0, 0);
                float2 outward = craterGradient / max(length(craterGradient), 1e-5);
                if (craterDistance < CRATER_DISTANCE_CAP - 0.5)
                {
                    // Dust settled on the floor, fading out up the wall.
                    float floorWeight = 1.0 - smoothstep(CRATER_FLOOR_FULL, CRATER_FLOOR_END, craterDistance);
                    if (floorWeight > 0.0)
                    {
                        float3 dust = SampleUntiled(_CraterFloorTex, world * _CraterFloorTexScale) * _CraterFloorTint.rgb;
                        Composite(premultiplied, alpha, dust, _CraterFloorTint.a * floorWeight);
                    }

                    // The lighter ejecta blanket around the rim, broken up so it reads as scattered material.
                    float blanket = smoothstep(CRATER_EJECTA_START, CRATER_EJECTA_FULL, craterDistance)
                        * (1.0 - smoothstep(CRATER_EJECTA_FULL, _EjectaReach, craterDistance));
                    float ejectaBreakup = 0.55 + 0.9 * ValueNoise(world * 0.6) * ValueNoise(world * 1.9 + 3.1);
                    Composite(premultiplied, alpha, _CraterEjectaTint.rgb,
                        saturate(_CraterEjectaTint.a * blanket * ejectaBreakup));

                    // Walls turned to the light brighten, walls turned away and the inner side of the rim
                    // darken, which is what outlines a crater from above. Shade stops well short of black so
                    // the ground under it still reads.
                    float craterShade = CraterShade(outward, craterDistance);
                    Composite(premultiplied, alpha, float3(1, 1, 1) * step(0.0, craterShade),
                        saturate(abs(craterShade)) * _CraterReliefStrength * (craterShade > 0.0 ? 0.8 : 0.65));
                }

                // Ice lakes, drawn like sheet-ice patches: the distance to the shore is expressed as the tile
                // membership a patch would have at that point, so both get the same ragged edge. A tile dug
                // through the ice shows the lake bed beneath.
                float lakeMembership = 0.5 + (-elevation / slope) / _TileSize;
                // Lakes are level frozen water: none on a crater's tilted wall, rim or outer slope. The band's
                // edges, in metres, join the shore so they get the same ragged edge; gameplay tiles follow the
                // same band (CraterField.IsOnSlope).
                float craterRadius = 1.0 / max(length(craterGradient), 1e-4);
                float slopeBandDistance = max(_SurfaceCraterProfile.w - craterDistance, craterDistance - _SurfaceCraterReach)
                    * craterRadius;
                lakeMembership = min(lakeMembership, 0.5 + slopeBandDistance / _TileSize);
                // Rock ground never holds ice (LandformSampler); its edge joins the shore the same way.
                lakeMembership = min(lakeMembership, 0.5 + -volcanic.x * volcanicMetres.x / _TileSize);
                float lakeCoverage = RaggedSheetCoverage(world, lakeMembership) * (dugAway ? 0.0 : 1.0);
                if (lakeCoverage > 0.0)
                {
                    Composite(premultiplied, alpha, SampleUntiled(_IceTex, world * _IceTexScale), lakeCoverage);
                }

                // Lava lakes where the lava field passes 1, with the same ragged shore and the same
                // exclusion from crater slopes as ice. The ground around them glows, and the lava crusts over
                // darkly where it meets the shore. The lava field is below zero off rock ground, so all of
                // this stays on rock ground.
                if (volcanic.y > -0.5)
                {
                    CompositeFissures(premultiplied, alpha, world, worldDx, worldDy);
                    // Metres to the shore of the lava actually drawn: glow and crust measured from the lava
                    // field alone lit up whole crater walls where the slope rule keeps lava off.
                    float lavaShoreDistance = min((volcanic.y - 1.0) * volcanicMetres.y, slopeBandDistance);
                    float glow = lavaShoreDistance < 0.0
                        ? saturate(1.0 + lavaShoreDistance / max(_LavaGlowReach, 1e-3))
                        : 1.0;
                    Composite(premultiplied, alpha, _LavaGlowColor.rgb, _LavaGlowColor.a * glow);

                    float lavaCoverage = RaggedSheetCoverage(world, 0.5 + lavaShoreDistance / _TileSize);
                    if (lavaCoverage > 0.0)
                    {
                        // Cooled rock, not darkened lava: darkening its yellow would turn it olive.
                        float crust = 1.0 - smoothstep(0.0, 1.2, lavaShoreDistance);
                        float3 lava = lerp(LavaColour(world), _LavaCrustColor.rgb, crust * 0.8);
                        Composite(premultiplied, alpha, lava, lavaCoverage);
                    }
                }

                CompositeContinuousPatches(premultiplied, alpha, world);
                CompositePatches(premultiplied, alpha, world, worldDx, worldDy);

                // Around the mountains: occlusion where the ground meets a wall and the mesh's cast shadow.
                // Ground under the mountain is hidden by the mesh, so skip it. Fallen rocks are their own
                // billboard mesh (Talus.shader) so they can stand in front of the walls.
                if (mountainDistance < 0.0 && mountainDistance > -(_ShadowReach + 1.0))
                {
                    Composite(premultiplied, alpha, float3(0, 0, 0), 0.3 * smoothstep(-1.2, 0.0, mountainDistance));
                    Composite(premultiplied, alpha, float3(0, 0, 0), MountainShadow(world) * _ShadowStrength);
                }

                return OverBaseGround(premultiplied, alpha, world, rockCoverage);
            }
            ENDCG
        }
    }

    Fallback Off
}
