#ifndef PLANET_SURVIVAL_TERRAIN_COMMON_INCLUDED
#define PLANET_SURVIVAL_TERRAIN_COMMON_INCLUDED

// Shared by the ground and mountain shaders so both are lit from one direction: the upper left of the
// screen, where the painted sprites take their light from. The scene's directional light turns with the day
// and lights nothing else, so the terrain does not follow it either. The light rises at about 45°, which
// the mountain shadow reach on the C# side relies on.
#define LIGHT_DIRECTION normalize(float3(-0.55, 0.7, 0.45))

float _VariantSeed;

uint Hash(int2 cell, uint salt)
{
    uint h = (uint)cell.x * 1597334677u ^ (uint)cell.y * 3812015801u ^ (salt + (uint)_VariantSeed) * 2654435769u;
    h ^= h >> 16;
    h *= 0x7feb352du;
    h ^= h >> 15;
    h *= 0x846ca68bu;
    h ^= h >> 16;
    return h;
}

float Hash01(int2 cell, uint salt)
{
    return (Hash(cell, salt) & 0xffffffu) / 16777216.0;
}

// Smooth value noise in [0, 1], used to roughen borders and vary shading.
float ValueNoise(float2 position)
{
    int2 cell = (int2)floor(position);
    float2 local = position - floor(position);
    float2 fade = local * local * (3.0 - 2.0 * local);
    float a = Hash01(cell, 11u);
    float b = Hash01(cell + int2(1, 0), 11u);
    float c = Hash01(cell + int2(0, 1), 11u);
    float d = Hash01(cell + int2(1, 1), 11u);
    return lerp(lerp(a, b, fade.x), lerp(c, d, fade.x), fade.y);
}

// Seamless textures still betray their repeat as faint lines and recurring features. Blending the texture
// with a shifted, rescaled copy of itself under a slow noise breaks both up. The copy is never rotated:
// painted rock carries baked lighting and bedding that must keep their direction.
float2 UntiledSecondUv(float2 uv)
{
    return uv * 0.83 + float2(0.37, 0.61);
}

float UntiledBlend(float2 uv)
{
    return smoothstep(0.3, 0.7, ValueNoise(uv * 1.3));
}

float3 SampleUntiled(sampler2D tex, float2 uv)
{
    return lerp(tex2D(tex, uv).rgb, tex2D(tex, UntiledSecondUv(uv)).rgb, UntiledBlend(uv));
}

#endif
