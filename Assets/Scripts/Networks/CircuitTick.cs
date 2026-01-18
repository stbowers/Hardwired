#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Hardwired.Objects.Electrical;
using Hardwired.Simulation.Electrical;
using Hardwired.Utility.Extensions;
using Objects.Pipes;

namespace Hardwired.Networks
{
    /// <summary>
    /// Manages updating a circuit components each tick.
    /// 
    /// Multiple CableNetworks (and their associated PowerTick) can be attached to the same circuit, and will all be solved together.
    /// This is easier than modifying the behavior of the base game's CircuitNetwork to merge what would otherwise be multiple different CircuitNetworks,
    /// and makes it easier to distinguish between the base game cable network (i.e. devices all physically attached to each other by cables; can carry
    /// power and data) and Hardwired's circuits (i.e. components all interacting electically with each other, may or may not be physically connected;
    /// only transfers power, but not data unless devices are in same cable network).
    /// </summary>
    public class CircuitTick
    {
        public Circuit Circuit = new();

        public Stopwatch TimeProcessingTick = new();

        public Stopwatch TimeInitializing = new();
        
        public Stopwatch TimeSolving = new();

        public Stopwatch TimeApplying = new();

        public HashSet<CableNetwork> CableNetworks = new();

        public int CurrentTick = 0;

        /// <summary>
        /// Processes a tick, given a "root" cable network.
        /// 
        /// Devices/components will be initialized/added to the circuit starting in the root cable network, and continuing to any other attached cable networks.
        /// 
        /// All cable networks in the circuit will be "ticked" at the same time, even though only one cable network's "PowerTick" will actually call ProcessTick().
        /// Any other PowerTicks should check `CurrentTick` and compare it to a local value so only one PowerTick will actually call ProcessTick() per tick. 
        /// </summary>
        /// <param name="root"></param>
        public void ProcessTick(CableNetwork root)
        {
            using var _ = TimeProcessingTick.BeginScope();
            CurrentTick += 1;

            Initialize(root);

            using (TimeSolving.BeginScope())
            {
                Circuit.ProcessTick();
            }

            foreach (var network in CableNetworks)
            {
                ApplyState(network);
            }

            LogMetrics();
        }


        /// <summary>
        /// Initialize/update the cable networks and components that are a part of this circuit, given a "root" cable network.
        /// 
        /// Any devices or cables in the root cable network, or in any cable network attached to a device already in the circuit,
        /// will be added to the circuit; any components that were in the circuit last tick but are no longer found will be removed
        /// from the circut.
        /// </summary>
        /// <param name="cableNetwork"></param>
        private void Initialize(CableNetwork cableNetwork)
        {
            using var _ = TimeInitializing.BeginScope();

            // TODO: it'd be better if CableNetwork called a method in PowerTick when it added/removed devices, so we could
            // handle them immediately (or at the very least, save a list of components that would need to be added/removed 
            // from the circuit on the next power tick), rather than iterate over the lists every time...

            // Get a list of components and networks that were part of the circuit last tick -- if they're not
            // found this tick we consider them "orphaned" and remove them from the circuit
            List<ElectricalComponent> orphanedComponents = Circuit.Components.ToList();
            List<CableNetwork> orphanedNetworks = CableNetworks.ToList();

            Queue<CableNetwork> toCheck = new();

            CableNetworks.Clear();

            orphanedNetworks.Remove(cableNetwork);
            toCheck.Enqueue(cableNetwork);

            // Get all cable networks that should also be in this circuit, due to being connected to a device in the circuit
            while (toCheck.TryDequeue(out CableNetwork network))
            {
                CableNetworks.Add(network);
                orphanedNetworks.Remove(network);

                foreach (var connectedNetwork in network.DeviceList.SelectMany(d => d.ConnectedCables()).Select(c => c.CableNetwork))
                {
                    // If CableNetworks already has this network, don't need to check again
                    if (CableNetworks.Contains(connectedNetwork))
                    {
                        continue;
                    }

                    toCheck.Enqueue(connectedNetwork);

                    (connectedNetwork.PowerTick as HardwiredPowerTick)?.SetCircuit(this);
                }

            }

            // For any "orphaned" networks - unlink this CircuitTick so it will create a new circuit as needed
            foreach (var orphanedNetwork in orphanedNetworks)
            {
                (orphanedNetwork.PowerTick as HardwiredPowerTick)?.SetCircuit(null);
            }

            // Hardwired.LogDebug($"Circuit {Circuit.Id} => Cable networks: [{string.Join(", ", CableNetworks.Select(n => n.ReferenceId))}] == root: {cableNetwork.ReferenceId}");

            foreach (var network in CableNetworks)
            {
                InitializeNetwork(network, orphanedComponents);
            }

            // Now "orphanedComponents" will only contain components that were in the circuit last tick but have since been removed
            foreach (var component in orphanedComponents)
            {
                component.RemoveFrom(Circuit);
            }

            // Update power sources based on how much power they will generate this tick
            foreach (var source in Circuit.PowerSources)
            {
                if (source.GetComponent<Device>() is not Device device){ continue; }

                source.PowerSetting = device.GetGeneratedPower(device.PowerCableNetwork);
            }

            // Update power sinks based on how much power they want to draw this tick
            foreach (var sink in Circuit.PowerSinks)
            {
                if (sink.GetComponent<Device>() is not Device device){ continue; }

                sink.PowerTarget = device.GetUsedPower(device.PowerCableNetwork);
            }
        }

