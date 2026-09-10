using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.World.Chunks;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    /// <summary>
    /// Covers the rule that makes several cutouts per resource safe: which variant a node wears has to
    /// come from the chunk plan, because a chunk is rebuilt from scratch every time the player walks
    /// back into it. A variant drawn when the node is built would change the look of a deposit each
    /// time it streams in.
    /// </summary>
    public sealed class ResourceVariantTests
    {
        private const int WorldSeed = 8128;
        private const float ChunkSize = 32f;

        private readonly List<Object> _createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void Plan_GivesEveryNodeAVariantSeedThatSurvivesAReload()
        {
            var chunk = new ChunkCoordinate(3, -5);

            IReadOnlyList<ChunkResourcePlacement> first = Plan(chunk);
            IReadOnlyList<ChunkResourcePlacement> reloaded = Plan(chunk);

            Assert.That(first.Count, Is.GreaterThan(1), "This chunk needs several nodes to be a useful case.");
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(reloaded[i].VariantSeed, Is.EqualTo(first[i].VariantSeed),
                    "A streamed-in chunk must restore the same look for every node.");
            }
        }

        [Test]
        public void Plan_DoesNotGiveEveryNodeOfAChunkTheSameVariant()
        {
            var distinctVariants = new HashSet<int>();
            for (int x = 0; x < 12; x++)
            {
                IReadOnlyList<ChunkResourcePlacement> placements = Plan(new ChunkCoordinate(x, x * 2 - 3));
                for (int i = 0; i < placements.Count; i++)
                {
                    // Five is the variant count the ice deposits are authored for.
                    distinctVariants.Add((placements[i].VariantSeed & int.MaxValue) % 5);
                }
            }

            Assert.That(distinctVariants.Count, Is.GreaterThan(1),
                "Every node landing on one variant would defeat the point of authoring several.");
        }

        [Test]
        public void SelectWorldSprite_IsStablePerSeedAndCoversEveryVariant()
        {
            Sprite[] variants = { CreateSprite("a"), CreateSprite("b"), CreateSprite("c") };
            ResourceNodeDefinition definition = CreateDefinition();
            definition.SetWorldSprites(variants);

            Assert.That(definition.SelectWorldSprite(7), Is.SameAs(definition.SelectWorldSprite(7)));
            Assert.That(definition.SelectWorldSprite(0), Is.SameAs(variants[0]));
            Assert.That(definition.SelectWorldSprite(1), Is.SameAs(variants[1]));
            Assert.That(definition.SelectWorldSprite(2), Is.SameAs(variants[2]));
            Assert.That(definition.SelectWorldSprite(3), Is.SameAs(variants[0]));
            Assert.That(definition.SelectWorldSprite(int.MinValue), Is.Not.Null,
                "A negative hash must not fall outside the variant list.");
        }

        [Test]
        public void SelectWorldSprite_WithASingleCutout_AlwaysReturnsIt()
        {
            Sprite only = CreateSprite("only");
            ResourceNodeDefinition definition = CreateDefinition();
            definition.SetWorldSprites(only);

            Assert.That(definition.SelectWorldSprite(0), Is.SameAs(only));
            Assert.That(definition.SelectWorldSprite(9999), Is.SameAs(only));
            Assert.That(definition.WorldSprite, Is.SameAs(only));
        }

        [Test]
        public void SelectWorldSprite_WithoutArtwork_ReturnsNullSoThePlaceholderIsDrawn()
        {
            ResourceNodeDefinition definition = CreateDefinition();

            Assert.That(definition.SelectWorldSprite(4), Is.Null);
            Assert.That(definition.WorldSprite, Is.Null);
        }

        [Test]
        public void SelectGroundPatchFootprint_IsStableAndKeepsConfiguredLargeSheets()
        {
            ResourceNodeDefinition definition = CreateDefinition();
            definition.ConfigureGroundPatchFootprints(Vector2Int.one, new Vector2Int(2, 2), new Vector2Int(3, 2));

            Assert.That(definition.SelectGroundPatchFootprint(1), Is.EqualTo(new Vector2Int(2, 2)));
            Assert.That(definition.SelectGroundPatchFootprint(2), Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(definition.SelectGroundPatchFootprint(2), Is.EqualTo(
                definition.SelectGroundPatchFootprint(2)), "Reloading a chunk must restore the same patch shape.");
        }

        [Test]
        public void SelectGroundPatchFootprint_WithoutConfiguration_PreservesSingleTileNodes()
        {
            Assert.That(CreateDefinition().SelectGroundPatchFootprint(123), Is.EqualTo(Vector2Int.one));
        }

        private static IReadOnlyList<ChunkResourcePlacement> Plan(ChunkCoordinate chunk)
        {
            return ChunkResourcePlanner.Plan(chunk, WorldSeed, ChunkSize, 4f, new[] { 2.5f, 1.5f });
        }

        private ResourceNodeDefinition CreateDefinition()
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure("ice_chunk", "Ice Chunk", 1, 20, false, true);
            _createdAssets.Add(item);

            ResourceNodeDefinition definition = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            definition.Configure("ice_deposit", "Ice Deposit", 4f, 2.25f, string.Empty,
                new Vector3(2.4f, 1.35f, 1.4f), new ResourceYield(item, 3));
            _createdAssets.Add(definition);
            return definition;
        }

        private Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(4, 4);
            _createdAssets.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(.5f, .5f));
            sprite.name = name;
            _createdAssets.Add(sprite);
            return sprite;
        }
    }
}
