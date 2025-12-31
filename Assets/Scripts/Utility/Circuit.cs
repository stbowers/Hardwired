#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;
using Assets.Scripts.Networks;
using Hardwired.Objects.Electrical;

namespace Hardwired.Utility
{
    public class Circuit : CableNetwork
    {
        private List<Component> _components = new();
        private List<Cable> _cables = new();
        private List<VoltageSource> _voltageSources = new();
        private MNASolver _solver = new();

        public IReadOnlyList<Component> Components => _components.AsReadOnly();

        public void AddComponent(Component component)
        {
            if (component is Cable cable)
            {
                _cables.Add(cable);
            }
            else if (component is VoltageSource voltageSource)
            {
                _voltageSources.Add(voltageSource);
            }

            _components.Add(component);

            PrintDebugInfo();
        }

        public void RemoveComponent(Component component)
        {
            _components.Remove(component);

            if (component is Cable cable)
            {
                _cables.Remove(cable);
            }
            else if (component is VoltageSource voltageSource)
            {
                _voltageSources.Remove(voltageSource);
            }

            PrintDebugInfo();

            RefreshNetwork();
        }

        int _powerTicks = 0;
        TimeSpan _timeProcessing = TimeSpan.Zero;
        public override void OnPowerTick()
        {
            var tick = DateTime.Now;

            _powerTicks += 1;

            if ((_powerTicks % 30) == 0)
            {
                double averageTickTime = _timeProcessing.Milliseconds / 30.0;
                _timeProcessing = TimeSpan.Zero;
                Hardwired.LogDebug($"Circuit.OnPowerTick() -- Network ID: {ReferenceId} -- Average tick time: {averageTickTime} ms -- total ticks: {_powerTicks}");
            }

            var tock = DateTime.Now;
            _timeProcessing += tock - tick;
        }

        public override bool IsNetworkValid()
        {
            return Components.Count > 0;
        }

        private void PrintDebugInfo()
        {
            StringBuilder info = new();

            info.AppendLine($"Circuit {ReferenceId} -- components: {_components.Count}");

            Hardwired.LogDebug(info);
        }

        public static Circuit? Merge(Circuit? a, Circuit? b)
        {
            if (a is null) { return b; }
            if (b is null) { return a; }

            foreach (var component in b._components)
            {
                component.Circuit = a;
                a.AddComponent(component);
                b.RemoveComponent(component);
            }

            return a;
        }
    }
}