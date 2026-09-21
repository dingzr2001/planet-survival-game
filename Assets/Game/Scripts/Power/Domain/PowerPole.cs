using System;
using System.Collections.Generic;

namespace PlanetSurvival.Power.Domain
{
    /// <summary>Session-owned routing configuration for one directional power pole.</summary>
    public sealed class PowerPole
    {
        private readonly List<string> _inputEndpointIds = new();
        private readonly List<string> _outputEndpointIds = new();

        public PowerPole(int poleNumber = 1)
        {
            PoleNumber = Math.Max(1, poleNumber);
        }

        public int PoleNumber { get; }
        public IReadOnlyList<string> InputEndpointIds => _inputEndpointIds;
        public IReadOnlyList<string> OutputEndpointIds => _outputEndpointIds;
        public float LastInputPower { get; private set; }
        public float LastDeliveredPower { get; private set; }
        public int LastPoweredOutputs { get; private set; }
        public event Action Changed;

        public bool AddInput(string endpointId) => AddUnique(_inputEndpointIds, endpointId);
        public bool AddOutput(string endpointId) => AddUnique(_outputEndpointIds, endpointId);
        public bool RemoveInput(string endpointId) => Remove(_inputEndpointIds, endpointId);
        public bool RemoveOutput(string endpointId) => Remove(_outputEndpointIds, endpointId);

        public bool MoveOutputEarlier(string endpointId) => MoveOutput(endpointId, -1);
        public bool MoveOutputLater(string endpointId) => MoveOutput(endpointId, 1);

        public void SetFlow(float inputPower, float deliveredPower, int poweredOutputs)
        {
            inputPower = Math.Max(0f, inputPower);
            deliveredPower = Math.Max(0f, deliveredPower);
            poweredOutputs = Math.Max(0, poweredOutputs);
            if (Math.Abs(LastInputPower - inputPower) < .0001f &&
                Math.Abs(LastDeliveredPower - deliveredPower) < .0001f &&
                LastPoweredOutputs == poweredOutputs)
            {
                return;
            }

            LastInputPower = inputPower;
            LastDeliveredPower = deliveredPower;
            LastPoweredOutputs = poweredOutputs;
            Changed?.Invoke();
        }

        private bool MoveOutput(string endpointId, int offset)
        {
            int index = _outputEndpointIds.IndexOf(endpointId);
            int destination = index + offset;
            if (index < 0 || destination < 0 || destination >= _outputEndpointIds.Count)
            {
                return false;
            }

            _outputEndpointIds.RemoveAt(index);
            _outputEndpointIds.Insert(destination, endpointId);
            Changed?.Invoke();
            return true;
        }

        private bool AddUnique(List<string> endpoints, string endpointId)
        {
            if (string.IsNullOrEmpty(endpointId) || endpoints.Contains(endpointId)) return false;
            endpoints.Add(endpointId);
            Changed?.Invoke();
            return true;
        }

        private bool Remove(List<string> endpoints, string endpointId)
        {
            if (!endpoints.Remove(endpointId)) return false;
            Changed?.Invoke();
            return true;
        }
    }
}
