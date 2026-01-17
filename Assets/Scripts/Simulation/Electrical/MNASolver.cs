#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Hardwired.Utility.Extensions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using Complex = System.Numerics.Complex;

namespace Hardwired.Simulation.Electrical
{
    /// <summary>
    /// A Modified Node Analysis solver which can solve most linear circuits, both AC and DC, by using a system of equations built up using Kirchoff's laws.
    /// 
    /// Non-linear components can be approximated using various strategies, though I don't think we'll actually end up needing any non-linear components...
    /// 
    /// References:
    /// - https://en.wikipedia.org/wiki/Nodal_analysis
    /// - https://en.wikipedia.org/wiki/Mesh_analysis
    /// - https://en.wikipedia.org/wiki/System_of_linear_equations
    /// - https://github.com/age-series/ElectricalAge/blob/main/src/main/java/mods/eln/sim/mna/SubSystem.java
    /// - https://ecircuitcenter.com/SpiceTopics/Nodal%20Analysis/Nodal%20Analysis.htm#top
    /// - https://cheever.domains.swarthmore.edu/Ref/mna/MNA2.html
    /// </summary>
    public class MNASolver
    {
        private List<Unknown> _unknownValues = new();

        /// <summary>
        /// Matrix of coefficients for the system of equations.
        /// 
        /// A * x = z
        /// 
        /// Where "x" is a vector of unknown values (to be solved for), and "z" is a vector of known values.
        /// 
        /// A[n, m] for values of n and m between 0 and `_nodes` represents the admittance between the nodes
        /// n and m, which is a measure of the ability for electricity to flow between two points (for simple
        /// circuits this is essentially the reciprocol of resistance, though other properties can contribute
        /// to this too, especially for AC circuits).
        /// 
        /// Row A[(_nodes + v), _] for values of v between 0 and `_voltageSources` represents the linear equation
        /// for the voltage of voltage source `v`.
        /// 
        /// Column A[n, (_nodes + v)] for values of v between 0 and `_voltageSources` represents the contribution
        /// of voltage source `v`'s current to node n.
        /// </summary>
        public Matrix<Complex> A = Matrix<Complex>.Build.Dense(0, 0);

        private LU<Complex>? _A_LU;

        /// <summary>
        /// Vector of unknown values to be solved for.
        /// The first `_nodes` values will be the voltages at each node.
        /// The next `_voltageSources` values will be the currents across each voltage source.
        /// </summary>
        public Matrix<Complex>? X;

        /// <summary>
        /// Vector of known values to be used as inputs.
        /// The first `_nodes` values will be the current flowing through each node from current sources (positive values indicate current flowing out of the node).
        /// The next `_voltageSources` values will be the voltage of each voltage source.
        /// </summary>
        public Matrix<Complex> Z = Matrix<Complex>.Build.Dense(0, 1);

        public IReadOnlyList<Unknown> Unknowns => _unknownValues.AsReadOnly();

        /// <summary>
        /// Adds the given admittance value between the given nodes.
        /// 
        /// If either node is null, it is assumed to be the common ground node.
        /// 
        /// Admittance is a complex value that describes the ability of electricity to flow between two points.
        /// The real part represents conductance (i.e. reciprocal of resistance).
        /// The imaginary part represents susceptance (i.e. reciprocal of reactance).
        /// 
        /// The reciprocal of admittance is also known as impedance.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="a"></param>
        public void AddAdmittance(Unknown? a, Unknown? b, Complex value)
        {
            if (a != null)
            {
                A[a.Index, a.Index] += value;
            }

            if (b != null)
            {
                A[b.Index, b.Index] += value;
            }

            if (a != null && b != null)
            {
                A[a.Index, b.Index] -= value;
                A[b.Index, a.Index] -= value;
            }

            // Since A was modified, invalidate factorization so it will be re-factored on the next solve
            _A_LU = null;
        }

        /// <summary>
        /// Adds the given impedance value between the given nodes.
        /// Impedance is a complex value representing the resistance to current.
        /// The real part of impedance is resistance.
        /// The imaginary part of impedance is reactance.
        /// 
        /// If n is null, it is assumed to be the common ground node.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="value"></param>
        public void AddImpedance(Unknown? a, Unknown? b, Complex value)
            => AddAdmittance(a, b, 1f / value);

        /// <summary>
        /// Adds the given resistance value between the given nodes.
        /// Resistance is the real part of impedence, and represents the resistance to current flow.
        /// 
        /// If n is null, it is assumed to be the common ground node.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="value"></param>
        public void AddResistance(Unknown? a, Unknown? b, double value)
            => AddImpedance(a, b, new Complex(value, 0));

        /// <summary>
        /// Adds the given reactance value between the given nodes.
        /// Reactance is the imaginary part of impedence, and represents the resistance to change in current flow.
        /// 
        /// If n is null, it is assumed to be the common ground node.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="value"></param>
        public void AddReactance(Unknown? a, Unknown? b, double value)
            => AddImpedance(a, b, new Complex(0, value));

        /// <summary>
        /// Adds a voltage source to the system of equations, representing the equation `V(m) - V(n) = z[v]`.
        /// 
        /// If either `n` or `m` is null, it is assumed to be the common ground node.
        /// 
        /// Must be called when initializing the solver, as this method modifies the A matrix.
        /// 
        /// The return value is the index of the voltage source, which can be used with `SetVoltage()` to set the voltage as an input to the system.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        public void AddVoltageSource(Unknown? a, Unknown? b, ref Unknown? i)
        {
            // Add an unknown for the current
            i ??= AddUnknown();

            if (a != null)
            {
                // V(n) ... = V
                A[i.Index, a.Index] = -1;

                // Add the voltage source's current to the destination node
                A[a.Index, i.Index] = -1;
            }

            if (b != null)
            {
                // ... -V(m) = V
                A[i.Index, b.Index] = 1;

                // Subtract the voltage source's current from the source node
                A[b.Index, i.Index] = 1;
            }
        }

