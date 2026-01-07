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

        int _powerTicks = 0;
        TimeSpan _timeProcessing = TimeSpan.Zero;
        DateTime? _lastTickRateReport;
        private Dictionary<object, MNASolver.Unknown> _nodes = new();
        private List<ElectricalComponent> _components = new();
        private bool _initialized;

        public int Id { get; } = _nextId++;

        public MNASolver Solver { get; } = new();

        public IReadOnlyList<ElectricalComponent> Components => _components.AsReadOnly();

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

            // If component is attached to a small grid object, use the pin as the index of the open end to check
            if (component.GetComponent<SmallGrid>() is SmallGrid smallGrid)
            {
                return GetNode(smallGrid.OpenEnds[pin]);
            }
            // Otherwise, use the component and pin number itself as the dictionary key
            // (this is mostly used as a convenience for unit tests - most real components will use connection points, but the
            // unit tests don't have access to those, so unit tests just use a shared index for pins)
            else
            {
                return GetNode((component, pin));
            }
        }

        public MNASolver.Unknown? GetNode(object key)
        {
            MNASolver.Unknown? node;

            // If there is already a known node for this connection, return it
            if (_nodes.TryGetValue(key, out node))
            {
                return node;
            }

            // Otherwise, first check if the peer to this connection is associated with a node
            object? peer = null;

            if (key is Connection connection)
            {
                peer = connection.GetPeer();
            }
            else if (key is (_, int pin))
            {
                peer = _nodes.Keys.FirstOrDefault(k => k is (_, int p) && p == pin);
            }

            if (peer == null || !_nodes.TryGetValue(peer, out node))
            {
                // If the peer isn't associated to a node (or doesn't exist), create a new node
                // for this connection
                node = Solver.AddUnknown();
            }

            // Associate the node with this connection
            _nodes.Add(key, node);

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

            // If component is attached to a small grid object, use the pin as the index of the open end to check
            if (component.GetComponent<SmallGrid>() is SmallGrid smallGrid)
            {
                RemoveNodeReference(smallGrid.OpenEnds[pin]);
            }
            // Otherwise, use the pin number itself as the dictionary key
            else
            {
                RemoveNodeReference((component, pin));
            }
        }

        private void RemoveNodeReference(object key)
        {
            // Get the node registered for this connection, if one exists
            if (!_nodes.TryGetValue(key, out MNASolver.Unknown node))
            {
                // No node registered for this connection, nothing to do...
                return;
            }

            // Remove the reference for this connection
            _nodes.Remove(key);

            // Check if there are any other references left to this node
            bool stillAlive = _nodes.Any(entry => entry.Value == node);
            if (!stillAlive)
            {
                // If no more references, remove the node from the MNA solver
                Solver.RemoveUnknown(node);
            }
        }

        public void ProcessTick()
        {
            var tick = DateTime.Now;

            _powerTicks += 1;

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

            var tock = DateTime.Now;
            _timeProcessing += tock - tick;

            ReportTickRate();
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

        private void ReportTickRate()
        {
            if ((_powerTicks % 30) == 0)
            {
                double averageTickProcessingTimeMs = _timeProcessing.Milliseconds / 30.0;
                double averageTickDurationMs = 500;

                if (_lastTickRateReport != null)
                {
                    averageTickDurationMs = (DateTime.Now - _lastTickRateReport.Value).TotalMilliseconds / 30f;
                }

                Hardwired.LogDebug($"Circuit network {Id} -- Average processing time: {averageTickProcessingTimeMs} ms / {averageTickDurationMs} -- components: {Components.Count}");

                _timeProcessing = TimeSpan.Zero;
                _lastTickRateReport = DateTime.Now;
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