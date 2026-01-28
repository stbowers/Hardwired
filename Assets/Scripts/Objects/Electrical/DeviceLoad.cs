#nullable enable

using System;
using System.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Hardwired.Simulation.Electrical;
using Hardwired.Simulation.Electrical.Elements;

namespace Hardwired.Objects.Electrical
{
    public class DeviceLoad : ElectricalComponent
    {
        private Device? _device;
        private PowerSink? _powerSink;

        public PowerSink.PowerProfile PowerProfile { get; set; } = PowerSink.PowerProfile.Default;

        public override void AddTo(Circuit circuit)
        {
            base.AddTo(circuit);

            _device ??= GetComponent<Device>();

            var nodeA = GetNode(circuit, PowerInput, WireType.Line1);
            var nodeB = GetNode(circuit, PowerOutput, WireType.Neutral);
            _powerSink = new(circuit, nodeA, nodeB);
        }

        public override void UpdateState(Circuit circuit)
        {
            base.UpdateState(circuit );

            if (_powerSink != null)
            {
                _powerSink.Profile = PowerProfile;
                _powerSink.PowerTarget = _device?.GetUsedPower(_device.PowerCableNetwork) ?? 0f;
                _powerSink.UpdateState();
            }
        }

        public override void ApplyState(Circuit circuit)
        {
            base.ApplyState(circuit);

            _powerSink?.ApplyState();

            if (_powerSink?.PowerDraw > 0f)
            {
                _device?.ReceivePower(_device.PowerCableNetwork, (float)_powerSink.PowerDraw);
                _device?.SetPowerFromThread(_device.PowerCableNetwork, true).Forget();
            }
            else
            {
                _device?.SetPowerFromThread(_device.PowerCableNetwork, false).Forget();
            }
        }

        public override void RemoveFrom(Circuit circuit)
        {
            base.RemoveFrom(circuit);

            _powerSink?.Dispose();
            _powerSink = null;
        }
    }
}
