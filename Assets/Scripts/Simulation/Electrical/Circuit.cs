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
using Hardwired.Simulation.Electrical.Elements;
using UnityEngine;

namespace Hardwired.Simulation.Electrical
{
    public class Circuit
    {
        internal static List<WeakReference<Circuit>> _allCircuits = new();

        private List<ICircuitElement> _elements = new();
        private List<PowerSource> _powerSources = new();
        private List<INonlinearCircuitElement> _nonlinearElements = new();
        private List<IFrequencySource> _frequencySources = new();
        private bool _frequencyInitialized;
        private bool _initialized;

        public int Id { get; } = new System.Random().Next();

        public MNASolver Solver { get; } = new();

        public IReadOnlyList<ICircuitElement> Elements => _elements.AsReadOnly();
        public IReadOnlyList<PowerSource> PowerSources => _powerSources.AsReadOnly();
        public IReadOnlyList<INonlinearCircuitElement> NonlinearElements => _nonlinearElements.AsReadOnly();
        public IReadOnlyList<IFrequencySource> FrequencySources => _frequencySources.AsReadOnly();

        /// <summary>
        /// The frequency of any AC voltages or currents in the circuit.
        /// </summary>
        public double Frequency { get; private set; }

        /// <summary>
        /// The time delta (dt) to use between each tick.
        /// </summary>
        public double TimeDelta { get; private set; } = 0.5;

        public int TicksProcessed { get; private set; }

        public Circuit()
        {
            _allCircuits.Add(new WeakReference<Circuit>(this));
        }

        public void AddElement(ICircuitElement element)
        {
            // Hardwired.LogDebug($"Circuit {Id} - Adding component: {component.GetType()}");

            _elements.Add(element);

            if (element is INonlinearCircuitElement nonlinearElement)
            {
                _nonlinearElements.Add(nonlinearElement);
            }

            if (element is IFrequencySource frequencySource)
            {
                _frequencySources.Add(frequencySource);
                _frequencyInitialized = false;
            }
        }

        public void RemoveElement(ICircuitElement element)
        {
            // Hardwired.LogDebug($"Circuit {Id} - Removing component: {component.GetType()}");

            // Remove from components list
            if (!_elements.Remove(element))
            {
                // If the component wasn't in this circuit to begin with, we don't have anything else to clean up...
                return;
            }

            if (element is INonlinearCircuitElement nonlinearElement)
            {
                _nonlinearElements.Remove(nonlinearElement);
            }

            if (element is IFrequencySource frequencySource)
            {
                _frequencySources.Remove(frequencySource);
                _frequencyInitialized = false;
            }
        }

        public void ProcessTick()
        {
            try
            {
                if (SolveInitial())
                {
                    // If initial solve had a solution, iterate non-linear components until convergance
                    SolveIterative();
                }
            }
            catch (Exception e)
            {
                Hardwired.LogDebug($"Circuit {Id} -- Error processing tick! {e}");

                Solver?.Z?.Clear();
                Solver?.X?.Clear();
            }

            ApplyState();
        }

        /// <summary>
        /// Marks the circuit as uninitialized so it will be re-initialized on the next power tick
        /// </summary>
        public void Invalidate()
        {
            _initialized = false;
        }

        private bool SolveInitial()
        {
            // Initialize
            InitializeFrequency();

            // Update A matrix & Z vector
            UpdateState();

            // Solve initial state (x_0)
            return Solver.SolveInitial();
        }

        private void SolveIterative()
        {
            // Skip NR iteration if there are no non-linear components (otherwise it will always try to iterate at least once)
            if (NonlinearElements.Count == 0) { return; }

            bool hasConverged = false;
            int i = 0;
            for (; i < 50 && !hasConverged; i++)
            {
                // clear/reset values
                Solver.BeginNRIteration();

                // Update J matrix & F vector
                foreach (var nonlinearComponent in NonlinearElements)
                {
                    nonlinearComponent.UpdateDifferentialState();
                }

                // Solve for x(i + 1)
                hasConverged = Solver.SolveNRIteration(i);
            }

            // Hardwired.LogDebug($"Circuit {Id} -- finished solving after {i} iterations -- converged: {hasConverged}");
        }

        // Determine the AC frequency to use
        private void InitializeFrequency()
        {
            // Check if frequency is already initialized
            if (_frequencyInitialized) { return; }

            double? frequency = null;

            foreach (var frequencySource in FrequencySources)
            {
                if (frequencySource.Frequency == null) { continue; }

                if (frequency != null && frequencySource.Frequency != frequency)
                {
                    throw new InvalidOperationException($"Circuit network {Id} invalid -- cannot have multiple AC sources at different frequencies");
                }

                frequency = frequencySource.Frequency;
            }

            Frequency = frequency ?? 0;
            _frequencyInitialized = true;
        }

        private void UpdateState()
        {
            // Update solver inputs (voltage source voltages, current source currents, etc) from each component
            foreach (var component in Elements)
            {
                component.UpdateState();
            }
        }

        private void ApplyState()
        {
            // Update components with solver output
            foreach (var component in Elements)
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

            foreach (var element in b.Elements.ToList())
            {
                b.RemoveElement(element);

                a.AddElement(element);
            }

            // Clear circuit B
            b._elements.Clear();

            return a;
        }
    }
}