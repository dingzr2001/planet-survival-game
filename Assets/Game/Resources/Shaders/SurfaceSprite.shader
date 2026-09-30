Shader "Planet Survival/Surface Sprite"
{
    // The default sprite shader for things lying flat on the ground (shadows, footprints, decals, the build
    // grid): every vertex is drawn at the height of the drawn ground under it (TerrainSurface.cginc), so they
    // follow a crater floor and wall instead of hovering over them. It lives under Resources so player
    // builds always include it.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
            #pragma vertex SurfaceSpriteVert
            #pragma fragment SpriteFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "Assets/Game/Shaders/TerrainSurface.cginc"

            v2f SurfaceSpriteVert(appdata_t input)
            {
                v2f output = SpriteVert(input);
                float3 world = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex.xyz, _Flip)).xyz;
                world.y += SurfaceHeight(world.xz);
                output.vertex = UnityWorldToClipPos(world);
                return output;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