        /// <summary>
        /// w = 2pi * f
        /// wl1 = w * l1
        /// wl2 = w * l2
        /// wm = w * m
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <param name="wl1"></param>
        /// <param name="wl2"></param>
        /// <param name="wm"></param>
        /// <param name="i1"></param>
        /// <param name="i2"></param>
        public void AddTransformer(Unknown? a, Unknown? b, Unknown? c, Unknown? d, double wL1, double wL2, double wM, ref Unknown? i1, ref Unknown? i2)
        {
            // Add 2 new unknowns for the current through each inductor
            i1 ??= AddUnknown();
            i2 ??= AddUnknown();

            if (a != null)
            {
                A[a.Index, i1.Index] += 1;
                A[i1.Index, a.Index] += 1;
            }

            if (b != null)
            {
                A[b.Index, i1.Index] -= 1;
                A[i1.Index, b.Index] -= 1;
            }

            if (c != null)
            {
                A[c.Index, i2.Index] += 1;
                A[i2.Index, c.Index] += 1;
            }

            if (d != null)
            {
                A[d.Index, i2.Index] -= 1;
                A[i2.Index, d.Index] -= 1;
            }

            A[i1.Index, i1.Index] -= new Complex(0, wL1);
            A[i1.Index, i2.Index] -= new Complex(0, wM);
            A[i2.Index, i1.Index] -= new Complex(0, wM);
            A[i2.Index, i2.Index] -= new Complex(0, wL2);
        }

        /// <summary>
        /// Sets the voltage for the voltage source with unknown current `i` to the given value.
        /// 
        /// `AddVoltageSource()` must be called before this method to ensure the system of equations was correctly set up.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="value"></param>
        public void SetVoltage(Unknown? i, Complex value)
        {
            if (i == null) { return; }

            // Set input voltage to the given value
            Z[i.Index, 0] = value;
        }

        /// <summary>
        /// Sets the current between two nodes to a specific value, repersenting a current source in the circuit.
        /// 
        /// If either n or m is null, it is assumed to be the common ground node.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="i"></param>
        public void AddCurrent(Unknown? a, Unknown? b, Complex value)
        {
            if (a != null)
            {
                Z[a.Index, 0] -= value;
            }

            if (b != null)
            {
                Z[b.Index, 0] += value;
            }
        }

        /// <summary>
        /// Solves the system for the voltage at each node and current through each voltage source.
        /// </summary>
        public void Solve()
        {
            // Try to factorize A into L & U matricies, if not already set up
            _A_LU ??= A.LU();

            if (_A_LU.Determinant == 0)
            {
                X = null;
                throw new InvalidOperationException($"Circuit cannot be solved (singular matrix)!");
            }

            // Solve for x
            X = _A_LU.Solve(Z);

        }

        public Unknown AddUnknown()
            => AddUnknowns(1)[0];

        public Unknown[] AddUnknowns(int count)
        {
            Unknown[] newUnknowns = new Unknown[count];

            for (int i = 0; i < count; i++)
            {
                newUnknowns[i] = new() { Index = _unknownValues.Count };
                _unknownValues.Add(newUnknowns[i]);
            }

            var newSize = _unknownValues.Count;
            A = A.Resize(newSize, newSize);
            Z = Z.Resize(newSize, 1);

            _A_LU = null;
            X = null;

            return newUnknowns;
        }

        public void RemoveUnknown(Unknown? unknown)
        {
            if (unknown == null || unknown.Index < 0) { return; }

            if (!_unknownValues.Remove(unknown))
            {
                Hardwired.LogDebug($"Warning: Couldn't remove node {unknown.Index}, not in circuit");
                return;
            }

            A = A.RemoveRowColumn(unknown.Index, unknown.Index);
            Z = Z.RemoveRow(unknown.Index);

            _A_LU = null;
            X = null;

            for (int i = unknown.Index; i < _unknownValues.Count; i++)
            {
                _unknownValues[i].Index = i;
            }

            unknown.Index = -1;
        }

        /// <summary>
        /// Gets the solved value of the given unknown, or null if the unknown is invalid or the system has not been solved yet.
        /// </summary>
        /// <param name="unknown"></param>
        /// <returns></returns>
        public Complex? GetValue(Unknown? unknown)
        {
            if (unknown == null || X == null) { return null; }
            if (unknown.Index < 0 || unknown.Index > X.RowCount) { return null; }

            return X[unknown.Index, 0];
        }

        /// <summary>
        /// Gets the solved value of the given unknown, or 0 if the unknown is invalid or the system has not been solved yet.
        /// </summary>
        /// <param name="unknown"></param>
        /// <returns></returns>
        public Complex GetValueOrDefault(Unknown? unknown)
        {
            Complex? value = GetValue(unknown);

            if (value == null || double.IsNaN(value.Value.Real) || double.IsNaN(value.Value.Imaginary))
            {
                return 0;
            }
            
            return value.Value;
        }

        /// <summary>
        /// Represents an unknown value and its corresponding equation in the system of equations.
        /// 
        /// For example, nodes in the circuit are an Unknown value representing the voltage at that node, and the KCL equation for that node.
        /// 
        /// Typical unknown values in MNA include:
        /// - Node voltages
        /// - Branch currents (i.e. for voltage source, transformer, etc)
        /// </summary>
        public class Unknown
        {
            public int Index;
        }
    }
}