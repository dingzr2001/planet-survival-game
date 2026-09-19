using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>Shared immutable mesh and material used by every loaded terrain chunk.</summary>
    public sealed class TerrainChunkRenderResources : IDisposable
    {
        public const int DefaultControlMapResolution = 128;
        public const float DefaultBlendDistance = 1.2f;
        public const string ShaderName = "Planet Survival/Terrain Blend";

        public TerrainChunkRenderResources(IReadOnlyList<TerrainPatchLayer> layers, float chunkSize,
            Shader shader = null, int controlMapResolution = DefaultControlMapResolution,
            float blendDistance = DefaultBlendDistance)
        {
            ControlMapResolution = Mathf.Max(16, controlMapResolution);
            BlendDistance = Mathf.Max(.05f, blendDistance);
            TextureSize = TerrainControlMapBuilder.TextureSizeFor(ControlMapResolution);
            Mesh = CreateChunkMesh(Mathf.Max(.1f, chunkSize));

            Shader resolvedShader = shader != null ? shader : Shader.Find(ShaderName);
            if (resolvedShader == null)
            {
                Debug.LogError($"Terrain shader '{ShaderName}' could not be found.");
                return;
            }

            Material = new Material(resolvedShader) { name = "Runtime Terrain Blend" };
            int layerCount = layers != null
                ? Mathf.Min(layers.Count, TerrainControlMapBuilder.MaximumLayerCount)
                : 0;
            Material.SetFloat("_LayerCount", layerCount);
            int variantSurfaceCount = 0;
            for (int i = 0; i < TerrainControlMapBuilder.MaximumLayerCount; i++)
            {
                TerrainSurfaceDefinition surface = i < layerCount ? layers[i].Surface : null;
                Material.SetTexture($"_Layer{i}", surface != null && surface.Texture != null
                    ? surface.Texture
                    : Texture2D.whiteTexture);
                Material.SetFloat($"_Layer{i}Scale", surface != null ? 1f / surface.TextureTileSize : 1f);

                if (surface != null && surface.TextureVariantCount > 1)
                {
                    if (variantSurfaceCount == 0)
                    {
                        Material.SetFloat("_VariantLayerIndex", i);
                        Material.SetFloat("_VariantCount", surface.TextureVariantCount);
                        Material.SetTexture("_Variant1", surface.GetTextureVariant(1));
                        Material.SetTexture("_Variant2", surface.GetTextureVariant(2));
                    }
                    else if (variantSurfaceCount == 1)
                    {
                        Material.SetFloat("_SecondVariantLayerIndex", i);
                        Material.SetFloat("_SecondVariantCount", surface.TextureVariantCount);
                        Material.SetTexture("_SecondVariant1", surface.GetTextureVariant(1));
                        Material.SetTexture("_SecondVariant2", surface.GetTextureVariant(2));
                    }
                    else
                    {
                        Debug.LogWarning(
                            "Terrain rendering supports texture variants on two layers; "
                            + $"layer {i} will use only its primary texture.");
                    }

                    variantSurfaceCount++;
                }
            }

            if (variantSurfaceCount == 0)
            {
                Material.SetFloat("_VariantLayerIndex", -1f);
                Material.SetFloat("_VariantCount", 1f);
            }

            if (variantSurfaceCount < 2)
            {
                Material.SetFloat("_SecondVariantLayerIndex", -1f);
                Material.SetFloat("_SecondVariantCount", 1f);
            }

            if (layers != null && layers.Count > TerrainControlMapBuilder.MaximumLayerCount)
            {
                Debug.LogWarning(
                    $"Terrain rendering supports {TerrainControlMapBuilder.MaximumLayerCount} layers; "
                    + $"{layers.Count - TerrainControlMapBuilder.MaximumLayerCount} lower-priority layers will not draw.");
            }
        }

        public Mesh Mesh { get; }
        public Material Material { get; }
        public int ControlMapResolution { get; }
        public int TextureSize { get; }
        public float BlendDistance { get; }
        public bool IsValid => Mesh != null && Material != null;

        public void Dispose()
        {
            DestroyRuntimeObject(Material);
            DestroyRuntimeObject(Mesh);
        }

        private static Mesh CreateChunkMesh(float size)
        {
            var mesh = new Mesh { name = "Shared Terrain Chunk Quad" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(size, 0f, 0f),
                new Vector3(size, 0f, size),
                new Vector3(0f, 0f, size)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