        /// <summary>
        /// Initializes/updates the devices/cables in the given network by adding any components to the circuit (or creating them, if neccesary).
        /// 
        /// Also removes components that should be in the circuit from "orphanedComponets", which is a list of components that were in the circuit
        /// last tick. After all cable networks are initialized, any components left in "orphanedComponents" are no longer attached to the circuit,
        /// and should be removed.
        /// </summary>
        /// <param name="cableNetwork"></param>
        /// <param name="orphanedComponents"></param>
        private void InitializeNetwork(CableNetwork cableNetwork, List<ElectricalComponent> orphanedComponents)
        {
            // Iterate over all devices in the cable network
            foreach (var component in cableNetwork.DeviceList.SelectMany(d => d.GetComponents<ElectricalComponent>()))
            {
                orphanedComponents.Remove(component);

                // Skip components that are already in the circuit; but remove them from "knownComponents" list
                if (!Circuit.Components.Contains(component))
                {
                    // If a component wasn't already in the circuit, add it now
                    Circuit.AddComponent(component);
                }
            }

            // Iterate over all cables in the cable network
            foreach (var component in cableNetwork.CableList.SelectMany(d => d.GetComponents<ElectricalComponent>()))
            {
                orphanedComponents.Remove(component);

                // Skip components that are already in the circuit; but remove them from "knownComponents" list
                if (!Circuit.Components.Contains(component))
                {
                    // If a component wasn't already in the circuit, add it now
                    Circuit.AddComponent(component);
                }
            }

            // Update power sources/sinks
            foreach (var device in cableNetwork.PowerDeviceList)
            {
                var generatedPower = device.GetGeneratedPower(cableNetwork);
                var usedPower = device.GetUsedPower(cableNetwork);

                PowerSource? powerSource = device.GetComponent<PowerSource>();
                PowerSink? powerSink = device.GetComponent<PowerSink>();

                if (generatedPower > 0.1 && powerSource == null)
                {
                    powerSource = device.gameObject.AddComponent<PowerSource>();

                    powerSource.NominalPower = 500;
                    powerSource.VoltageNominal = 200;
                    powerSource.Frequency = 60;
                    powerSource.IsFrequencyDriver = true;

                    powerSource.PinA = -1;
                    powerSource.PinB = device.OpenEnds.FindIndex(c => (c.ConnectionType & NetworkType.Power) != NetworkType.None);

                    Circuit.AddComponent(powerSource);
                }

                if (usedPower > 0.1 && powerSink == null)
                {
                    powerSink = device.gameObject.AddComponent<PowerSink>();

                    powerSink.VoltageMin = 100;
                    powerSink.VoltageNominal = 200;
                    powerSink.VoltageMax = 400;

                    powerSink.PinA = device.OpenEnds.FindIndex(c => (c.ConnectionType & NetworkType.Power) != NetworkType.None);
                    powerSink.PinB = -1;

                    // Make volume pumps inductive (this is just an example - eventually I'd like to have data-driven power profiles for devices)
                    if (device is VolumePump volPump)
                    {
                        powerSink.Inductance = 2;
                    }

                    Circuit.AddComponent(powerSink);
                }

                // Update power source
                if (powerSource != null)
                {
                    powerSource.PowerSetting = generatedPower;
                }

                // Update power sink
                if (powerSink != null)
                {
                    if (powerSink.MaxPower < usedPower)
                    {
                        powerSink.MaxPower = usedPower;
                        powerSink.Deinitialize();
                        powerSink.Initialize();
                    }

                    powerSink.PowerTarget = generatedPower;
                }
            }

            // Update cables
            foreach (var cable in cableNetwork.CableList)
            {
                // If cable already has a component, don't add anything else
                if (cable.GetComponent<ElectricalComponent>() != null) { continue; }

                // Otherwise, add a "line" component between each connection
                for (int i = 0; i < cable.OpenEnds.Count; i++)
                {
                    for (int j = i + 1; j < cable.OpenEnds.Count; j++)
                    {
                        Line line = cable.gameObject.AddComponent<Line>();

                        // Partially based on physical properties of copper wire ~2mm diameter
                        // Partially balanced around 25A max for normal cables (~5 kW @ 200V)
                        line.Resistance = 0.002;
                        line.SpecificHeat = 0.025; // how fast should a cable heat up with load
                        line.Temperature = 293.15;

                        // I^2 * R = D * T_c
                        // --> balance point @ 90 C, 10A --> D ~= 0.002
                        var iTarget = 10;
                        var tTarget = 90;
                        line.DissipationCapacity = iTarget * iTarget * line.Resistance / tTarget;

                        line.PinA = i;
                        line.PinB = j;

                        Hardwired.LogDebug($"Added resistor to cable between connection {i} and {j}");

                        Circuit.AddComponent(line);
                    }
                }
            }
        }

