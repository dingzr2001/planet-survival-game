using System;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Inventory.Domain;
using System.Collections.Generic;
using PlanetSurvival.Transport.Domain;
using PlanetSurvival.Power.Domain;
using UnityEngine;

namespace PlanetSurvival.Building.Domain
{
    public enum BuildState
    {
        /// <summary>Materials are already spent and the construction timer is running.</summary>
        UnderConstruction,

        /// <summary>The timer finished; the building stands.</summary>
        Completed
    }

    /// <summary>
    /// One placed structure, from the moment its materials are paid until it is demolished. Like the
    /// cooking process this is plain state: the scene component drives <see cref="Advance"/>, while the
    /// session owns the instance so a half-built shed survives a trip into the landing pod.
    /// </summary>
    public sealed class BuildSite
    {
        /// <param name="compactBuildingNumber">
        /// The player-facing number of a transfer post or power pole; ignored by every other structure.
        /// </param>
        internal BuildSite(string siteId, BuildableDefinition definition, BuildFootprint footprint,
            IReadOnlyList<InventoryItemAmount> paidMaterials, int compactBuildingNumber = 0)
            : this(siteId, definition, footprint, null, paidMaterials, compactBuildingNumber)
        {
        }

        internal BuildSite(string siteId, BuildableDefinition definition, Vector2 offGridCenter,
            IReadOnlyList<InventoryItemAmount> paidMaterials)
            : this(siteId, definition, default, offGridCenter, paidMaterials)
        {
        }

        private BuildSite(string siteId, BuildableDefinition definition, BuildFootprint footprint,
            Vector2? offGridCenter, IReadOnlyList<InventoryItemAmount> paidMaterials,
            int transferPostNumber = 0)
        {
            SiteId = siteId;
            Definition = definition;
            Footprint = footprint;
            OffGridCenter = offGridCenter;
            TotalSeconds = Mathf.Max(0f, definition.BuildSeconds);
            RemainingSeconds = TotalSeconds;
            State = TotalSeconds <= 0f ? BuildState.Completed : BuildState.UnderConstruction;
            if (definition.IsOxygenCandle)
            {
                OxygenCandle = new OxygenCandleBurn();
            }

            if (definition.MiningDrill != null)
            {
                MiningDrill = new MiningDrill(definition.MiningDrill);
            }

            if (definition.PlanterBox != null)
            {
                PlanterBox = new PlanterBox(definition.PlanterBox);
            }

            if (definition.Electrolyzer != null)
            {
                Electrolyzer = new Electrolyzer(definition.Electrolyzer);
            }

            if (definition.CombustionGenerator != null)
            {
                CombustionGenerator = new CombustionGenerator(definition.CombustionGenerator);
            }

            if (definition.IsItemTransferPost)
            {
                int postNumber = Mathf.Max(1, transferPostNumber);
                ItemTransferPost = new ItemTransferPost(postNumber, DefaultTransferColor(postNumber));
            }

            if (definition.IsPowerPole)
            {
                PowerPole = new PowerPole(transferPostNumber);
            }

            PaidMaterials = paidMaterials ?? System.Array.Empty<InventoryItemAmount>();
        }

        public string SiteId { get; }
        public BuildableDefinition Definition { get; }
        /// <summary>The claimed cells of a grid structure; meaningless for an off-grid item.</summary>
        public BuildFootprint Footprint { get; }

        /// <summary>World XZ centre of an off-grid item, or null for a structure snapped to the grid.</summary>
        public Vector2? OffGridCenter { get; }

        public bool IsOffGrid => OffGridCenter.HasValue;
        public BuildState State { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float TotalSeconds { get; }
        public OxygenCandleBurn OxygenCandle { get; }
        public MiningDrill MiningDrill { get; }
        public PlanterBox PlanterBox { get; }
        public Electrolyzer Electrolyzer { get; }
        public CombustionGenerator CombustionGenerator { get; }
        public ItemTransferPost ItemTransferPost { get; }
        public PowerPole PowerPole { get; }
        public IReadOnlyList<InventoryItemAmount> PaidMaterials { get; }

        /// <summary>
        /// The machine contracts the transport and power networks connect to, resolved here so adding a
        /// machine does not mean teaching every network about it. Null when this structure offers none.
        /// </summary>
        public IItemOutput ItemOutput => (IItemOutput)MiningDrill ?? (IItemOutput)Electrolyzer ?? CombustionGenerator;
        public IItemInput ItemInput => (IItemInput)MiningDrill ?? (IItemInput)PlanterBox ?? (IItemInput)Electrolyzer ?? CombustionGenerator;
        public IPowerInput PowerInput => (IPowerInput)MiningDrill ?? Electrolyzer;

        public float Progress => TotalSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(1f - RemainingSeconds / TotalSeconds);

        /// <summary>Raised on every tick of the timer and when the building is finished.</summary>
        public event Action Changed;

        /// <summary>Raised once, when construction finishes.</summary>
        public event Action Completed;

        internal void Advance(float elapsedSeconds)
        {
            if (State != BuildState.UnderConstruction || elapsedSeconds <= 0f)
            {
                return;
            }

            RemainingSeconds -= elapsedSeconds;
            if (RemainingSeconds > 0f)
            {
                Changed?.Invoke();
                return;
            }

            Finish();
        }

        /// <summary>Finishes the build immediately. Used by tests and by debug tooling.</summary>
        internal void Finish()
        {
            if (State == BuildState.Completed)
            {
                return;
            }

            RemainingSeconds = 0f;
            State = BuildState.Completed;
            Changed?.Invoke();
            Completed?.Invoke();
        }

        private static Color DefaultTransferColor(int groupNumber)
        {
            Color color = Color.HSVToRGB(Mathf.Repeat(groupNumber * .173f, 1f), .72f, 1f);
            color.a = 1f;
            return color;
        }
    }
}
