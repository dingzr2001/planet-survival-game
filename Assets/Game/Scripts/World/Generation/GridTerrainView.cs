using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [DisallowMultipleComponent]
    public sealed class GridTerrainView : MonoBehaviour
    {
        private const int DiscSegments = 128;
        private const float MaximumRadialStep = 250f;
        private const float MinimumRadius = 1200f;
        private const float RecenterStep = 16f;

        [SerializeField, HideInInspector] private Transform _ground;
        [SerializeField, HideInInspector] private Transform _target;
        private Material _material;
        private Mesh _mesh;
        [SerializeField, HideInInspector] private float _tileSize;

        private void OnEnable()
        {
            RestoreRuntimeReferences();
            EnsureGroundCoverage();
        }

        public void Build(TerrainGenerationSettings settings, WorldVisualSettings visuals)
        {
            var ground = new GameObject();
            ground.name = "Flat Ground";
            ground.transform.SetParent(transform, false);
            _ground = ground.transform;
            _ground.localPosition = new Vector3(
                (settings.Width - 1) * settings.CellSize * .5f,
                0f,
                (settings.Length - 1) * settings.CellSize * .5f);

            float logicalDiameter = Mathf.Max(settings.Width, settings.Length) * settings.CellSize;
            float radius = Mathf.Max(MinimumRadius, logicalDiameter);
            _tileSize = visuals != null ? visuals.GroundTileSize : 8f;
            _mesh = CreateDisc(radius, _tileSize);
            ground.AddComponent<MeshFilter>().sharedMesh = _mesh;
            ground.AddComponent<MeshCollider>().sharedMesh = _mesh;

            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            _material = new Material(shader) { name = "Runtime Martian Ground" };
            _material.mainTexture = visuals != null ? visuals.GroundTexture : null;
            _material.color = visuals != null ? visuals.GroundTint : new Color(.55f, .24f, .1f);
            ground.AddComponent<MeshRenderer>().sharedMaterial = _material;
            UpdateTextureOffset();
        }

        private void RestoreRuntimeReferences()
        {
            if (_ground == null)
            {
                _ground = transform.Find("Flat Ground");
            }

            if (_ground == null)
            {
                return;
            }

            _mesh = _ground.GetComponent<MeshFilter>()?.sharedMesh;
            _material = _ground.GetComponent<MeshRenderer>()?.sharedMaterial;
            if (_tileSize <= 0f)
            {
                _tileSize = 8f;
            }
        }

        private void EnsureGroundCoverage()
        {
            if (_ground == null || _mesh == null)
            {
                return;
            }

            float currentDiameter = _mesh.bounds.size.x * _ground.localScale.x;
            float requiredDiameter = MinimumRadius * 2f;
            if (currentDiameter >= requiredDiameter)
            {
                return;
            }

            float scaleMultiplier = requiredDiameter / currentDiameter;
            Vector3 scale = _ground.localScale;
            _ground.localScale = new Vector3(scale.x * scaleMultiplier, scale.y, scale.z * scaleMultiplier);
            if (_material != null)
            {
                _material.mainTextureScale *= scaleMultiplier;
            }
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void LateUpdate()
        {
            if (_target == null || _ground == null)
            {
                return;
            }

            Vector3 delta = _target.position - _ground.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < RecenterStep * RecenterStep)
            {
                return;
            }

            _ground.position = new Vector3(
                Mathf.Round(_target.position.x / RecenterStep) * RecenterStep,
                0f,
                Mathf.Round(_target.position.z / RecenterStep) * RecenterStep);
            UpdateTextureOffset();
        }

        private void UpdateTextureOffset()
        {
            if (_material == null || _ground == null)
            {
                return;
            }

            _material.mainTextureOffset = new Vector2(_ground.position.x / _tileSize, _ground.position.z / _tileSize);
        }

        private static Mesh CreateDisc(float radius, float tileSize)
        {
            int ringCount = Mathf.Max(1, Mathf.CeilToInt(radius / MaximumRadialStep));
            int verticesPerRing = DiscSegments + 1;
            var vertices = new Vector3[1 + ringCount * verticesPerRing];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[DiscSegments * 3 + (ringCount - 1) * DiscSegments * 6];
            float safeTileSize = Mathf.Max(.1f, tileSize);
            int triangleIndex = 0;

            for (int ring = 1; ring <= ringCount; ring++)
            {
                float ringRadius = radius * ring / ringCount;
                int ringStart = 1 + (ring - 1) * verticesPerRing;
                for (int segment = 0; segment <= DiscSegments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / DiscSegments;
                    var vertex = new Vector3(
                        Mathf.Cos(angle) * ringRadius,
                        0f,
                        Mathf.Sin(angle) * ringRadius);
                    int vertexIndex = ringStart + segment;
                    vertices[vertexIndex] = vertex;
                    uv[vertexIndex] = new Vector2(vertex.x / safeTileSize, vertex.z / safeTileSize);
                }

                for (int segment = 0; segment < DiscSegments; segment++)
                {
                    int current = ringStart + segment;
                    int next = current + 1;
                    if (ring == 1)
                    {
                        triangles[triangleIndex++] = 0;
                        triangles[triangleIndex++] = next;
                        triangles[triangleIndex++] = current;
                        continue;
                    }

                    int previous = current - verticesPerRing;
                    int previousNext = next - verticesPerRing;
                    triangles[triangleIndex++] = previous;
                    triangles[triangleIndex++] = previousNext;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = previous;
                    triangles[triangleIndex++] = next;
                    triangles[triangleIndex++] = current;
                }
            }

            var mesh = new Mesh { name = "Runtime Endless Ground Disc" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                DestroyRuntimeObject(_material);
            }

            if (_mesh != null)
            {
                DestroyRuntimeObject(_mesh);
            }
        }

        private static void DestroyRuntimeObject(Object instance)
        {
            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
    }
}
