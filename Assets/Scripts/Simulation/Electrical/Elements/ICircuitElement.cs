#nullable enable

using System;

namespace Hardwired.Simulation.Electrical.Elements
{
    public interface ICircuitElement : IDisposable
    {
        public Circuit Circuit { get; }

        public void UpdateState();

        public void ApplyState();

        public string DebugInfo() => "";
    }
}