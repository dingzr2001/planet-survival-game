# Terrain Rendering

## Goal

The surface should read as continuous ground, not as a grid, and mountains should read as solid rock that
stands up out of it. Landforms (mountains, basins, craters, ice lakes) follow one continuous elevation field;
patch artwork (rock, ore, sheet ice) hides the 2.75 m gameplay tile lattice, while gameplay stays exact: the
art over a tile is always what digging it yields, and a dug tile clears.

Each streamed chunk draws three things: the ground quad, a real 3D mountain mesh, and a billboard mesh of
fallen rocks along the cliff foot.

## Responsibilities

- `TerrainTileMap` owns terrain identity, dig progress, landform queries and `TileChanged` events. It holds no
  render resources.
- `TerrainControlMapBuilder` fills two per-chunk textures:
  - **Landform** (`RGBAHalf`, bilinear). R = normalised elevation (0 is the ice-lake shoreline, 1 the foot of
    the mountains). G = true mountain height in metres. B = signed distance to the mountain border (positive
    inside). A = distance to the nearest crater's centre in its radii, capped at 3. Half precision places
    borders to a few centimetres.
  - **Volcanic** (`RGHalf`, bilinear, laid out like the landform map). R = rock ground, 0 on its border with
    the regolith. G = lava activity, 1 on a lava shore, always below 0 off rock ground.
  - **Patch ids** (`RGBA32`, point): one texel per tile. R = owning patch layer + 1, G = depth inside its patch
    (artwork shrinks towards a rim), B = dug flag.
- `MountainHeightProfile` turns border distance and elevation into height. The foot alternates along the
  edge between rock cliffs (full height within 0.6 m) and scree (about two thirds of the height over 3.2 m,
  near 50°). Spurs pull it towards cliff and gullies towards scree, and a fine octave rags the lip. One even
  wall around the massif read as a cake: a box with a lid. Behind the foot:
  - The massif swells with the landform elevation above the mountain level (`MassifRise`).
  - A warped, per-octave-rotated ridged multifractal adds crests and valleys (`RidgeHeight`,
    `RidgeSpacing`). Gradient noise is zero on its lattice, so unwarped ridges crest on a square grid.
  - A smooth slope cap (`MaximumReliefSlope`) keeps the rise gentle right behind the brink.

  The foot is buried a few centimetres so the mesh never floats over the ground.
- `MountainMeshBuilder` builds one mesh per chunk from the same distance grid (2 vertices per metre), keeping
  only quads that reach inside a mountain.
- `TalusMeshBuilder` scatters loose rocks, at most one per 1 m cell (hash-jittered), sorted far to near.
  Fallen rocks lie in a band of `TalusWidth` in front of the mountain foot, densest against the wall.
  Smaller ejecta rocks lie wherever the crater mark is positive, as likely as the mark is thick.
- `TerrainChunkRenderResources` owns the ground, mountain and talus materials, the chunk quad, and a
  `Tex2DArray` render texture holding every patch layer's artwork (512² slices) followed by the talus stones.
- `TerrainChunkView` owns its two control textures, its `MaterialPropertyBlock`, and the child objects
  "Mountains" and "Fallen Rocks". It rebuilds both meshes whenever the landform map is refilled.
- `TerrainChunkStreamer` owns the shared resources, rebuilds dirty chunks and their neighbours, and adds a
  `TerrainChunkObstacles` component that builds merged mountain colliders per chunk.

## Shaders

- `TerrainBlend.shader` (ground) composites, in premultiplied alpha over the base regolith disc: relief
  shading and basin tint → ice lakes → continuous patches → scattered patches → contact occlusion and the
  mountain's cast shadow. The shadow marches 8 steps towards the light through the G channel, so it always
  lines up with the mesh built from the same heights.
- `Mountain.shader`: opaque, depth-writing. Flat parts take `MountainTop` projected from above; steep parts
  take `MountainCliff` projected from the side along X and Z, so bedding stays horizontal on every wall.
  Lighting is diffuse from the shared `LIGHT_DIRECTION`, not Unity lights, to match the unlit sprites.
  The cliff texture only shows on true rock faces, so scree keeps the top's rubble.
  Seen from almost straight above, true slopes barely change the light. Slopes are therefore lit
  `ReliefShading` times steeper than they are, like a hillshade z-factor. Hollows darken by how far the rock
  1.5 m around stands above the point.

### Drawn ground height (sunken craters)

Gameplay stays on the flat plane: collisions, placement rules, pathing and digging are unchanged. Only the
picture sinks into craters:

