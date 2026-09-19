using NUnit.Framework;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Building.Runtime;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class BuildGridOverlayTests
    {
        private GameObject _cameraObject;
        private GameObject _overlayObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_overlayObject);
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void Bind_StartsHiddenAndCanBeToggledVisible()
        {
            BuildGridOverlay overlay = CreateOverlay(new BuildGrid());
            MeshRenderer renderer = overlay.GetComponent<MeshRenderer>();

            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(renderer.enabled, Is.False);

            overlay.Toggle();

            Assert.That(overlay.IsVisible, Is.True);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(overlay.ToggleKey, Is.EqualTo(KeyCode.G));
        }

        [Test]
        public void ShowingOverlay_BuildsLinesOnTheBoundariesOfTheBoundGrid()
        {
            var grid = new BuildGrid(2f, new Vector3(.5f, 0f, -.5f));
            BuildGridOverlay overlay = CreateOverlay(grid);

            overlay.SetVisible(true);

            Mesh mesh = overlay.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.GetTopology(0), Is.EqualTo(MeshTopology.Lines));
            Assert.That(mesh.vertexCount, Is.GreaterThan(4));
            foreach (Vector3 vertex in mesh.vertices)
            {
                float xCell = (vertex.x - grid.Origin.x) / grid.CellSize;
                float zCell = (vertex.z - grid.Origin.z) / grid.CellSize;
                bool liesOnVerticalBoundary = Mathf.Abs(xCell - Mathf.Round(xCell)) < .0001f;
                bool liesOnHorizontalBoundary = Mathf.Abs(zCell - Mathf.Round(zCell)) < .0001f;
                Assert.That(liesOnVerticalBoundary || liesOnHorizontalBoundary, Is.True,
                    $"Vertex {vertex} did not lie on a build-cell boundary.");
            }
        }

        private BuildGridOverlay CreateOverlay(BuildGrid grid)
        {
            _cameraObject = new GameObject("Grid Test Camera");
            Camera camera = _cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.aspect = 1.6f;
            _cameraObject.transform.position = new Vector3(3f, 10f, -5f);
            _cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            _overlayObject = new GameObject("Grid Test Overlay");
            BuildGridOverlay overlay = _overlayObject.AddComponent<BuildGridOverlay>();
            overlay.Bind(grid, camera);
            return overlay;
        }
    }
}
