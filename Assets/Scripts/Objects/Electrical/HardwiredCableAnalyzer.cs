#nullable enable

using System.Numerics;
using System.Text;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;

namespace Hardwired.Objects.Electrical
{
    public class HardwiredCableAnalyzer : ElectricalComponent
    {
        protected CableAnalyser? BaseGameCableAnalyzer => Device as CableAnalyser;
        protected Cable? MountedTo => BaseGameCableAnalyzer?.SmallCell?.Cable;
        protected HardwiredCable? HardwiredCable => MountedTo?.GetComponent<HardwiredCable>();

        public double CircuitFrequency = 0;

        public Complex Voltage = 0;

        public double Current = 0;

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            if (CircuitFrequency != 0)
            {
                stringBuilder.AppendLine($"f: {HardwiredCable?.InputCircuit?.Frequency.ToStringPrefix("Hz", "yellow")}");
            }

            stringBuilder.AppendLine($"V: {Voltage.Magnitude.ToStringPrefix("V", "yellow")}");
            stringBuilder.AppendLine($"I: {Current.ToStringPrefix("A", "yellow")}");
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            var hardwiredCable = MountedTo?.GetComponent<HardwiredCable>();

            if (hardwiredCable == null)
            {
                CircuitFrequency = 0;
                Voltage = 0;
                Current = 0;
                return;
            }

            CircuitFrequency = hardwiredCable.InputCircuit?.Frequency ?? 0;
            Voltage = hardwiredCable.MaximumVoltage;
            Current = hardwiredCable.Current;
        }
    }
}