- **Shape and data**: `CraterRelief` is the crater profile in radii. It has a flat floor 20 % of the radius
  deep, a steep inner wall from 0.7 of the radius, and a narrow rim.
  - `TerrainSurface` turns it into drawn heights, built at the mountains' vertical scale.
  - The chunk streamer uploads the craters around the player to every shader
    (`TerrainSurfaceShaderGlobals`, `TerrainSurface.cginc`).
  - Shallower craters were tried: from almost straight above, walls of a few degrees showed no pit at
    all.
- **Ground**: chunks are a shared 0.5 m grid sunk in the vertex shader. They draw the base regolith
  themselves, opaque, because the far disc (`ContinuousTerrainView`) now lies `DiscDepth` below so it can
  never hide a sunken chunk.
- **Things standing or lying on the ground**:
  - Standing sprites sink by the height at their foot (`_StandingFoot`, set by `WorldSpriteView`).
  - Loose rocks sink by the height at theirs.
  - Flat sprites and the build grid use `SurfaceSprite`, which sinks every vertex; grid lines are split
    per cell.
  - Mountain feet follow the ground, so a mountain beside a crater has no gap under its edge.
- **Pointer and camera**: `GroundSurfaceRaycast` resolves the pointer against the drawn ground by a few
  plane iterations. Building clicks shift the ray so it meets colliders where their buildings are drawn.
  The camera follows the player's drawn height.

### Craters

From the crater distance and its gradient (pointing away from the centre), the ground shader gives every
crater the same drawn profile in radii: a flat floor, a steep inner wall from 0.6, a narrow rim and a
gentle outer slope. It lights that profile, so the inner wall turned away from the light is a dark
crescent, the opposite wall is lit, and the rim is a crisp edge. A crater with only the blurred elevation
relief and the basin tint read as a soft, even dark ring, so both are faded out inside craters.

On top of that the shader draws:

- floor dust with the tinted `CraterFloor` texture, fading out up the wall;
- a lighter ejecta blanket outside the rim.

`CraterMarkings` computes the same floor and ejecta radii on the C# side, adding rays, to scatter ejecta
rocks.

### Rock ground and lava

- **Rock ground**: very large expanses (about 1.4 km features, 30 % of the ground) of bare rock that replace
  the regolith as the base ground. `OverBaseGround` lerps the regolith towards the untiled `RockGround`
  texture, so every other layer (relief, basins, craters, patches) lies on top of either one alike. The
  border is pushed around by broad lobes and a fine fray so it reads as regolith drifted over rock, not as a
  contour. Rock ground never holds ice. Gameplay is unchanged: it is walkable, buildable and digs like
  regolith.
- **Where lava lies**: a finer lava field (about 170 m clusters) surfaces freely in the inner 60 % of rock
  ground and is pressed down across the rim, hard enough that nothing is left at the border with regolith.
  Lava and fissures therefore never appear on regolith. Capping lava at the rock-ground field instead cut
  lakes along that field's nearly straight contour: a ruled lake edge with a glowing line beside it.
- **Landing-site clearance**: each seed shifts the rock-ground noise to the first of 32 candidate offsets
  that leaves the clearance radius on regolith (a hard press remains only as a fallback). Pressing the field
  down radially made every rock and lava edge within the ramp follow its circle, which reads as ruled lines.
- **Glow and crust** are measured from the shore of the lava actually drawn, crater slopes included;
  measured from the lava field alone they lit whole crater walls where the slope rule keeps lava off.
