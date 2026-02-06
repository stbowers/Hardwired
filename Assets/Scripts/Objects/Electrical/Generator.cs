#nullable enable

using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Util;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;
using Hardwired.Utility.Extensions;

namespace Hardwired.Objects.Electrical
{
    public class Generator : ElectricalComponent
    {
        private Device? _device;
        private PowerSource? _powerSource;

        public double PowerAvailable { get; private set; }

        public double PowerDraw { get; private set; }

        public double PowerFactor { get; private set; }

        public Complex VoltageDelta { get; private set; }

        public Complex CurrentDraw { get; private set; }

        public override void BuildPassiveToolTip(StringBuilder stringBuilder)
        {
            base.BuildPassiveToolTip(stringBuilder);

            stringBuilder.AppendLine($"Power Available: {PowerAvailable.ToStringPrefix("W", "yellow")}");
            stringBuilder.AppendLine($"Power Draw: {PowerDraw.ToStringPrefix("W", "yellow")} | PF: {PowerFactor}");
            stringBuilder.AppendLine($"ΔV: {VoltageDelta.ToStringPrefix(OutputCircuit?.Frequency, "V", "yellow")} | Current Draw: {CurrentDraw.ToStringPrefix(OutputCircuit?.Frequency, "A", "yellow")}");
        }

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            _powerSource = new(circuit, nodeA, null);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit);

            if (_powerSource != null)
            {
                _powerSource.PowerAvailable = _device?.GetGeneratedPower(_device.PowerCableNetwork) ?? 0;
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            PowerAvailable = _powerSource?.PowerAvailable ?? 0;
            PowerDraw = _powerSource?.Power.Real ?? 0;
            PowerFactor = _powerSource?.PowerFactor ?? 0;
            VoltageDelta = _powerSource?.VoltageDelta ?? 0;
            CurrentDraw = _powerSource?.Current ?? 0;

            _device?.UsePower(_device.PowerCableNetwork, (float)PowerDraw);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _powerSource?.Dispose();
            _powerSource = null;
        }
    }
}
