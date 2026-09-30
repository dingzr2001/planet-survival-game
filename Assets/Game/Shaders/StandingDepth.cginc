#ifndef PLANET_SURVIVAL_STANDING_DEPTH_INCLUDED
#define PLANET_SURVIVAL_STANDING_DEPTH_INCLUDED

#include "UnityCG.cginc"

// Sprites face the camera at full height, while real geometry under the steep camera shows only a fraction
// of its height; mountain meshes are therefore built taller so walls read at sprite scale. To depth-test a
// camera-facing sprite against that geometry correctly, each vertex is moved along its own view ray onto
// an upright plane standing where the sprite meets the ground (constant world Z). The pixel lands on the
// same spot of the screen, but its depth is that of an upright object there: in front of every wall behind
// its feet and hidden by any rock in front of them.
float4 StandingClipPosition(float3 worldPosition, float groundContactZ)
{
    float3 forward = -UNITY_MATRIX_V[2].xyz;
    if (abs(forward.z) < 1e-3)
    {
        // A camera looking straight down has no upright planes to speak of.
        return UnityWorldToClipPos(worldPosition);
    }

    float travel = (groundContactZ - worldPosition.z) / forward.z;
    return UnityWorldToClipPos(worldPosition + forward * travel);
}

#endif
