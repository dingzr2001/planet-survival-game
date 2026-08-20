using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [CreateAssetMenu(menuName = "Planet Survival/World/Terrain Generation Settings")]
    public sealed class TerrainGenerationSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int _width = 24;
        [SerializeField, Min(1)] private int _length = 24;
        [SerializeField, Min(0.1f)] private float _cellSize = 1f;
        [SerializeField, Min(0.1f)] private float _undergroundDepth = 2f;
        [SerializeField, Min(0.001f)] private float _noiseScale = 0.08f;
        [SerializeField, Min(0f)] private float _heightAmplitude = 4f;
        [SerializeField] private int _seed = 8128;

        public int Width => _width;
        public int Length => _length;
        public float CellSize => _cellSize;
        public float UndergroundDepth => _undergroundDepth;
        public float NoiseScale => _noiseScale;
        public float HeightAmplitude => _heightAmplitude;
        public int Seed => _seed;

        public void Configure(int width, int length, float cellSize, float undergroundDepth, int seed)
        {
            _width = Mathf.Max(1, width);
            _length = Mathf.Max(1, length);
            _cellSize = Mathf.Max(0.1f, cellSize);
            _undergroundDepth = Mathf.Max(0.1f, undergroundDepth);
            _seed = seed;
        }
    }
}
