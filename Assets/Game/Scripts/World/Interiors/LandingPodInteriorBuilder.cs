using PlanetSurvival.Cooking.Runtime;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.SceneManagement;
using PlanetSurvival.Farming.Runtime;
using PlanetSurvival.Storage.Runtime;
using PlanetSurvival.UI.Storage;
using PlanetSurvival.UI.Water;
using PlanetSurvival.Water.Domain;
using PlanetSurvival.Water.Runtime;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.World.Interiors
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Adds only gameplay geometry that cannot be represented by the fixed interior artwork:
    /// room boundaries, interactable fixtures, scene portals, and the free-standing props.
    /// </summary>
    public sealed class LandingPodInteriorBuilder
    {
        /// <summary>
        /// Artwork of the machines that are not painted into the backdrop. A missing sprite leaves a
        /// tinted placeholder behind so a fixture is still visible and usable before its art exists.
        /// </summary>
        private const string HydroponicsRackResource = "Interiors/HydroponicsRack";
        private const string WaterProcessorResource = "Interiors/WaterProcessor";

        /// <summary>
        /// Interior world units are about 1.5× metric — the painted crew and equipment are drawn that
        /// way — so a two-metre machine has to stand roughly three units tall to match the backdrop.
        /// </summary>
        private const float HydroponicsRackHeight = 3.1f;
        private const float WaterProcessorHeight = 2.9f;

        private Transform _root;

        public void Build(Transform parent, LandingPodDeck deck, Sprite diningTableSprite, Camera camera,
            in LandingPodFixtures fixtures)
        {
            _root = new GameObject("Interior Gameplay Geometry").transform;
            _root.SetParent(parent, false);

            BuildRoomBoundaries(deck);
            if (deck == LandingPodDeck.Habitat)
            {
                BuildDiningTable(diningTableSprite, camera);
                CreateWaterDispenser(camera, fixtures.WaterSupply, fixtures.WaterBottle, fixtures.RefillView);
                CreateStorage("Habitat Refrigerator", "refrigerator", camera, new Vector2(.055f, .43f),
                    new Vector3(2.2f, 2.4f, 1.8f), fixtures.RefrigeratorStorage, fixtures.StorageView);
                // The galley range sits between the refrigerator and the sink in the backdrop art. Its
                // trigger stays clear of both neighbours so standing at the counter never focuses the
                // wrong fixture.
                CreateCookingStation("Habitat Oven", camera, new Vector2(.165f, .45f),
                    new Vector3(1.6f, 2.2f, 1f), fixtures.Cooking);
                // The rack stands on open floor between the deck hatch and the starboard lockers, on the
                // same deck as the galley it feeds, so a harvest walks straight to the oven. It is kept
                // clear of the painted suit locker further right.
                CreateHydroponicsRack(camera, new Vector2(.70f, .42f), fixtures.Hydroponics);
                CreatePortal("Deck Ladder", PortalPosition(camera, new Vector2(.615f, .29f)), new Vector3(2.2f, 2f, 1.5f),
                    GameSceneNames.LandingPodCargo, "climb down to cargo deck");
            }
            else
            {
                CreateStorage("Cargo Storage Racks", "cargo storage", camera, new Vector2(.17f, .58f),
                    new Vector3(4.8f, 2.4f, 2.2f), fixtures.CargoStorage, fixtures.StorageView);
                // The processor stands between the ladder and the airlock: ice can be dumped on the way
                // in, before the haul is carried any further.
                CreateWaterProcessor(camera, new Vector2(.70f, .43f), fixtures.Processor);
                CreatePortal("Deck Ladder", PortalPosition(camera, new Vector2(.60f, .60f)), new Vector3(2f, 2f, 1.2f),
                    GameSceneNames.LandingPodHabitat, "climb up to habitat deck");
                CreatePortal("Surface Airlock", PortalPosition(camera, new Vector2(.82f, .55f)), new Vector3(1.8f, 2f, 1.6f),
                    GameSceneNames.Gameplay, "cycle airlock and exit to surface");
            }
        }

        private void CreateStorage(string objectName, string displayName, Camera camera, Vector2 viewportPosition,
            Vector3 colliderSize, InventoryModel inventory, StorageView storageView)
        {
            var storage = new GameObject(objectName);
            storage.transform.SetParent(_root, false);
            storage.transform.localPosition = PortalPosition(camera, viewportPosition);
            BoxCollider collider = storage.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.isTrigger = true;
            storage.AddComponent<StorageContainer>().Bind(displayName, inventory, storageView);
        }

        private void CreateCookingStation(string objectName, Camera camera, Vector2 viewportPosition,
            Vector3 colliderSize, CookingStationBinding cooking)
        {
            if (!cooking.IsComplete)
            {
                Debug.LogWarning($"'{objectName}' was omitted because its cooking configuration is incomplete.");
                return;
            }

            var station = new GameObject(objectName);
            station.transform.SetParent(_root, false);
            station.transform.localPosition = PortalPosition(camera, viewportPosition);
            BoxCollider collider = station.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.isTrigger = true;
            station.AddComponent<CookingStation>().Bind(cooking);
        }

        private void CreateHydroponicsRack(Camera camera, Vector2 viewportPosition,
            in HydroponicsBinding binding)
        {
            if (!binding.IsComplete)
            {
                Debug.LogWarning("The hydroponics rack was omitted because its configuration is incomplete.");
                return;
            }

            GameObject fixture = CreateFixtureProp("Habitat Hydroponics Rack", HydroponicsRackResource, camera,
                viewportPosition, HydroponicsRackHeight, new Vector3(2.2f, HydroponicsRackHeight, 1.5f),
                new Color(.34f, .56f, .38f));
            fixture.AddComponent<HydroponicsStation>().Bind(binding);
        }

        private void CreateWaterProcessor(Camera camera, Vector2 viewportPosition,
            in WaterProcessorBinding binding)
        {
            if (!binding.IsComplete)
            {
                Debug.LogWarning("The water processor was omitted because its configuration is incomplete.");
                return;
            }

            GameObject fixture = CreateFixtureProp("Cargo Water Processor", WaterProcessorResource, camera,
                viewportPosition, WaterProcessorHeight, new Vector3(1.8f, WaterProcessorHeight, 1.4f),
                new Color(.42f, .58f, .68f));
            fixture.AddComponent<WaterProcessorStation>().Bind(binding);
        }

        /// <summary>
        /// Builds a free-standing machine: a billboard that stands on the floor plus the trigger the
        /// player interacts through. Without artwork the billboard is a tinted block, so the fixture can
        /// be found and used before an artist has drawn it.
        /// </summary>
        private GameObject CreateFixtureProp(string objectName, string spriteResourcePath, Camera camera,
            Vector2 viewportPosition, float worldHeight, Vector3 colliderSize, Color placeholderColor)
        {
            var fixture = new GameObject(objectName);
            fixture.transform.SetParent(_root, false);
            fixture.transform.localPosition = ViewportToGround(camera, viewportPosition);

            Sprite sprite = Resources.Load<Sprite>(spriteResourcePath);
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(fixture.transform, false);
            visual.transform.rotation = camera.transform.rotation;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }
            else
            {
                Debug.LogWarning(
                    $"'{objectName}' has no sprite at 'Resources/{spriteResourcePath}'; a placeholder block is " +
                    "drawn instead. A texture left at the default import type also lands here — run " +
                    "'Planet Survival/Setup World Art' to import prop artwork as a sprite.");
                renderer.sprite = PlaceholderArt.SolidSprite();
                renderer.color = placeholderColor;
            }

            float spriteHeight = Mathf.Max(.01f, renderer.sprite.bounds.size.y);
            visual.transform.localScale = Vector3.one * (worldHeight / spriteHeight);
            // The billboard faces the camera, so half its height has to be lifted along the camera's up
            // axis, not the world's: offsetting along world up would tilt the machine into the floor by
            // the cosine of the camera pitch. The artwork must be trimmed to its opaque bounds for the
            // sprite's bottom edge to be its contact point.
            visual.transform.localPosition = camera.transform.up * (worldHeight * .5f);
            float depth = Vector3.Dot(
                camera.transform.forward, fixture.transform.position - camera.transform.position);
            renderer.sortingOrder = -Mathf.RoundToInt(depth * 100f);

            BoxCollider collider = fixture.AddComponent<BoxCollider>();
            collider.center = Vector3.up * (colliderSize.y * .5f);
            collider.size = colliderSize;
            collider.isTrigger = true;
            return fixture;
        }

        private void CreateWaterDispenser(Camera camera, LiquidContainer waterSupply,
            LiquidContainer waterBottle, WaterRefillView refillView)
        {
            var dispenser = new GameObject("Habitat Water Dispenser");
            dispenser.transform.SetParent(_root, false);
            dispenser.transform.localPosition = PortalPosition(camera, new Vector2(.235f, .53f));
            BoxCollider collider = dispenser.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.3f, 2f, 1.8f);
            collider.isTrigger = true;
            dispenser.AddComponent<LandingPodWaterDispenser>().Bind(waterSupply, waterBottle, refillView);
        }

        private void BuildRoomBoundaries(LandingPodDeck deck)
        {
            var boundaryRoot = new GameObject("Room Boundary");
            boundaryRoot.transform.SetParent(_root, false);

            // The outline is authored directly on the XZ ground plane. The long side edges are
            // approximately 40 degrees from the horizontal; the two lower chamfers keep the player
            // off the foreground hull visible in the lower corners of the artwork.
            Vector2[] outline = deck == LandingPodDeck.Habitat
                ? new[]
                {
                    new Vector2(-9.1f, -5.3f),
                    new Vector2(9.1f, -5.3f),
                    new Vector2(11.7f, -.5f),
                    new Vector2(7.5f, 3f),
                    new Vector2(-7.5f, 3f),
                    new Vector2(-11.7f, -.5f)
                }
                : new[]
                {
                    new Vector2(-9.2f, -5.15f),
                    new Vector2(9.2f, -5.15f),
                    new Vector2(11.9f, -.45f),
                    new Vector2(7.7f, 3.1f),
                    new Vector2(-7.7f, 3.1f),
                    new Vector2(-11.9f, -.45f)
                };

            for (int i = 0; i < outline.Length; i++)
            {
                Vector3 start = new Vector3(outline[i].x, 0f, outline[i].y);
                Vector2 next = outline[(i + 1) % outline.Length];
                Vector3 end = new Vector3(next.x, 0f, next.y);
                CreateBoundarySegment(boundaryRoot.transform, i, start, end);
            }
        }

        private void BuildDiningTable(Sprite diningTableSprite, Camera camera)
        {
            if (diningTableSprite == null)
            {
                Debug.LogWarning("The habitat dining-table sprite is missing; its collision was omitted.");
                return;
            }

            var table = new GameObject("Dining Table");
            table.transform.SetParent(_root, false);
            table.transform.localPosition = new Vector3(-2.8f, 1.1f, -.1f);

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(table.transform, false);
            visual.transform.rotation = camera.transform.rotation;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = diningTableSprite;
            float depth = Vector3.Dot(camera.transform.forward, table.transform.position - camera.transform.position);
            renderer.sortingOrder = -Mathf.RoundToInt(depth * 100f);
            float spriteHeight = Mathf.Max(.01f, diningTableSprite.bounds.size.y);
            visual.transform.localScale = Vector3.one * (2.9f / spriteHeight);

            BoxCollider collider = table.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -.65f, 0f);
            collider.size = new Vector3(3.8f, .9f, 2.25f);
        }

        private static void CreateBoundarySegment(Transform parent, int index, Vector3 start, Vector3 end)
        {
            const float thickness = .28f;
            Vector3 direction = end - start;
            var boundary = new GameObject($"Boundary Segment {index + 1:00}");
            boundary.transform.SetParent(parent, false);
            boundary.transform.position = (start + end) * .5f + Vector3.up;
            boundary.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            boundary.AddComponent<BoxCollider>().size = new Vector3(thickness, 2f, direction.magnitude + thickness);
        }

        private static Vector3 ViewportToGround(Camera camera, Vector2 viewportPosition)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(viewportPosition.x, viewportPosition.y, 0f));
            float distance = -ray.origin.y / ray.direction.y;
            Vector3 point = ray.GetPoint(distance);
            point.y = 0f;
            return point;
        }

        private static Vector3 PortalPosition(Camera camera, Vector2 viewportPosition)
        {
            Vector3 position = ViewportToGround(camera, viewportPosition);
            position.y = 1f;
            return position;
        }

        private void CreatePortal(string objectName, Vector3 position, Vector3 size,
            string targetScene, string prompt)
        {
            var portalObject = new GameObject(objectName);
            portalObject.transform.SetParent(_root, false);
            portalObject.transform.localPosition = position;
            BoxCollider collider = portalObject.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            portalObject.AddComponent<ScenePortal>().Configure(targetScene, prompt);
        }
    }
}
