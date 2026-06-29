#nullable enable

using System;
using System.Numerics;
using Hardwired.Utility;
using MathNet.Numerics;

namespace Hardwired.Simulation.Electrical.Elements
{
    /// <summary>
    /// A mutual inductor/transformer
    /// 
    /// Note - this transformer only works for AC. It might be worth replacing with an "ideal transformer" eventually, which also works for DC.
    /// The main difference between a mutual inductor and ideal transformer in MNA would usually just be for transient/waveform analysis, which we're not really doing...
    /// </summary>
    public class MutualInductor : CircuitElementBase
    {
        private MNASolver.Unknown? _i1;
        private MNASolver.Unknown? _i2;
        private double? _appliedN;
        private double? _appliedFrequency;

        /// <summary>
        /// The node for pin "A" (the positive pin of the primary coil)
        /// </summary>
        public virtual RefCounted<MNASolver.Unknown>? NodeA { get; }

        /// <summary>
        /// The node for pin "B" (the negative pin of the primary coil)
        /// </summary>
        public virtual RefCounted<MNASolver.Unknown>? NodeB { get; }

        /// <summary>
        /// The node for pin "C" (the positive pin of the secondary coil)
        /// </summary>
        public virtual RefCounted<MNASolver.Unknown>? NodeC { get; }

        /// <summary>
        /// The node for pin "D" (the negative pin of the secondary coil)
        /// </summary>
        public virtual RefCounted<MNASolver.Unknown>? NodeD { get; }

        /// <summary>
        /// The current flowing from node A to B (through the primary coil)
        /// </summary>
        public virtual Complex PrimaryCurrent => Circuit.Solver.GetValueOrDefault(_i1);

        /// <summary>
        /// The current flowing from node C to D (through the secondary coil)
        /// </summary>
        public virtual Complex SecondaryCurrent => Circuit.Solver.GetValueOrDefault(_i2);

        /// <summary>
        /// The voltage at node "A", compared to a common reference (i.e. ground)
        /// </summary>
        public virtual Complex VoltageA => Circuit.Solver.GetValueOrDefault(NodeA?.Value);
        
        /// <summary>
        /// The voltage at node "B", compared to a common reference (i.e. ground)
        /// </summary>
        public virtual Complex VoltageB => Circuit.Solver.GetValueOrDefault(NodeB?.Value);

        /// <summary>
        /// The voltage at node "C", compared to a common reference (i.e. ground)
        /// </summary>
        public virtual Complex VoltageC => Circuit.Solver.GetValueOrDefault(NodeC?.Value);
        
        /// <summary>
        /// The voltage at node "D", compared to a common reference (i.e. ground)
        /// </summary>
        public virtual Complex VoltageD => Circuit.Solver.GetValueOrDefault(NodeD?.Value);

        /// <summary>
        /// The voltage delta between nodes A and B (primary coil)
        /// </summary>
        public virtual Complex PrimaryVoltageDelta => VoltageA - VoltageB;

        /// <summary>
        /// The voltage delta between nodes C and D (secondary coil)
        /// </summary>
        public virtual Complex SecondaryVoltageDelta => VoltageC - VoltageD;

        /// <summary>
        /// The apparent (complex) power being consumed/produce by the primary coil.
        /// 
        /// The real part indicates the "real power" consumed (or produced, if negative) by this element.
        /// The imaginary part indicates the "reactive power", which only exists in AC circuits and indicates how much power is flowing back and forth without actually being used due to voltage
        /// and current being out of phase.
        /// </summary>
        public virtual Complex PrimaryPower => PrimaryVoltageDelta * PrimaryCurrent.Conjugate();

        /// <summary>
        /// The apparent (complex) power being consumed/produced by the secondary coil.
        /// 
        /// The real part indicates the "real power" consumed (or produced, if negative) by this element.
        /// The imaginary part indicates the "reactive power", which only exists in AC circuits and indicates how much power is flowing back and forth without actually being used due to voltage
        /// and current being out of phase.
        /// </summary>
        public virtual Complex SecondaryPower => SecondaryVoltageDelta * SecondaryCurrent.Conjugate();

        /// <summary>
        /// The ratio of real power to apparent power (i.e. `Power.Real / Power.Magnitude`) for the primary coil
        /// </summary>
        public virtual double PrimaryPowerFactor
        {
            get
            {
                var s = PrimaryPower;
                return Math.Abs(s.Real) / s.Magnitude;
            }
        }

        /// <summary>
        /// The ratio of real power to apparent power (i.e. `Power.Real / Power.Magnitude`) for the secondary coil
        /// </summary>
        public virtual double SecondaryPowerFactor
        {
            get
            {
                var s = SecondaryPower;
                return Math.Abs(s.Real) / s.Magnitude;
            }
        }

        /// <summary>
        /// The ratio of primary windings to secondary windings; V(C-D) = N * V(A-B)
        /// </summary>
        public double N { get; set; }

        public MutualInductor(Circuit circuit, RefCounted<MNASolver.Unknown>? nodeA, RefCounted<MNASolver.Unknown>? nodeB, RefCounted<MNASolver.Unknown>? nodeC, RefCounted<MNASolver.Unknown>? nodeD) : base(circuit)
        {
            NodeA = nodeA?.Clone();
            NodeB = nodeB?.Clone();
            NodeC = nodeC?.Clone();
            NodeD = nodeD?.Clone();
        }

        public override void Dispose()
        {
            base.Dispose();

            RemoveState();

            NodeA?.Dispose();
            NodeB?.Dispose();
            NodeC?.Dispose();
            NodeD?.Dispose();
        }

        public override void UpdateState()
        {
            base.UpdateState();

            // If input values are the same as what was last applied to the circuit, nothing to do...
            if (_appliedN == N && _appliedFrequency == Circuit.Frequency)
            {
                return;
            }

            // Remove previous state
            RemoveState();

            // Only works in AC
            if (Circuit.Frequency != 0f)
            {
                // TODO: Make input parameters
                // Note - l1 is the primary coil inductance, l2 is the secondary coil inductance, and m is the mutual inductance between the two coils.
                // l1 should be set based on the expected current draw and voltage leak, and in the future may need to be calculated based on other properties (like max power draw, voltage leak, etc).
                // In the past this value was 10 H, but that was too high and caused the simulation to be unstable (too much voltage leak due to high reactive impedance).
                // 0.1 H seems to work well for now, but may need to be adjusted in the future.
                // One side effect of a lower inductance value is that the transformer will have a higher reactive power draw, which still causes resistive losses in the circuit even when there is 0 real power draw, which is unexpected.
                var l1 = 0.1;
                var l2 = l1 * N * N;
                var k = 0.999;
                var m = k * l1 * N;

                var w = 2f * Math.PI * Circuit.Frequency;
                var wL1 = w * l1;
                var wL2 = w * l2;
                var wM = w * m;

                Circuit.Solver.AddTransformer(NodeA?.Value, NodeB?.Value, NodeC?.Value, NodeD?.Value, wL1, wL2, wM, ref _i1, ref _i2);
            }

            _appliedN = N;
            _appliedFrequency = Circuit.Frequency;
        }

        private void RemoveState()
        {
            _i1?.Dispose();
            _i2?.Dispose();

            _i1 = null;
            _i2 = null;

            _appliedN = null;
            _appliedFrequency = null;
        }
    }
}
