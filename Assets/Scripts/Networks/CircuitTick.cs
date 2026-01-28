#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
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
        
        public Stopwatch TimeUpdating = new();

        public Stopwatch TimeSolving = new();

        public Stopwatch TimeApplying = new();

        public HashSet<CableNetwork> CableNetworks = new();

        public HashSet<ElectricalComponent> Components = new();

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

            UpdateState();
            Solve();
            ApplyState();

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
            HashSet<ElectricalComponent> orphanedComponents = new(Components);
            HashSet<CableNetwork> orphanedNetworks = new(CableNetworks);

            CableNetworks.Clear();
            Queue<CableNetwork> toInitialize = new();
            toInitialize.Enqueue(cableNetwork);

            // Get all cable networks that should also be in this circuit, due to being connected to a device in the circuit
            while (toInitialize.TryDequeue(out CableNetwork network))
            {
                orphanedNetworks.Remove(network);

                if (network.PowerTick is HardwiredPowerTick otherNetworkTick && otherNetworkTick.CircuitTick != this)
                {
                    otherNetworkTick.SetCircuit(this);
                }

                if (CableNetworks.Add(network))
                {
                    InitializeNetwork(network, orphanedComponents, toInitialize);
                }
            }

            // For any "orphaned" networks - unlink this CircuitTick so it will create a new circuit as needed
            foreach (var orphanedNetwork in orphanedNetworks)
            {
                (orphanedNetwork.PowerTick as HardwiredPowerTick)?.SetCircuit(null);
            }

            // Now "orphanedComponents" will only contain components that were in the circuit last tick but have since been removed
            foreach (var component in orphanedComponents)
            {
                component.RemoveFrom(Circuit);
                Components.Remove(component);
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
        private void InitializeNetwork(CableNetwork cableNetwork, HashSet<ElectricalComponent> orphanedComponents, Queue<CableNetwork> toInitialize)
        {
            var deviceComponents = cableNetwork.DeviceList.SelectMany(d => d.GetComponents<ElectricalComponent>());
            var cableComponents = cableNetwork.CableList.SelectMany(d => d.GetComponents<ElectricalComponent>());

            foreach (var component in deviceComponents.Concat(cableComponents))
            {
                orphanedComponents.Remove(component);

                if (Components.Add(component))
                {
                    component.AddTo(Circuit);
                }

                // foreach (var network in component.GetBridgedNetworks(network)) { toInitialize.Add(...); }
            }
        }

        private void UpdateState()
        {
            using var _ = TimeUpdating.BeginScope();

            foreach (var component in Components)
            {
                component.UpdateState(Circuit);
            }
        }

        private void Solve()
        {
            using var _ = TimeSolving.BeginScope();

            Circuit.ProcessTick();
        }

        /// <summary>
        /// Once the circuit has been solved, ApplyState() is called for all cable networks that are a part of the circuit to update their devices/cables,
        /// including setting the power draw/consumption, burning out overloaded cables, tripping breakers, etc.
        /// </summary>
        /// <param name="cableNetwork"></param>
        private void ApplyState()
        {
            using var _ = TimeApplying.BeginScope();

            foreach (var component in Components)
            {
                component.ApplyState(Circuit);
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

            Hardwired.LogDebug($"Circuit {Circuit.Id} performance (average per tick) -- components: {Circuit.Elements.Count}");
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