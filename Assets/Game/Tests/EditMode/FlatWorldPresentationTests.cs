using NUnit.Framework;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class FlatWorldPresentationTests
    {
        private readonly System.Collections.Generic.List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void TerrainView_BuildsOneGroundPlaneAtZeroHeight()
        {
            TerrainGenerationSettings settings = CreateSettings(4, 3, 2f);
            var root = Track(new GameObject("Terrain"));

            root.AddComponent<GridTerrainView>().Build(settings, null);

            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Transform ground = root.transform.GetChild(0);
            Assert.That(ground.name, Is.EqualTo("Flat Ground"));
            Assert.That(ground.position.y, Is.EqualTo(0f).Within(.0001f));
            Assert.That(ground.GetComponent<MeshCollider>(), Is.Not.Null);
            Mesh mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.GreaterThan(100));
            Assert.That(mesh.bounds.size.x, Is.EqualTo(mesh.bounds.size.z).Within(.001f));
            Assert.That(mesh.bounds.size.x, Is.GreaterThanOrEqualTo(2400f));
            Assert.That(GetLongestTriangleEdge(mesh), Is.LessThanOrEqualTo(300f));
        }

        [Test]
        public void TerrainView_RecentersDiscAroundDistantPlayer()
        {
            TerrainGenerationSettings settings = CreateSettings(4, 3, 2f);
            var root = Track(new GameObject("Terrain"));
            GridTerrainView terrainView = root.AddComponent<GridTerrainView>();
            terrainView.Build(settings, null);
            var target = Track(new GameObject("Target"));
            terrainView.SetTarget(target.transform);
            target.transform.position = new Vector3(100f, 0f, 100f);

            typeof(GridTerrainView)
                .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(terrainView, null);

            Transform ground = root.transform.GetChild(0);
            Assert.That(Vector3.Distance(ground.position, target.transform.position), Is.LessThan(12f));
        }

        [Test]
        public void WorldSpriteView_ScalesAndGroundsACenteredSprite()
        {
            var texture = Track(new Texture2D(8, 16));
            var sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 8f, 16f), new Vector2(.5f, .5f), 8f));
            var root = Track(new GameObject("Sprite"));
            root.AddComponent<SpriteRenderer>();

            root.AddComponent<WorldSpriteView>().Configure(sprite, 2f);

            Assert.That(root.transform.localPosition.y, Is.EqualTo(1f).Within(.0001f));
            Assert.That(root.transform.localScale.y, Is.EqualTo(1f).Within(.0001f));
        }

        [Test]
        public void LandingPodExterior_HasArtworkSolidBodyAndSeparateAirlockTrigger()
        {
            var texture = Track(new Texture2D(16, 12));
            var sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 16f, 12f), new Vector2(.5f, .5f), 4f));
            var visuals = Track(ScriptableObject.CreateInstance<WorldVisualSettings>());
            visuals.ConfigureLandingPod(sprite, 5.4f);
            var parent = Track(new GameObject("Parent"));

            GameObject pod = LandingPodExterior.Create(parent.transform, new Vector3(4f, 0f, 2f), visuals);

            _created.Add(pod);
            BoxCollider body = pod.GetComponent<BoxCollider>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.isTrigger, Is.False);
            Assert.That(body.bounds.min.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(body.size.x, Is.GreaterThanOrEqualTo(6f));
            Assert.That(body.size.z, Is.GreaterThanOrEqualTo(4f));

            WorldSpriteView view = pod.GetComponentInChildren<WorldSpriteView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.Renderer.sprite, Is.EqualTo(sprite));
            Assert.That(view.transform.localPosition, Is.EqualTo(Vector3.zero));

            ScenePortal portal = pod.GetComponentInChildren<ScenePortal>();
            Assert.That(portal, Is.Not.Null);
            Assert.That(portal.TargetScene, Is.EqualTo(GameSceneNames.LandingPodCargo));
            Assert.That(portal.GetComponent<Collider>().isTrigger, Is.True);
        }

        [Test]
        public void WorldSpriteView_SelectsFourCameraRelativeDirectionsAndKeepsLastIdleFacing()
        {
            var texture = Track(new Texture2D(16, 16));
            Sprite down = CreateSprite(texture, 0f);
            Sprite left = CreateSprite(texture, 4f);
            Sprite right = CreateSprite(texture, 8f);
            Sprite up = CreateSprite(texture, 12f);
            var root = Track(new GameObject("Sprite"));
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            WorldSpriteView view = root.AddComponent<WorldSpriteView>();
            view.ConfigureDirectional(down, 2f,
                new[] { down }, new[] { left }, new[] { right }, new[] { up });

            view.SetMovement(Vector2.left);
            Assert.That(renderer.sprite, Is.EqualTo(left));
            view.SetMovement(Vector2.right);
            Assert.That(renderer.sprite, Is.EqualTo(right));
            view.SetMovement(Vector2.up);
            Assert.That(renderer.sprite, Is.EqualTo(up));
            view.SetMovement(Vector2.zero);

            Assert.That(renderer.sprite, Is.EqualTo(up));
        }

        [Test]
        public void WorldSpriteView_MapsSheetRowsToTheirVisibleDirections()
        {
            var texture = Track(new Texture2D(16, 16));
            var root = Track(new GameObject("Sprite"));
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            WorldSpriteView view = root.AddComponent<WorldSpriteView>();

            view.ConfigureDirectional(null, 2f, texture);

            view.SetMovement(Vector2.down);
            Assert.That(renderer.sprite.rect.y, Is.EqualTo(12f));
            view.SetMovement(Vector2.right);
            Assert.That(renderer.sprite.rect.y, Is.EqualTo(8f));
            view.SetMovement(Vector2.left);
            Assert.That(renderer.sprite.rect.y, Is.EqualTo(4f));
            view.SetMovement(Vector2.up);
            Assert.That(renderer.sprite.rect.y, Is.EqualTo(0f));
        }

        [Test]
        public void WorldSpriteView_UsesPerFrameFootAnchorsWithoutMovingTheOwner()
        {
            var texture = Track(new Texture2D(16, 16));
            var rects = new Rect[16];
            var pivots = new Vector2[16];
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = new Rect((i % 4) * 4f, (3 - i / 4) * 4f, 4f, 4f);
                pivots[i] = new Vector2(.5f, 0f);
            }

            pivots[4] = new Vector2(.25f, 0f);
            var root = Track(new GameObject("Sprite"));
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            WorldSpriteView view = root.AddComponent<WorldSpriteView>();

            view.ConfigureDirectional(null, 2f, texture, 4, rects, pivots);
            view.SetMovement(Vector2.right);

            Assert.That(renderer.sprite.pivot.x, Is.EqualTo(1f).Within(.001f));
            Assert.That(renderer.sprite.pivot.y, Is.Zero.Within(.001f));
            Assert.That(root.transform.localPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void FollowCamera_TracksTargetWithoutElasticLag()
        {
            var camera = Track(new GameObject("Camera"));
            var target = Track(new GameObject("Target"));
            FollowCamera follow = camera.AddComponent<FollowCamera>();
            follow.SetTarget(target.transform);
            Vector3 firstPosition = camera.transform.position;
            Quaternion firstRotation = camera.transform.rotation;

            var targetDelta = new Vector3(2f, 0f, 3f);
            target.transform.position += targetDelta;
            typeof(FollowCamera)
                .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(follow, null);

            Assert.That(camera.transform.position, Is.EqualTo(firstPosition + targetDelta));
            Assert.That(camera.transform.rotation, Is.EqualTo(firstRotation));
        }

        [Test]
        public void FollowCamera_UsesSteeperFramingAndKeepsPlayerAboveHud()
        {
            var camera = Track(new GameObject("Camera"));
            Camera unityCamera = camera.AddComponent<Camera>();
            unityCamera.fieldOfView = 90f;
            var target = Track(new GameObject("Target"));
            FollowCamera follow = camera.AddComponent<FollowCamera>();

            follow.SetTarget(target.transform);

            Vector3 horizontalForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            float downwardPitch = Vector3.Angle(horizontalForward, camera.transform.forward);
            Vector3 targetViewportPosition = unityCamera.WorldToViewportPoint(target.transform.position);
            Assert.That(downwardPitch, Is.GreaterThanOrEqualTo(18f));
            Assert.That(targetViewportPosition.y, Is.GreaterThanOrEqualTo(.25f));
        }

        [Test]
        public void FollowCamera_AlignsWorldGridAxesWithScreenAxes()
        {
            var camera = Track(new GameObject("Camera"));
            var target = Track(new GameObject("Target"));
            FollowCamera follow = camera.AddComponent<FollowCamera>();

            follow.SetTarget(target.transform);

            Vector3 groundForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 groundRight = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            Assert.That(groundForward.x, Is.Zero.Within(.0001f),
                "World Z grid lines should project vertically instead of diagonally.");
            Assert.That(groundRight.z, Is.Zero.Within(.0001f),
                "World X grid lines should project horizontally instead of diagonally.");
        }

        [Test]
        public void HorizonBackdrop_IsCameraRelativeAndCoversPerspectiveView()
        {
            var cameraObject = Track(new GameObject("Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 78f;
            camera.farClipPlane = 1100f;
            var texture = Track(new Texture2D(16, 9));
            var sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 16f, 9f), new Vector2(.5f, .5f), 1f));

            HorizonBackdrop backdrop = HorizonBackdrop.Create(camera, sprite);
            _created.Add(backdrop.gameObject);

            Assert.That(backdrop.transform.parent, Is.EqualTo(camera.transform));
            Assert.That(backdrop.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(backdrop.transform.localPosition.z, Is.GreaterThan(0f));
            Assert.That(backdrop.transform.localPosition.y, Is.EqualTo(0f).Within(.0001f));
            Assert.That(backdrop.transform.localPosition.z, Is.LessThan(camera.farClipPlane));
            Assert.That(backdrop.GetComponent<SpriteRenderer>().sprite, Is.EqualTo(sprite));
        }

        private TerrainGenerationSettings CreateSettings(int width, int length, float cellSize)
        {
            var settings = Track(ScriptableObject.CreateInstance<TerrainGenerationSettings>());
            settings.Configure(width, length, cellSize, 42);
            return settings;
        }

        private static float GetLongestTriangleEdge(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            float longestEdge = 0f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                longestEdge = Mathf.Max(longestEdge,
                    Vector3.Distance(a, b),
                    Vector3.Distance(b, c),
                    Vector3.Distance(c, a));
            }

            return longestEdge;
        }

        private Sprite CreateSprite(Texture2D texture, float x)
        {
            return Track(Sprite.Create(texture, new Rect(x, 0f, 4f, 16f), new Vector2(.5f, .5f), 8f));
        }

        private T Track<T>(T instance) where T : Object
        {
            _created.Add(instance);
            return instance;
        }
    }
}
