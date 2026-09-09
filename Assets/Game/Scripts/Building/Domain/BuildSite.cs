using System;
using PlanetSurvival.Building.Definitions;
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
        internal BuildSite(string siteId, BuildableDefinition definition, BuildFootprint footprint)
        {
            SiteId = siteId;
            Definition = definition;
            Footprint = footprint;
            TotalSeconds = Mathf.Max(0f, definition.BuildSeconds);
            RemainingSeconds = TotalSeconds;
            State = TotalSeconds <= 0f ? BuildState.Completed : BuildState.UnderConstruction;
        }

        public string SiteId { get; }
        public BuildableDefinition Definition { get; }
        public BuildFootprint Footprint { get; }
        public BuildState State { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float TotalSeconds { get; }

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
    }
}
