using System;
using System.Collections.Generic;
using System.Linq;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.UI.Power;
using UnityEngine;

namespace PlanetSurvival.Power.Runtime
{
    /// <summary>One-way, configurable power routes. Poles may link remotely; ordinary buildings must be adjacent.</summary>
    [DisallowMultipleComponent]
    public sealed class PowerPoleSystem : MonoBehaviour
    {
        private const string SolarPrefix = "solar:";
        private const string GeneratorPrefix = "generator:";
        private const string ConsumerPrefix = "consumer:";
        private const string PolePrefix = "pole:";
        private readonly List<PowerPoleStation> _stations = new();
        private BuildingService _buildings;
        private PowerPoleView _view;

        public void Bind(BuildingService buildings, PowerPoleView view)
        { _buildings = buildings; _view = view; }
        public void Register(PowerPoleStation station) { if (station != null && !_stations.Contains(station)) { _stations.Add(station); station.AttachNetwork(this, _view); } }
        public void Unregister(PowerPoleStation station) { if (station == null) return; Disconnect(station); _stations.Remove(station); }
        public IReadOnlyList<PowerEndpointOption> GetAvailableInputs(PowerPoleStation station) => BuildOptions(station, true);
        public IReadOnlyList<PowerEndpointOption> GetAvailableOutputs(PowerPoleStation station) => BuildOptions(station, false);
        public string GetEndpointLabel(string id) => TryGetEndpoint(id, out string label, out _) ? label : "Unavailable endpoint";
        public Sprite GetEndpointIcon(string id) => TryGetEndpoint(id, out _, out Sprite icon) ? icon : null;

        public void AddInput(PowerPoleStation station, string endpointId)
        {
            if (station?.Pole == null || !CanUseEndpoint(station, endpointId, true)) return;
            if (IsProducerEndpoint(endpointId)) RemoveEndpointFromOtherPoles(endpointId, station, true);
            station.Pole.AddInput(endpointId);
            if (TryGetPole(endpointId, out PowerPoleStation source)) source.Pole.AddOutput(PoleId(station));
        }
        public void AddOutput(PowerPoleStation station, string endpointId)
        {
            if (station?.Pole == null || !CanUseEndpoint(station, endpointId, false)) return;
            if (IsConsumerEndpoint(endpointId)) RemoveEndpointFromOtherPoles(endpointId, station, false);
            station.Pole.AddOutput(endpointId);
            if (TryGetPole(endpointId, out PowerPoleStation destination)) destination.Pole.AddInput(PoleId(station));
        }
        public void RemoveInput(PowerPoleStation station, string endpointId)
        {
            if (station?.Pole == null || !station.Pole.RemoveInput(endpointId)) return;
            if (TryGetPole(endpointId, out PowerPoleStation source)) source.Pole.RemoveOutput(PoleId(station));
        }
        public void RemoveOutput(PowerPoleStation station, string endpointId)
        {
            if (station?.Pole == null || !station.Pole.RemoveOutput(endpointId)) return;
            if (TryGetPole(endpointId, out PowerPoleStation destination)) destination.Pole.RemoveInput(PoleId(station));
        }
        public void MoveOutputEarlier(PowerPoleStation station, string id) => station?.Pole?.MoveOutputEarlier(id);
        public void MoveOutputLater(PowerPoleStation station, string id) => station?.Pole?.MoveOutputLater(id);

        private void Update()
        {
            if (_buildings == null) return;
            Advance(Time.deltaTime);
        }

