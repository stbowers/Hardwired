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

namespace Hardwired.Utility
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
                component.Circuit = this;

                // Circuit topology has changed, so we need to reset solver
                Invalidate();
            }
        }

        public void RemoveComponent(ElectricalComponent component)
        {
            lock (Solver)
            {
                _components.Remove(component);
                component.Circuit = null;

                // Circuit topology has changed, so we need to reset solver
                Invalidate();
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
            // Otherwise, use the pin number itself as the dictionary key
            // (this is mostly used as a convenience for unit tests - most real components will use connection points, but the
            // unit tests don't have access to those, so unit tests just use a shared index for pins)
            else
            {
                MNASolver.Unknown? node;
                node = _nodes.GetValueOrDefault(pin);

                // If node doesn't exist yet, create a new one and add it
                if (node == null)
                {
                    node = Solver.AddUnknown();
                    _nodes.Add(pin, node);
                }

                return node;
            }
        }

        public MNASolver.Unknown? GetNode(Connection connection)
        {
            MNASolver.Unknown? node;

            // If there is already a known node for this connection, return it
            if (_nodes.TryGetValue(connection, out node))
            {
                return node;
            }

            // Otherwise, first check if the peer to this connection is associated with a node
            Connection? peer = connection.GetPeer();
            if (peer == null || !_nodes.TryGetValue(peer, out node))
            {
                // If the peer isn't associated to a node (or doesn't exist), create a new node
                // for this connection
                node = Solver.AddUnknown();
            }

            // Associate the node with this connection
            _nodes.Add(connection, node);

            return node;
        }

        public void ProcessTick()
        {
            var tick = DateTime.Now;

            _powerTicks += 1;

            lock (Solver)
            {
                Initialize();
                UpdateState();
                Solver.Solve();
                ApplyState();
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