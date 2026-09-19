# Terrain Control-Map Rendering

## Goal

Terrain rules and presentation remain deterministic and tile-based. Every visible gameplay tile displays one
complete square terrain illustration, while the renderer preserves chunk streaming, layer priority, and exact
dug-tile state without creating one GameObject per tile.

## Responsibilities

- `TerrainTileMap` owns terrain identity, dig progress, and `TileChanged` events. It contains no render
  resources.
- `TerrainControlMapBuilder` samples each layer's signed distance field in world space. It writes up to eight
  independent weights into two RGBA maps and clears every weight inside a fully dug tile.
- `TerrainChunkRenderResources` owns the immutable four-vertex chunk mesh and one material shared by all
  loaded chunks in a streamer.
- `TerrainChunkView` owns only its two control textures and a `MaterialPropertyBlock`. A chunk therefore costs
  one renderer regardless of how many terrain types overlap it.
- `TerrainChunkStreamer` owns shared render resources, rebuilds dirty chunks, and also invalidates neighbours
  when a changed tile touches a chunk boundary.
- `TerrainBlend.shader` samples the control maps once at each gameplay tile's centre, turns coverage into a
  stable tile choice, and maps the complete 0-1 texture range across that tile. Low-priority layers composite
  first so overlaps still resolve to the configured highest-priority terrain.

## Control-map layout

The first four layer weights use RGBA in control map 0; layers five through eight use control map 1. Layer
order is the existing `TerrainPatchSettings` priority order. The configured 128 samples retain an accurate
field for generation, but the shader samples only at logical tile centres so a texture cannot be clipped by
a sub-tile transition.

Every map includes one sample of gutter beyond each chunk edge. Both adjacent chunks evaluate that sample
from the same world coordinate, preventing filtering seams without sharing mutable textures.

## Lifecycle and updates

The streamer creates the shared mesh/material when configured and disposes them when reconfigured or
destroyed. Each view allocates its control textures once and reuses their pixel buffers. Loading a chunk or
receiving a relevant `TileChanged` event regenerates only that chunk's control pixels; no mesh topology or
material instance is rebuilt.

The current eight-layer limit is deliberate: it bounds shader texture samples and keeps configuration errors
visible. Supporting more layers should be driven by a real content requirement, at which point layers can be
grouped by biome or migrated to a texture-array material library.

## Verification

Edit Mode tests cover control weights, independent layers, exact clearing of dug tiles, shared-edge equality
between neighbouring control maps, fixed-quad geometry, per-chunk tile mapping, and the serialized shader
reference required by player builds.
