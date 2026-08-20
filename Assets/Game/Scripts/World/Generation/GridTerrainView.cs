using PlanetSurvival.World.Grid;
using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [DisallowMultipleComponent]
    public sealed class GridTerrainView : MonoBehaviour
    {
        private static readonly Color LowColor = new(0.35f, 0.18f, 0.12f);
        private static readonly Color HighColor = new(0.72f, 0.38f, 0.22f);

        public void Build(GridMap map, TerrainGenerationSettings settings)
        {
            var material = new Material(Shader.Find("Standard"));
            var propertyBlock = new MaterialPropertyBlock();

            foreach (GridCell cell in map.GetCells())
            {
                GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Cell_{cell.Coordinate.X}_{cell.Coordinate.Z}";
                tile.transform.SetParent(transform, false);

                float totalHeight = cell.UndergroundDepth + cell.SurfaceHeight;
                tile.transform.localScale = new Vector3(settings.CellSize, totalHeight, settings.CellSize);
                tile.transform.localPosition = new Vector3(
                    cell.Coordinate.X * settings.CellSize,
                    cell.SurfaceHeight - totalHeight * 0.5f,
                    cell.Coordinate.Z * settings.CellSize);

                var renderer = tile.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                float heightRange = Mathf.Max(0.001f, settings.HeightAmplitude);
                propertyBlock.SetColor("_Color", Color.Lerp(LowColor, HighColor, cell.SurfaceHeight / heightRange));
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
