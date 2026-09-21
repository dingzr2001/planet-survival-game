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
        internal BuildSite(string siteId, BuildableDefinition definition, BuildFootprint footprint,
            IReadOnlyList<InventoryItemAmount> paidMaterials)
            : this(siteId, definition, footprint, null, paidMaterials)
        {
        }

        internal BuildSite(string siteId, BuildableDefinition definition, Vector2Int quarterCell,
            IReadOnlyList<InventoryItemAmount> paidMaterials, int transferPostNumber)
            : this(siteId, definition, default, quarterCell, paidMaterials, transferPostNumber)
        {
        }

        private BuildSite(string siteId, BuildableDefinition definition, BuildFootprint footprint,
            Vector2Int? quarterCell, IReadOnlyList<InventoryItemAmount> paidMaterials,
            int transferPostNumber = 0)
        {
            SiteId = siteId;
            Definition = definition;
            Footprint = footprint;
            QuarterCell = quarterCell;
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
        public BuildFootprint Footprint { get; }
        public Vector2Int? QuarterCell { get; }
        public BuildState State { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float TotalSeconds { get; }
        public OxygenCandleBurn OxygenCandle { get; }
        public MiningDrill MiningDrill { get; }
        public PlanterBox PlanterBox { get; }
        public ItemTransferPost ItemTransferPost { get; }
        public PowerPole PowerPole { get; }
        public IReadOnlyList<InventoryItemAmount> PaidMaterials { get; }

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
