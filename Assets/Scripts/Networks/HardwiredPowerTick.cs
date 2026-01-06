#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Pipes;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;

namespace Hardwired.Networks
{
    /// <summary>
    /// Hardwired's replacement for the base game PowerTick class.
    /// </summary>
    public partial class HardwiredPowerTick : PowerTick
    {
        public Circuit Circuit = new();

        public long TicksProcessed;

        public Stopwatch TimeProcessingTick = new();

        public Stopwatch TimeInitializing = new();
        
        public Stopwatch TimeSolving = new();

        public Stopwatch TimeApplying = new();

        public new void Initialise(CableNetwork cableNetwork)
        {
            TimeProcessingTick.Start();
            TimeInitializing.Start();

            CableNetwork = cableNetwork;

            // TODO: it'd be better if CableNetwork called a method in PowerTick when it added/removed devices, so we could
            // handle them immediately (or at the very least, save a list of components that would need to be added/removed 
            // from the circuit on the next power tick), rather than iterate over the lists every time...

            // Get list of components already added to the circuit
            List<ElectricalComponent> knownComponents = Circuit.Components.ToList();

            // Iterate over all components in the cable network
            foreach (var component in cableNetwork.DeviceList.SelectMany(d => d.GetComponents<ElectricalComponent>()))
            {
                // Skip components that are already in the circuit; but remove them from "knownComponents" list
                if (knownComponents.Remove(component)) { continue; }

                // If a component wasn't already in the circuit, add it now
                Circuit.AddComponent(component);
            }

            // Now "knownComponents" will only contain components that were in the circuit last tick but have since been removed
            foreach (var component in knownComponents)
            {
                Circuit.RemoveComponent(component);
            }

            // Update power sources based on how much power they will generate this tick
            foreach (var source in Circuit.PowerSources)
            {
                if (source.GetComponent<Device>() is not Device device){ continue; }

                source.PowerSetting = device.GetGeneratedPower(CableNetwork);
            }

            // Update power sinks based on how much power they want to draw this tick
            foreach (var sink in Circuit.PowerSinks)
            {
                if (sink.GetComponent<Device>() is not Device device){ continue; }

                sink.PowerTarget = device.GetUsedPower(CableNetwork);
            }

            TimeInitializing.Stop();
        }

        public new void CalculateState()
        {
            TimeSolving.Start();

            Circuit.ProcessTick();

            TimeSolving.Stop();
        }

        public new void ApplyState()
        {
            TimeApplying.Start();

            // TODO: Check fuses/breakers/etc
            // TODO: Check cable temperatures; burn out if too hot
            // TODO: Apply power to devices

            // Use power from sources
            foreach (var source in Circuit.PowerSources)
            {
                if (source.GetComponent<Device>() is not Device device){ continue; }

                device.UsePower(CableNetwork, (float)source.EnergyInput);
            }

            // Apply power to sinks
            foreach (var sink in Circuit.PowerSinks)
            {
                // Get device this sink is attached to
                if (sink.GetComponent<Device>() is not Device device){ continue; }

                device.ReceivePower(CableNetwork, (float)sink.EnergyOutput);

                if (sink.EnergyOutput > 0)
                {
                    device.SetPowerFromThread(CableNetwork, true).Forget();
                }
                else
                {
                    device.SetPowerFromThread(CableNetwork, false).Forget();
                }
            }

            Required = 0;
            Consumed = 0;
            Potential = 0;

            TimeApplying.Stop();
            TimeProcessingTick.Stop();

            TicksProcessed += 1;

            LogMetrics();
        }

        private void LogMetrics()
        {
            // Log metrics every minute ~= 120 ticks
            if ((TicksProcessed % 120) != 0) { return; }

            TimeSpan averageTimeInitializing = TimeInitializing.Elapsed / 120;
            TimeSpan averageTimeSolving = TimeSolving.Elapsed / 120;
            TimeSpan averageTimeApplying = TimeApplying.Elapsed / 120;
            TimeSpan averageTickProcessingTime = TimeProcessingTick.Elapsed / 120;

            Hardwired.LogDebug($"Network {CableNetwork.ReferenceId} performance -- components: {Circuit.Components.Count}");
            Hardwired.LogDebug($"  Initialise(): {averageTimeInitializing.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  CalculateState(): {averageTimeSolving.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  ApplyState(): {averageTimeApplying.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  Total: {averageTickProcessingTime.TotalMilliseconds} ms");

            TimeInitializing.Reset();
            TimeSolving.Reset();
            TimeApplying.Reset();
            TimeProcessingTick.Reset();
        }

    }
}