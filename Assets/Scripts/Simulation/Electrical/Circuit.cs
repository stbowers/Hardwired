#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using Assets.Scripts.Networks;
using Assets.Scripts.Objects;
using Hardwired.Objects;
using Hardwired.Objects.Electrical;

namespace Hardwired.Simulation.Electrical
{
    public class Circuit
    {
        private static int _nextId = 0;

        private Dictionary<(ElectricalComponent component, int pin), MNASolver.Unknown> _nodes = new();
        private List<ElectricalComponent> _components = new();
        private List<PowerSink> _powerSinks = new();
        private List<PowerSource> _powerSources = new();
        private bool _initialized;

        public int Id { get; } = _nextId++;

        public MNASolver Solver { get; } = new();

        public IReadOnlyList<ElectricalComponent> Components => _components.AsReadOnly();
        public IReadOnlyList<PowerSink> PowerSinks => _powerSinks.AsReadOnly();
        public IReadOnlyList<PowerSource> PowerSources => _powerSources.AsReadOnly();

        /// <summary>
        /// The frequency of any AC voltages or currents in the circuit.
        /// </summary>
        public double Frequency { get; private set; }

        /// <summary>
        /// The time delta (dt) to use between each tick.
        /// </summary>
        public double TimeDelta { get; private set; } = 0.5;

        public void AddComponent(ElectricalComponent component)
        {
            lock (Solver)
            {
                _components.Add(component);

                if (component is PowerSink powerSink)
                {
                    _powerSinks.Add(powerSink);
                }

                // If the circuit is already initialized, initialize this component now...
                // Otherwise, it will be initialized on the next power tick
                if (_initialized)
                {
                    component.Initialize(this);
                }
            }
        }

        public void RemoveComponent(ElectricalComponent component)
        {
            lock (Solver)
            {
                _components.Remove(component);
                component.Remove(this);

                if (component is PowerSink powerSink)
                {
                    _powerSinks.Remove(powerSink);
                }
            }
        }

        /// <summary>
        /// Returns the MNASolver.Unknown object representing the voltage at the node referenced by the given pin on the component.
        /// 
        /// If the component is part of a SmallGrid object, the pin is taken to be the index of 
        /// </summary>
        /// <param name="component"></param>
        /// <param name="pin"></param>
        /// <returns></returns>
        public MNASolver.Unknown? GetNode(ElectricalComponent component, int pin)
        {
            // pin -1 (or any negative pin) is the common ground
            if (pin < 0) { return null; }

            MNASolver.Unknown? node;

            if (_nodes.TryGetValue((component, pin), out node))
            {
                return node;
            }

            var peer = TryGetPeer(component, pin);
            if (peer != null)
            {
                node = _nodes.GetValueOrDefault(peer.Value);
            }

            node ??= Solver.AddUnknown();
            _nodes.Add((component, pin), node);

            return node;
        }

        /// <summary>
        /// Removes the the given component's reference to the given node.
        /// If all references to a node are gone, the node will be removed.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="pin"></param>
        /// <param name="node"></param>
        public void RemoveNodeReference(ElectricalComponent component, int pin)
        {
            // pin -1 (or any negative pin) is the common ground
            if (pin < 0) { return; }

            // Get the node registered for this connection, if one exists
            if (!_nodes.TryGetValue((component, pin), out MNASolver.Unknown node))
            {
                // No node registered for this connection, nothing to do...
                return;
            }

            // Remove the reference for this connection
            _nodes.Remove((component, pin));

            // Check if there are any other references left to this node
            bool stillAlive = _nodes.Any(entry => entry.Value == node);
            if (!stillAlive)
            {
                // If no more references, remove the node from the MNA solver
                Solver.RemoveUnknown(node);
            }
        }

        private (ElectricalComponent component, int pin)? TryGetPeer(ElectricalComponent component, int pin)
        {
            if (pin < 0) { return null; }

            if (component.TryGetComponent<SmallGrid>(out SmallGrid smallGrid))
            {
                var connection = smallGrid.OpenEnds[pin];
                var peerIndex = connection.GetPeerIndex();
                var peer = connection.GetOther(false)?.GetComponents<ElectricalComponent>().FirstOrDefault(c => c.UsesConnection(peerIndex));
                if (peer != null)
                {
                    return (peer, peerIndex);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return _nodes.Keys.FirstOrDefault(k => k.pin == pin);
            }
        }

        public void ProcessTick()
        {
            lock (Solver)
            {
                try
                {
                    Initialize();
                    Solver.Z.Clear();
                    UpdateState();
                    Solver.Solve();
                    ApplyState();
                }
                catch (Exception e)
                {
                    Hardwired.LogDebug($"Error processing tick! {e}");
                }
            }
        }

        /// <summary>
        /// Marks the circuit as uninitialized so it will be re-initialized on the next power tick
        /// </summary>
        public void Invalidate()
        {
            _initialized = false;
        }

        private void Initialize()
        {
            if (_initialized) { return; }

            InitializeFrequency();

            Hardwired.LogDebug($"Circuit network {Id} - Initializing circuit with {Components.Count} components at {Frequency} Hz");

            // Clear MNA matrix
            Solver.A.Clear();
            Solver.Z.Clear();

            foreach (var component in Components)
            {
                component.Initialize(this);
            }

            _initialized = true;
        }

        // Determine the AC frequency to use
        private void InitializeFrequency()
        {
            Frequency = 0f;
            foreach (var component in Components)
            {
                double componentFrequency;

                if (component is VoltageSource voltageSource)
                {
                    componentFrequency = voltageSource.Frequency;
                }
                else if (component is CurrentSource currentSource)
                {
                    componentFrequency = currentSource.Frequency;
                }
                else
                {
                    continue;
                }

                if (Frequency != 0f && componentFrequency != 0f && componentFrequency != Frequency)
                {
                    throw new InvalidOperationException($"Circuit network {Id} invalid -- cannot have multiple AC sources at different frequencies");
                }
                else if (componentFrequency != 0f)
                {
                    Frequency = componentFrequency;
                }
            }
        }

        private void UpdateState()
        {
            // Update solver inputs (voltage source voltages, current source currents, etc) from each component
            foreach (var component in Components)
            {
                component.UpdateState();
            }
        }

        private void ApplyState()
        {
            // Update components with solver output
            foreach (var component in Components)
            {
                component.ApplyState();
            }
        }

        public static Circuit? Merge(Circuit? a, Circuit? b)
        {
            // If a or b is null, nothing to merge (just return the other)
            if (a is null) { return b; }
            if (b is null) { return a; }

            // If a == b, nothing to merge (same network)
            if (a == b) { return a; }

            foreach (var component in b.Components.ToList())
            {
                b.RemoveComponent(component);

                component.Circuit = a;
                a.AddComponent(component);
            }

            // Clear circuit B
            b._components.Clear();
            b._nodes.Clear();

            return a;
        }
    }
}