- **Lava lakes**: the shader turns the lava field into metres from the shore through its gradient, the
  same way it treats ice. Lava lakes therefore get the same ragged shore and the same exclusion from crater
  slopes. The surface is the seamless `Lava` texture, drifting slowly as two offset layers. It crusts over
  towards the shore (`LavaCrustColor`, a rock colour: darkening the lava's yellow turned it olive) and glows
  on the ground out to `LavaGlowReach`.
- **Fissures**: the six `LavaFissure` cutouts, cropped from one sheet, are appended to the artwork array.
  At most one lies in each 6 m cell, turned and sized at random, crowding towards the lava. They are purely
  decorative.
- **Night**: day and night only change Unity lights, which the unlit ground ignores, so lava needs no
  separate night glow.

### Relief lessons

- Anything that rises with distance from the edge follows the outline. Behind a south wall it faces away
  from the light, and the camera stretches a slope facing it by its own height. Such a ramp shows as a wide
  dark strip, a "second wall". The ramp is kept gentle, and ridges run up to the brink so the rim breaks into
  spurs and gullies.
- Cast shadows across mountain tops were tried. With exaggerated heights they made black blobs, so the tops
  have diffuse and cavity shading only; the ground keeps the cast shadow.
- `Talus.shader`: each rock is a camera-facing quad grown in the vertex shader from its foot position.
- `TerrainCommon.cginc` holds the light direction, hashes, value noise and the un-tiling sampler.

### Vertical exaggeration

The camera looks down at 75°, so a wall of true height h covers only h·cos 75° on screen. Sprites are drawn
upright facing the camera, which makes a 1.8 m character look far taller than a 2.4 m wall. Mountain meshes
are therefore built `VerticalExaggeration` = 1 / cos 75° ≈ 3.86 times taller. The mountain shader undoes the
scale for normals, texture heights and occlusion, so it shades as the nominal shape.

### Standing sprites and depth

Sprites and rocks are billboards; mountains are real geometry. For them to occlude each other correctly, every
billboard is depth-tested as if it were an upright board standing at the point where its image meets the
ground. `StandingDepth.cginc` slides each vertex along its view ray onto that plane. Only depth changes; the
image stays where it was drawn. As a result a player in front of a wall is drawn over it, and a player behind
a mountain is hidden by it.

- `StandingSprite.shader` (under `Resources/Shaders`, so every build includes it) is the sprite version.
  `StandingSpriteMaterial.Apply` swaps it in only for renderers still on `Sprites/Default`. Custom materials
  are left alone.
- `WorldSpriteView` sets `_StandingGroundZ` per renderer each `LateUpdate` from the bottom of its bounds.
- `FollowCamera` pulls an orthographic camera 60 m back along its view (`_orthographicClearance`). The picture
  is unchanged, but the tall meshes no longer cross the near clip plane.

### Rejected approaches

The first versions drew cliffs in the ground shader: a south-facing face painted on the mountain's own
ground, with a lip highlight, height noise and fallen rocks composited on top. These were abandoned because
they never read as solid:

- A painted face cannot occlude a sprite standing behind it.
- Its silhouette follows the texel field rather than a shape.
- Every fix (varying height, broken lip, terraces, talus) added another layer of fakery.

Drawing the face south of the border was also tried; it painted a wall over walkable ground, so the player
appeared to stand inside the cliff.

## Patch artwork

Each surface declares a `TerrainPatchRendering` mode:

- **Scattered** (cutout rock and ore): each tile around the pixel paints its artwork once, with a hashed
  offset (±35 %) and size (0.9–1.2×, shrunk towards the patch rim). Neighbours may overlap, northern tiles
  first, so the tile grid disappears.
- **Continuous** (sheet ice): tile ownership is blended bilinearly between tile centres and thresholded with
  noise, which rounds a run of tiles into one sheet with a ragged rim. The seamless texture is tiled in world
  space and blended with a second, offset copy of itself to hide the repeat.
  Landform ice lakes use the same ragged edge and texture: their shore distance is turned into the membership
  a patch would have, so lakes and sheet-ice patches look like one kind of ice. A smooth, frost-rimmed lake
  shore next to ragged patches read as two different things.

The three rock grades (`RockScree`, `RockBroken`, `RockBoulders`) are composed from the same `TalusRock1-5`
cutouts as the fallen rocks, so plains rubble and cliff rubble share one style. The older cartoon `Stone1-3`
art is kept but no longer referenced.

Opaque square illustrations with the ground baked in cannot be drawn either way without showing the grid;
supply a cutout (scattered) or a seamless texture (continuous) instead.

## Gutters

The landform map extends `ceil(SampleReach / texel) + 1` texels beyond the chunk, where the reach covers the
shadow length, the talus band and the edge roughness. The patch map extends one tile. Both chunks sharing an
edge sample the gutter from the same world coordinates, so filtering, neighbour look-ups and mesh edges never
seam.

## Lifecycle and updates

The streamer creates the shared resources when configured and disposes them when reconfigured or destroyed;
the patch artwork array is released with them. Each view allocates its control textures once and owns its two
meshes. Loading a chunk or a relevant `TileChanged` event refills only that chunk's textures. Mountain meshes
and colliders are built once per chunk load, because mountains cannot be dug.

## Limits and costs

- Up to 16 patch layers (uniform slice tables in the shader).
- Without a graphics device (headless editor) the artwork array cannot be created; the terrain still draws
  landforms and mountains, and patches and fallen rocks draw nothing.
- Per-renderer property blocks on standing sprites break sprite batching. This is acceptable at the current
  sprite count; revisit with the Profiler if it grows.

## Verification

Edit Mode tests cover:

- The quad and sorting, and the artwork slice tables including the talus slices.
- The half-precision landform map, and that maps agree on shared edges between neighbouring chunks.
- Point-filtered patch ids that name exactly the gameplay layer, rim depth, and dug flags.
- The height profile, mountain mesh coverage and height, the talus band, and the standing-sprite material.
- That the ground, mountain, talus and standing-sprite shaders all compile without errors.
