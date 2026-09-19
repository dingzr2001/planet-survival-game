using NUnit.Framework;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Player.Animation;
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
            TerrainGenerationSettings settings = CreateSettings(new Vector2(8f, 6f));
            var root = Track(new GameObject("Terrain"));

            root.AddComponent<ContinuousTerrainView>().Build(settings, null);

            Assert.That(root.transform.childCount, Is.EqualTo(1));
            Transform ground = root.transform.GetChild(0);
            Assert.That(ground.name, Is.EqualTo("Flat Ground"));
            Assert.That(ground.position, Is.EqualTo(new Vector3(4f, 0f, 3f)));
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
            TerrainGenerationSettings settings = CreateSettings(new Vector2(8f, 6f));
            var root = Track(new GameObject("Terrain"));
            ContinuousTerrainView terrainView = root.AddComponent<ContinuousTerrainView>();
            terrainView.Build(settings, null);
            var target = Track(new GameObject("Target"));
            terrainView.SetTarget(target.transform);
            target.transform.position = new Vector3(100f, 0f, 100f);

            typeof(ContinuousTerrainView)
                .GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(terrainView, null);

            Transform ground = root.transform.GetChild(0);
            Assert.That(Vector3.Distance(ground.position, target.transform.position), Is.LessThan(12f));
        }

        [Test]
        public void TerrainView_AcceptsFractionalWorldDimensionsWithoutBuildGridCells()
        {
            var startingAreaSize = new Vector2(2750.5f, 1800.25f);
            TerrainGenerationSettings settings = CreateSettings(startingAreaSize);
            var root = Track(new GameObject("Terrain"));

            root.AddComponent<ContinuousTerrainView>().Build(settings, null);

            Transform ground = root.transform.GetChild(0);
            Mesh mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.size.x, Is.EqualTo(startingAreaSize.x).Within(.01f));
            Assert.That(ground.position, Is.EqualTo(settings.StartingAreaCenter));
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
        public void WorldSpriteView_AcceptsAuthoredRectsWhenSheetHasRemainderPixels()
        {
            var texture = Track(new Texture2D(16, 18));
            var rects = new Rect[16];
            var pivots = new Vector2[16];
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = new Rect((i % 4) * 4f, (3 - i / 4) * 4f, 4f, 4f);
                pivots[i] = new Vector2(.5f, 0f);
            }

            var root = Track(new GameObject("Sprite"));
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            WorldSpriteView view = root.AddComponent<WorldSpriteView>();

            view.ConfigureDirectional(null, 2f, texture, 4, rects, pivots);
            view.SetMovement(Vector2.up);

            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite.rect, Is.EqualTo(rects[12]));
        }

        [Test]
        public void PlayerAnimationController_ComposesOneEquipmentSpriteWithDirectionalGripPoses()
        {
            var bodyTexture = Track(new Texture2D(8, 16));
            var bodySprite = Track(Sprite.Create(bodyTexture, new Rect(0f, 0f, 8f, 16f),
                new Vector2(.5f, 0f), 8f));
            var equipmentTexture = Track(new Texture2D(4, 8));
            var equipmentSprite = Track(Sprite.Create(equipmentTexture, new Rect(0f, 0f, 4f, 8f),
                new Vector2(.5f, .2f), 4f));
            var root = Track(new GameObject("Player"));
            PlayerAnimationController animation = PlayerAnimationController.Create(root.transform, bodySprite, 2f);
            var equipment = Track(ScriptableObject.CreateInstance<PlayerEquipmentVisualDefinition>());
            equipment.Configure(equipmentSprite, .5f,
                new DirectionalEquipmentPose(new Vector2(0f, .2f), 0f),
                new DirectionalEquipmentPose(new Vector2(-.2f, .1f), -25f),
                new DirectionalEquipmentPose(new Vector2(.2f, .1f), 25f, true),
                new DirectionalEquipmentPose(new Vector2(0f, .25f), 180f, true));

            Assert.That(animation.Equip(equipment), Is.True);
            animation.SetMovement(Vector2.right);

            Assert.That(animation.Body.Facing, Is.EqualTo(SpriteFacingDirection.Right));
            Assert.That(animation.EquipmentRenderer.sprite, Is.EqualTo(equipmentSprite));
            Assert.That(animation.EquipmentRenderer.enabled, Is.True);
            Assert.That(animation.EquipmentMotion.localPosition.x, Is.EqualTo(.4f).Within(.001f));
            Assert.That(animation.EquipmentMotion.localPosition.y, Is.EqualTo(.2f).Within(.001f));
            Assert.That(animation.EquipmentMotion.localPosition.z, Is.Zero.Within(.001f));
            Assert.That(animation.EquipmentMotion.localEulerAngles.z, Is.EqualTo(25f).Within(.001f));
            Assert.That(animation.EquipmentMotion.localScale.x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(animation.EquipmentRenderer.sortingOrder,
                Is.EqualTo(animation.Body.Renderer.sortingOrder - 1));
        }

        [Test]
        public void PlayerAnimationController_PlaysReusableTransformActionAndReturnsToRest()
        {
            var texture = Track(new Texture2D(8, 16));
            var sprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 8f, 16f),
                new Vector2(.5f, 0f), 8f));
            var root = Track(new GameObject("Player"));
            PlayerAnimationController animation = PlayerAnimationController.Create(root.transform, sprite, 2f);
            var action = Track(ScriptableObject.CreateInstance<PlayerActionAnimationDefinition>());
            action.Configure(1f, false,
                AnimationCurve.Constant(0f, 1f, .1f),
                AnimationCurve.Constant(0f, 1f, .2f),
                AnimationCurve.Constant(0f, 1f, 10f),
                AnimationCurve.Constant(0f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 0f),
                AnimationCurve.Constant(0f, 1f, 30f),
                AnimationCurve.Constant(0f, 1f, .8f));
            action.ConfigureRig(
                AnimationCurve.Constant(0f, 1f, 12f),
                AnimationCurve.Constant(0f, 1f, 24f),
                AnimationCurve.Constant(0f, 1f, 6f),
                AnimationCurve.Constant(0f, 1f, -18f),
                AnimationCurve.Constant(0f, 1f, -30f),
                AnimationCurve.Constant(0f, 1f, -8f));
            bool completed = false;
            animation.ActionCompleted += completedAction => completed = completedAction == action;

            Assert.That(animation.PlayAction(action), Is.True);
            animation.Advance(.5f);

            Assert.That(animation.CharacterMotion.localPosition.x, Is.EqualTo(.2f).Within(.001f));
            Assert.That(animation.CharacterMotion.localPosition.y, Is.EqualTo(.4f).Within(.001f));
            Assert.That(animation.CharacterMotion.localEulerAngles.z, Is.EqualTo(10f).Within(.001f));
            Assert.That(animation.IsPlayingAction, Is.True);
            PlayerRigActionPose rigPose = action.EvaluateRigPose(.5f);
            Assert.That(rigPose.FarUpperArm, Is.EqualTo(12f).Within(.001f));
            Assert.That(rigPose.NearForearm, Is.EqualTo(-30f).Within(.001f));

            animation.Advance(.5f);

            Assert.That(completed, Is.True);
            Assert.That(animation.IsPlayingAction, Is.False);
            Assert.That(animation.CharacterMotion.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(animation.CharacterMotion.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void PlayerAnimationController_ResolvesToolActionByItemIdAndUnequipsOnStop()
        {
            var texture = Track(new Texture2D(8, 16));
            var bodySprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 8f, 16f),
                new Vector2(.5f, 0f), 8f));
            var toolSprite = Track(Sprite.Create(texture, new Rect(0f, 0f, 4f, 8f),
                new Vector2(.5f, .5f), 8f));
            var root = Track(new GameObject("Player"));
            PlayerAnimationController animation = PlayerAnimationController.Create(root.transform, bodySprite, 2f);
            var visual = Track(ScriptableObject.CreateInstance<PlayerEquipmentVisualDefinition>());
            visual.Configure(toolSprite, .5f, default, default, default, default);
            var action = Track(ScriptableObject.CreateInstance<PlayerActionAnimationDefinition>());
            action.Configure(1f, true, null, null, null, null, null, null, null);
            var toolAnimation = Track(ScriptableObject.CreateInstance<PlayerToolAnimationDefinition>());
            toolAnimation.Configure("pickaxe", visual, action);
            animation.ConfigureTools(new[] { toolAnimation });

            Assert.That(animation.TryPlayToolAction("pickaxe"), Is.True);
            Assert.That(animation.Equipment, Is.SameAs(visual));
            Assert.That(animation.CurrentAction, Is.SameAs(action));
            Assert.That(animation.StopToolAction("another_tool"), Is.False);
            Assert.That(animation.StopToolAction("pickaxe"), Is.True);
            Assert.That(animation.Equipment, Is.Null);
            Assert.That(animation.IsPlayingAction, Is.False);
        }

        [Test]
        public void PlayerAnimationController_ModularRigReusesPartsAndSwitchesDirection()
        {
            var texture = Track(new Texture2D(16, 16));
            var fallback = Track(Sprite.Create(texture, new Rect(0f, 0f, 8f, 16f),
                new Vector2(.5f, 0f), 8f));
            var downPart = Track(Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f),
                new Vector2(.5f, 1f), 8f));
            var rightPart = Track(Sprite.Create(texture, new Rect(8f, 0f, 8f, 8f),
                new Vector2(.5f, 1f), 8f));
            var root = Track(new GameObject("Player"));
            PlayerAnimationController animation = PlayerAnimationController.Create(root.transform, fallback, 2f);
            var definition = Track(ScriptableObject.CreateInstance<ModularPlayerRigDefinition>());
            var down = new DirectionalRigSprites(downPart, downPart, downPart, downPart,
                downPart, downPart, downPart);
            var right = new DirectionalRigSprites(rightPart, rightPart, rightPart, rightPart,
                rightPart, rightPart, rightPart);
            definition.Configure(down, down, right, down);

            Assert.That(animation.ConfigureModularRig(definition), Is.True);
            animation.SetMovement(Vector2.right);

            Assert.That(animation.Body.Renderer.enabled, Is.False);
            Assert.That(animation.ModularRig, Is.Not.Null);
            SpriteRenderer torso = animation.ModularRig.transform.Find("Torso").GetComponent<SpriteRenderer>();
            Assert.That(torso.sprite, Is.SameAs(rightPart));
            Assert.That(animation.ModularRig.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(13),
                "The torso and four three-part chains should use only authored overlap-ready sprites.");
            Transform ankle = animation.ModularRig.transform.Find("Far Leg/Middle Joint/End Joint");
            Vector3 restingAnkle = ankle.position;
            animation.Advance(.1f);
            Assert.That(Vector3.Distance(restingAnkle, ankle.position), Is.GreaterThan(.001f),
                "Movement time should advance the authored cutout gait without baked full-body frames.");

            animation.SetMovement(Vector2.down);
            animation.Advance(.1f);
            Transform farAnkle = animation.ModularRig.transform.Find("Far Leg/Middle Joint/End Joint");
            Transform nearAnkle = animation.ModularRig.transform.Find("Near Leg/Middle Joint/End Joint");
            Assert.That(farAnkle.position.x, Is.LessThan(nearAnkle.position.x),
                "Front/back gait must keep each foot on its own side instead of crossing the legs.");
            Assert.That(animation.ModularRig.transform.Find("Near Arm/Upper")
                .GetComponent<SpriteRenderer>().flipX, Is.True,
                "Front/back views must mirror the reused second-side limb artwork.");

            var action = Track(ScriptableObject.CreateInstance<PlayerActionAnimationDefinition>());
            action.Configure(1f, true, null, null, null, null, null, null, null);
            action.ConfigureRig(null, null, null,
                AnimationCurve.Constant(0f, 1f, 35f), null, null);
            Assert.That(animation.PlayAction(action), Is.True);
            animation.Advance(.1f);
            float nearArmAngle = animation.ModularRig.transform.Find("Near Arm").localEulerAngles.z;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(nearArmAngle, 35f)), Is.LessThan(.001f),
                "Action assets must drive character symbols, not only the held equipment sprite.");
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
        public void FollowCamera_UsesObliqueOrthographicFraming()
        {
            var camera = Track(new GameObject("Camera"));
            Camera unityCamera = camera.AddComponent<Camera>();
            unityCamera.orthographic = true;
            unityCamera.orthographicSize = 9.5f;
            var target = Track(new GameObject("Target"));
            FollowCamera follow = camera.AddComponent<FollowCamera>();

            follow.SetTarget(target.transform);

            Vector3 horizontalForward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            float downwardPitch = Vector3.Angle(horizontalForward, camera.transform.forward);
            Vector3 directionToTarget = target.transform.position - camera.transform.position;
            float groundViewingAngle = Vector3.Angle(
                Vector3.ProjectOnPlane(directionToTarget, Vector3.up), directionToTarget);
            float targetDistance = Vector3.Distance(camera.transform.position, target.transform.position);
            Vector3 targetViewportPosition = unityCamera.WorldToViewportPoint(target.transform.position);
            Assert.That(groundViewingAngle, Is.InRange(79f, 81f),
                "The camera position should stay mostly above the player.");
            Assert.That(downwardPitch, Is.InRange(74f, 76f),
                "The steep pitch should keep square ground tiles nearly square on screen.");
            Assert.That(targetDistance, Is.InRange(17f, 17.5f),
                "The camera distance should preserve a useful amount of surrounding play area.");
            Assert.That(targetViewportPosition.x, Is.EqualTo(.5f).Within(.01f));
            Assert.That(targetViewportPosition.y, Is.InRange(.4f, .44f),
                "The player should sit just below center so more terrain remains visible ahead.");
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
            float projectedRight = Mathf.Abs(Vector3.Dot(camera.transform.right, Vector3.right));
            float projectedForward = Mathf.Abs(Vector3.Dot(camera.transform.up, Vector3.forward));
            Assert.That(projectedForward / projectedRight, Is.InRange(.95f, 1.05f),
                "A square world tile should remain approximately square in the gameplay view.");
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

        private TerrainGenerationSettings CreateSettings(Vector2 startingAreaSize)
        {
            var settings = Track(ScriptableObject.CreateInstance<TerrainGenerationSettings>());
            settings.Configure(startingAreaSize, 42);
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
