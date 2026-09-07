using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.World.Generation;
using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>Builds the surface landing pod's presentation, solid body, and airlock interaction.</summary>
    public static class LandingPodExterior
    {
        private const float DefaultExteriorHeight = 5.4f;
        private const string ExteriorResourcePath = "World/LandingPodExterior";
        private static readonly Vector2 GroundAnchor = new(.43f, .18f);
        private static readonly Vector3 BodyColliderSize = new(6.2f, 4.5f, 4f);

        public static GameObject Create(Transform parent, Vector3 position, WorldVisualSettings visuals)
        {
            var pod = new GameObject("Landing Pod Surface Exterior");
            pod.transform.SetParent(parent);
            pod.transform.position = position;

            var bodyCollider = pod.AddComponent<BoxCollider>();
            bodyCollider.size = BodyColliderSize;
            bodyCollider.center = Vector3.up * (BodyColliderSize.y * .5f);

            CreateVisual(pod.transform, visuals);
            CreateAirlock(pod.transform);

            Color shadowColor = visuals != null
                ? visuals.ShadowColor
                : new Color(0f, 0f, 0f, .4f);
            BlobShadow.Create(pod.transform, new Vector2(5.8f, 3.1f), shadowColor);
            return pod;
        }

        private static void CreateVisual(Transform parent, WorldVisualSettings visuals)
        {
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(parent, false);
            visual.AddComponent<SpriteRenderer>();

            Sprite sprite = visuals != null ? visuals.LandingPodExteriorSprite : null;
            if (sprite == null)
            {
                sprite = LoadResourceSprite();
            }

            float height = visuals != null ? visuals.LandingPodExteriorHeight : DefaultExteriorHeight;
            visual.AddComponent<WorldSpriteView>().ConfigureGrounded(sprite, height);

            if (sprite == null)
            {
                Debug.LogWarning("The landing pod exterior sprite is missing; run 'Planet Survival/Setup World Art'.", parent);
            }
        }

        private static Sprite LoadResourceSprite()
        {
            Sprite importedSprite = Resources.Load<Sprite>(ExteriorResourcePath);
            if (importedSprite != null)
            {
                return importedSprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(ExteriorResourcePath);
            if (texture == null)
            {
                return null;
            }

            Sprite runtimeSprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                GroundAnchor,
                512f,
                0,
                SpriteMeshType.FullRect);
            runtimeSprite.name = "Landing Pod Exterior (Runtime)";
            return runtimeSprite;
        }

        private static void CreateAirlock(Transform parent)
        {
            var airlock = new GameObject("Surface Airlock Interaction");
            airlock.transform.SetParent(parent, false);
            airlock.transform.localPosition = new Vector3(-3.35f, 0f, 0f);

            var trigger = airlock.AddComponent<BoxCollider>();
            trigger.center = Vector3.up * 1.1f;
            trigger.size = new Vector3(1.2f, 2.2f, 1.8f);
            trigger.isTrigger = true;

            airlock.AddComponent<ScenePortal>().Configure(
                GameSceneNames.LandingPodCargo,
                "enter the landing pod airlock");
        }
    }
}
