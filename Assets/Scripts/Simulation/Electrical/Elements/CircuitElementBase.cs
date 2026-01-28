#nullable enable

namespace Hardwired.Simulation.Electrical.Elements
{
    public class CircuitElementBase : ICircuitElement
    {
        public Circuit Circuit { get; }

        public CircuitElementBase(Circuit circuit)
        {
            Circuit = circuit;

            Circuit.AddElement(this);
        }

        public virtual void UpdateState()
        {
        }

        public virtual void ApplyState()
        {
        }

        public virtual void Dispose()
        {
            Circuit.RemoveElement(this);
        }
    }
}