        /// <summary>
        /// Once the circuit has been solved, ApplyState() is called for all cable networks that are a part of the circuit to update their devices/cables,
        /// including setting the power draw/consumption, burning out overloaded cables, tripping breakers, etc.
        /// </summary>
        /// <param name="cableNetwork"></param>
        private void ApplyState(CableNetwork cableNetwork)
        {
            using var _ = TimeApplying.BeginScope();

            // TODO: Check fuses/breakers/etc

            // Use power from sources
            foreach (var source in Circuit.PowerSources)
            {
                if (source.GetComponent<Device>() is not Device device){ continue; }

                // Note - Device.UsePower()/ReceivePower() expect power in Watts, but for easier energy calculations the power source/sink
                // calculate the actual energy used in this tick, so we need to convert back when sending to the device.
                device.UsePower(cableNetwork, (float)(source.EnergyOutput / Circuit.TimeDelta));
            }

            // Apply power to sinks
            foreach (var sink in Circuit.PowerSinks)
            {
                // Get device this sink is attached to
                if (sink.GetComponent<Device>() is not Device device){ continue; }

                // Note - Device.UsePower()/ReceivePower() expect power in Watts, but for easier energy calculations the power source/sink
                // calculate the actual energy used in this tick, so we need to convert back when sending to the device.
                device.ReceivePower(cableNetwork, (float)(sink.EnergyInput / Circuit.TimeDelta));

                if (sink.EnergyInput > 0)
                {
                    device.SetPowerFromThread(cableNetwork, true).Forget();
                }
                else
                {
                    device.SetPowerFromThread(cableNetwork, false).Forget();
                }
            }

            // Update cable heat
            foreach (var cable in cableNetwork.CableList)
            {
                Line[] lines = cable.GetComponents<Line>();
                if (lines.Length == 0) { continue; }

                // Set all cables to average temp (in case more current is going down one path)
                var cableTemp = lines.Average(l => l.Temperature);
                foreach (var line in lines)
                {
                    line.Temperature = cableTemp;
                }

                // Randomly break cables that are over temp, with a higher chance the hotter they are
                // 100 C ~ 0%
                // 150 C ~ 100%
                double breakChance = (cableTemp - 373.15) / 50f;
                breakChance = Math.Clamp(breakChance, 0f, 1f);

                bool shouldBreak = breakChance >= UnityEngine.Random.Range(0f, 1f);
                if (shouldBreak)
                {
                    var pd = lines.Average(l => l.PowerDissipated);
                    var v = lines.Average(l => l.Voltage.Magnitude);
                    var vp = lines.Average(l => l.Voltage.Phase);
                    var a = lines.Average(l => l.Current.Magnitude);
                    var ap = lines.Average(l => l.Current.Phase);
                    var r = lines.Average(l => l.Resistance);
                    Hardwired.LogDebug($"Breaking cable -- temp: {cableTemp - 273.15} C -- {pd} W -- {v} V ({vp})-- {a} A ({ap})-- {r} Ohm");
                    cable.Break();
                }
            }
        }

        private void LogMetrics()
        {
            // Log metrics every minute ~= 120 ticks
            if ((CurrentTick % 120) != 0) { return; }

            TimeSpan averageTimeInitializing = TimeInitializing.Elapsed / 120;
            TimeSpan averageTimeSolving = TimeSolving.Elapsed / 120;
            TimeSpan averageTimeApplying = TimeApplying.Elapsed / 120;
            TimeSpan averageTickProcessingTime = TimeProcessingTick.Elapsed / 120;

            Hardwired.LogDebug($"Circuit {Circuit.Id} performance (average per tick) -- components: {Circuit.Components.Count}");
            Hardwired.LogDebug($"  Initialize(): {averageTimeInitializing.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  Circuit.ProcessTick(): {averageTimeSolving.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  ApplyState(): {averageTimeApplying.TotalMilliseconds} ms");
            Hardwired.LogDebug($"  Total: {averageTickProcessingTime.TotalMilliseconds} ms");

            TimeInitializing.Reset();
            TimeSolving.Reset();
            TimeApplying.Reset();
            TimeProcessingTick.Reset();
        }
    }
}