        private void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f || Time.timeScale <= 0f) return;
            List<PowerPoleStation> active = _stations.Where(IsActive).ToList();
            var available = active.ToDictionary(station => station, station => SolarInput(station, elapsedSeconds));
            foreach (PowerPoleStation station in active)
            {
                float needed = station.Pole.OutputEndpointIds.Sum(output =>
                    OutputDemand(output, available, elapsedSeconds, new HashSet<PowerPoleStation>()));
                needed = Mathf.Max(0f, needed - available[station]);
                foreach (string inputId in station.Pole.InputEndpointIds)
                {
                    if (needed <= 0f) break;
                    if (!TryGetGenerator(inputId, out BuildSite generatorSite)) continue;
                    float supplied = Mathf.Min(needed,
                        generatorSite.CombustionGenerator.CurrentPowerWatts * elapsedSeconds);
                    available[station] += supplied;
                    needed -= supplied;
                }
            }
            var parents = active.ToDictionary(station => station, station => station.Pole.InputEndpointIds.Count(id => TryGetPole(id, out _)));
            var pending = new Queue<PowerPoleStation>(active.Where(station => parents[station] == 0));
            var settled = new HashSet<PowerPoleStation>();
            while (pending.Count > 0)
            {
                PowerPoleStation station = pending.Dequeue();
                if (!settled.Add(station)) continue;
                float input = available[station];
                float remaining = input;
                float delivered = 0f;
                int powered = 0;
                foreach (string output in station.Pole.OutputEndpointIds)
                {
                    float demand = OutputDemand(output, available, elapsedSeconds, new HashSet<PowerPoleStation>());
                    if (demand <= .0001f)
                    {
                        if (TryGetPole(output, out PowerPoleStation alreadySatisfied) && --parents[alreadySatisfied] == 0)
                        {
                            pending.Enqueue(alreadySatisfied);
                        }
                        powered++;
                        continue;
                    }
                    if (remaining + .0001f < demand) break;
                    if (TryGetConsumer(output, out IPowerInput consumer))
                    {
                        if (consumer.ReceiveElectricity(demand) + .0001f < demand) break;
                    }
                    else if (TryGetPole(output, out PowerPoleStation destination))
                    {
                        available[destination] += demand;
                        if (--parents[destination] == 0) pending.Enqueue(destination);
                    }
                    else continue;
                    remaining -= demand; delivered += demand; powered++;
                }
                station.Pole.SetFlow(input, delivered, powered);
            }
            foreach (PowerPoleStation station in active.Where(station => !settled.Contains(station))) station.Pole.SetFlow(0f, 0f, 0);
        }

        private float OutputDemand(string endpointId, Dictionary<PowerPoleStation, float> available, float elapsed, HashSet<PowerPoleStation> visiting)
        {
            if (TryGetConsumer(endpointId, out IPowerInput consumer)) return consumer.RequestedElectricity(elapsed);
            if (!TryGetPole(endpointId, out PowerPoleStation target) || !visiting.Add(target)) return 0f;
            float requirement = target.Pole.OutputEndpointIds.Sum(output => OutputDemand(output, available, elapsed, visiting));
            visiting.Remove(target);
            return Mathf.Max(0f, requirement - available.GetValueOrDefault(target));
        }

        private IReadOnlyList<PowerEndpointOption> BuildOptions(PowerPoleStation station, bool input)
        {
            var options = new List<PowerEndpointOption>();
            if (station?.Pole == null || _buildings == null) return options;
            foreach (PowerPoleStation other in _stations.Where(other => IsActive(other) && other != station))
                options.Add(new PowerEndpointOption(PoleId(other), $"#{other.Pole.PoleNumber:00}", other.Site.Definition.MenuIcon));
            foreach (BuildSite site in _buildings.Sites)
            {
                if (site.State != BuildState.Completed || !AreAdjacent(station.Site, site)) continue;
                if (input && site.Definition.IsSolarPanel)
                    options.Add(new PowerEndpointOption(SolarId(site), ProducerLabel(site), site.Definition.MenuIcon));
                if (input && site.CombustionGenerator != null)
                    options.Add(new PowerEndpointOption(GeneratorId(site), ProducerLabel(site), site.Definition.MenuIcon));
                if (!input && TryGetPowerInput(site, out _)) options.Add(new PowerEndpointOption(ConsumerId(site), EndpointNumber(site, false), site.Definition.MenuIcon));
            }
            return options;
        }

        private bool CanUseEndpoint(PowerPoleStation station, string endpointId, bool input)
        {
            if (endpointId == PoleId(station)) return false;
            if (TryGetPole(endpointId, out _)) return true;
            return input
                ? (TryGetSolar(endpointId, out BuildSite source) && AreAdjacent(station.Site, source)) ||
                  (TryGetGenerator(endpointId, out BuildSite generator) && AreAdjacent(station.Site, generator))
                : TryGetConsumer(endpointId, out _, out BuildSite consumer) && AreAdjacent(station.Site, consumer);
        }
        private bool TryGetEndpoint(string id, out string label, out Sprite icon)
        {
            label = "Unavailable endpoint"; icon = null;
            if (TryGetSolar(id, out BuildSite solar)) { label = ProducerLabel(solar); icon = solar.Definition.MenuIcon; return true; }
            if (TryGetGenerator(id, out BuildSite generator)) { label = ProducerLabel(generator); icon = generator.Definition.MenuIcon; return true; }
            if (TryGetConsumer(id, out _, out BuildSite consumer)) { label = EndpointNumber(consumer, false); icon = consumer.Definition.MenuIcon; return true; }
            if (TryGetPole(id, out PowerPoleStation pole)) { label = $"#{pole.Pole.PoleNumber:00}"; icon = pole.Site.Definition.MenuIcon; return true; }
            return false;
        }
        private float SolarInput(PowerPoleStation station, float elapsed) => station.Pole.InputEndpointIds.Sum(id => TryGetSolar(id, out BuildSite site) ? site.Definition.SolarElectricityPerSecond * elapsed : 0f);
        private string ProducerLabel(BuildSite site) => $"{site.Definition.DisplayName} {EndpointNumber(site, true)}";
        private static bool AreAdjacent(BuildSite pole, BuildSite other)
        {
            if (!pole.QuarterCell.HasValue) return false;
            Vector2Int cell = new(Mathf.FloorToInt(pole.QuarterCell.Value.x / 2f), Mathf.FloorToInt(pole.QuarterCell.Value.y / 2f));
            for (int i = 0; i < other.Footprint.CellCount; i++) { Vector2Int delta = other.Footprint.CellAt(i) - cell; if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1) return true; }
            return false;
        }
        private bool TryGetSolar(string id, out BuildSite site) => TryGetSite(id, SolarPrefix, candidate => candidate.Definition.IsSolarPanel, out site);
        private bool TryGetGenerator(string id, out BuildSite site) =>
            TryGetSite(id, GeneratorPrefix, candidate => candidate.CombustionGenerator != null, out site);
        private bool TryGetConsumer(string id, out IPowerInput input) => TryGetConsumer(id, out input, out _);
        private bool TryGetConsumer(string id, out IPowerInput input, out BuildSite site)
        {
            input = null;
            site = null;
            if (_buildings == null || string.IsNullOrEmpty(id) || !id.StartsWith(ConsumerPrefix, StringComparison.Ordinal)) return false;
            string siteId = id.Substring(ConsumerPrefix.Length);
            foreach (BuildSite candidate in _buildings.Sites)
            {
                if (candidate.SiteId != siteId || candidate.State != BuildState.Completed || !TryGetPowerInput(candidate, out IPowerInput candidateInput)) continue;
                site = candidate;
                input = candidateInput;
                return true;
            }
            return false;
        }
        private static bool TryGetPowerInput(BuildSite site, out IPowerInput input) { input = site.PowerInput; return input != null; }
        private string EndpointNumber(BuildSite site, bool producer)
        {
            int number = 0;
            foreach (BuildSite candidate in _buildings.Sites)
            {
                bool matches = producer ? candidate.Definition.IsSolarPanel || candidate.CombustionGenerator != null : TryGetPowerInput(candidate, out _);
                if (!matches) continue;
                number++;
                if (candidate == site) return $"#{number:00}";
            }
            return "#--";
        }
        private bool TryGetPole(string id, out PowerPoleStation station)
        {
            station = null;
            if (string.IsNullOrEmpty(id) || !id.StartsWith(PolePrefix, StringComparison.Ordinal)) return false;
            string siteId = id.Substring(PolePrefix.Length);
            station = _stations.FirstOrDefault(candidate => IsActive(candidate) && candidate.Site.SiteId == siteId);
            return station != null;
        }
        private bool TryGetSite(string id, string prefix, Func<BuildSite, bool> predicate, out BuildSite site)
        {
            site = null;
            if (_buildings == null || string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string siteId = id.Substring(prefix.Length);
            site = _buildings.Sites.FirstOrDefault(candidate => candidate.SiteId == siteId && candidate.State == BuildState.Completed && predicate(candidate));
            return site != null;
        }
        private void RemoveEndpointFromOtherPoles(string id, PowerPoleStation owner, bool input)
        { foreach (PowerPoleStation station in _stations.Where(IsActive).Where(station => station != owner)) { if (input) station.Pole.RemoveInput(id); else station.Pole.RemoveOutput(id); } }
        private void Disconnect(PowerPoleStation station)
        { foreach (string input in station.Pole.InputEndpointIds.ToArray()) RemoveInput(station, input); foreach (string output in station.Pole.OutputEndpointIds.ToArray()) RemoveOutput(station, output); }
        private static bool IsActive(PowerPoleStation station) => station != null && station.isActiveAndEnabled && station.Site.State == BuildState.Completed && station.Pole != null;
        private static bool IsProducerEndpoint(string id) => id != null &&
            (id.StartsWith(SolarPrefix, StringComparison.Ordinal) || id.StartsWith(GeneratorPrefix, StringComparison.Ordinal));
        private static bool IsConsumerEndpoint(string id) => id != null && id.StartsWith(ConsumerPrefix, StringComparison.Ordinal);
        private static string SolarId(BuildSite site) => SolarPrefix + site.SiteId;
        private static string GeneratorId(BuildSite site) => GeneratorPrefix + site.SiteId;
        private static string ConsumerId(BuildSite site) => ConsumerPrefix + site.SiteId;
        private static string PoleId(PowerPoleStation station) => PolePrefix + station.Site.SiteId;
    }
}
