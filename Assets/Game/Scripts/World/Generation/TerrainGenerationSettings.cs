using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [CreateAssetMenu(menuName = "Planet Survival/World/Terrain Generation Settings")]
    public sealed class TerrainGenerationSettings : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("Width of the starting area in cells. Only sets where the player spawns and how the ground disc is centred; the world itself is endless.")]
        private int _width = 24;
        [SerializeField, Min(1), Tooltip("Length of the starting area in cells. See the width tooltip.")]
        private int _length = 24;
        [SerializeField, Min(0.1f), Tooltip("World units per cell.")]
        private float _cellSize = 1f;
        [SerializeField, Tooltip("World seed. Resource layouts derive their own seed from it.")]
        private int _seed = 8128;

        public int Width => _width;
        public int Length => _length;
        public float CellSize => _cellSize;
        public int Seed => _seed;

        public void Configure(int width, int length, float cellSize, int seed)
        {
            _width = Mathf.Max(1, width);
            _length = Mathf.Max(1, length);
            _cellSize = Mathf.Max(0.1f, cellSize);
            _seed = seed;
        }
    }
}
