#ifndef PLANET_SURVIVAL_TERRAIN_SURFACE_INCLUDED
#define PLANET_SURVIVAL_TERRAIN_SURFACE_INCLUDED

// The height at which the ground is drawn, for every shader that draws the ground or something standing
// on it. Gameplay stays on the flat plane; only the picture sinks into craters. The craters around the
// player and the crater profile are uploaded by TerrainSurfaceShaderGlobals; the profile mirrors
// CraterRelief on the C# side, which resolves the pointer and camera against the same heights.

#define SURFACE_MAX_CRATERS 32

float4 _SurfaceCraters[SURFACE_MAX_CRATERS]; // centre x, centre z, radius
float _SurfaceCraterCount;
float _SurfaceVerticalScale;
float4 _SurfaceCraterProfile; // depth, rim height, rim width, wall start; all in radii
float _SurfaceCraterReach;

// Height at 'relative' radii from a crater's centre, in radii.
float CraterReliefProfile(float relative)
{
    if (relative >= _SurfaceCraterReach)
    {
        return 0.0;
    }

    float wallStart = _SurfaceCraterProfile.w;
    float bowl = -_SurfaceCraterProfile.x * (1.0 - smoothstep(wallStart, 1.0, relative));
    float rimOffset = (relative - 1.0) / _SurfaceCraterProfile.z;
    return bowl + _SurfaceCraterProfile.y * exp(-rimOffset * rimOffset);
}

// World-space height of the drawn ground at (x, z), built to the mountains' vertical scale. Like the C#
// side, the crater nearest in its own radii decides.
float SurfaceHeight(float2 world)
{
    float nearestRelative = 1e6;
    float nearestRadius = 0.0;
    [loop]
    for (int i = 0; i < SURFACE_MAX_CRATERS; i++)
    {
        if (i >= (int)_SurfaceCraterCount)
        {
            break;
        }

        float4 crater = _SurfaceCraters[i];
        float relative = length(world - crater.xy) / max(crater.z, 1e-3);
        if (relative < nearestRelative)
        {
            nearestRelative = relative;
            nearestRadius = crater.z;
        }
    }

    return CraterReliefProfile(nearestRelative) * nearestRadius * max(_SurfaceVerticalScale, 1.0);
}

#endif
