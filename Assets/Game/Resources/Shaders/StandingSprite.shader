Shader "Planet Survival/Standing Sprite"
{
    // The default sprite shader, except that each vertex is depth-tested as part of an upright board
    // standing where the sprite meets the ground (see StandingDepth.cginc), and the sprite is drawn at the
    // height of the ground at its foot, so it stands on a crater floor rather than floating over it
    // (TerrainSurface.cginc). _StandingGroundZ and _StandingFoot are set per renderer by WorldSpriteView.
    // It lives under Resources so player builds always include it.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [PerRendererData] _StandingGroundZ ("Standing Ground Z", Float) = 0
        [PerRendererData] _StandingEnabled ("Standing Enabled", Float) = 0
        [PerRendererData] _StandingFoot ("Standing Foot (x, z)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex StandingSpriteVert
            #pragma fragment SpriteFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "Assets/Game/Shaders/StandingDepth.cginc"
            #include "Assets/Game/Shaders/TerrainSurface.cginc"

            float _StandingGroundZ;
            float _StandingEnabled;
            float4 _StandingFoot;

            v2f StandingSpriteVert(appdata_t input)
            {
                v2f output = SpriteVert(input);
                if (_StandingEnabled > 0.5)
                {
                    // Sprite vertices may arrive already in world space when batched, so recover the world
                    // position from the object transform exactly as SpriteVert placed it.
                    float3 world = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex.xyz, _Flip)).xyz;
                    // The whole sprite moves by the height at its foot, so it is not bent across a slope.
                    world.y += SurfaceHeight(_StandingFoot.xy);
                    output.vertex = StandingClipPosition(world, _StandingGroundZ);
                }

                return output;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
