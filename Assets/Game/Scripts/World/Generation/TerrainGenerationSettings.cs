using UnityEngine.Serialization;
using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [CreateAssetMenu(menuName = "Planet Survival/World/Terrain Generation Settings")]
    public sealed class TerrainGenerationSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Size of the starting area in world units. It centres the initial player position and sets the minimum logical ground diameter; the streamed world itself is endless.")]
        private Vector2 _startingAreaSize = new(24f, 24f);
        [SerializeField, Tooltip("World seed. Resource layouts derive their own seed from it.")]
        private int _seed = 8128;

        // These fields only exist to upgrade terrain assets authored before terrain dimensions became
        // continuous world-space values. They are cleared as soon as Unity deserializes the old data.
        [SerializeField, HideInInspector, FormerlySerializedAs("_width")]
        private int _legacyWidth;
        [SerializeField, HideInInspector, FormerlySerializedAs("_length")]
        private int _legacyLength;
        [SerializeField, HideInInspector, FormerlySerializedAs("_cellSize")]
        private float _legacyCellSize;

        public Vector2 StartingAreaSize => new(
            Mathf.Max(.1f, _startingAreaSize.x),
            Mathf.Max(.1f, _startingAreaSize.y));

        public Vector3 StartingAreaCenter => new(StartingAreaSize.x * .5f, 0f, StartingAreaSize.y * .5f);
        public int Seed => _seed;

        public void Configure(Vector2 startingAreaSize, int seed)
        {
            _startingAreaSize = new Vector2(
                Mathf.Max(.1f, startingAreaSize.x),
                Mathf.Max(.1f, startingAreaSize.y));
            _seed = seed;
            ClearLegacyDimensions();
        }

        private void OnValidate()
        {
            UpgradeLegacyDimensions();
            _startingAreaSize = StartingAreaSize;
        }

        private void OnEnable()
        {
            UpgradeLegacyDimensions();
        }

        private void UpgradeLegacyDimensions()
        {
            if (_legacyWidth <= 0 || _legacyLength <= 0)
            {
                return;
            }

            float cellSize = Mathf.Max(.1f, _legacyCellSize);
            _startingAreaSize = new Vector2(_legacyWidth * cellSize, _legacyLength * cellSize);
            ClearLegacyDimensions();
        }

        private void ClearLegacyDimensions()
        {
            _legacyWidth = 0;
            _legacyLength = 0;
            _legacyCellSize = 0f;
        }
    }
}
