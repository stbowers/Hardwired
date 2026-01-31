#nullable enable

using System;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;

namespace Hardwired.Objects.Electrical
{
    public class Generator : ElectricalComponent
    {
        private Device? _device;
        private PowerSource? _powerSource;

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            _powerSource = new(circuit, nodeA, null);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit );

            if (_powerSource != null)
            {
                _powerSource.PowerAvailable = _device?.GetGeneratedPower(_device.PowerCableNetwork) ?? 0f;
                _powerSource.UpdateState();
            }

            _powerSource?.UpdateState();
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            _powerSource?.ApplyState();

            var powerDraw = _powerSource?.Power.Real ?? 0f;
            _device?.UsePower(_device.PowerCableNetwork, (float)powerDraw);
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _powerSource?.Dispose();
            _powerSource = null;
        }
    }
}
