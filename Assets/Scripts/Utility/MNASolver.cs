#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using MathNet.Numerics.LinearAlgebra;

using Complex = System.Numerics.Complex;

namespace Hardwired.Utility
{
    // Note that there are also other approaches to solving circuits - in particular the direct alternative to "node analysis" is "mesh analysis", which uses
    // Kirchoff's voltage law (KVL) rather than Kirchoff's current law (KCL) for the system of equations. According to Wikipedia, mesh analysis is particularly
    // suitable for "planar circuits" (i.e. circuits where no wires cross each other). In our case, all our circuits should be planar (in fact, they follow a
    // fairly regular structure, since all devices must be wired in parallel), so it may be possible to create a more optimal solution that doesn't have to do
    // so many calculations... On the other hand, this solver is fairly versatile so could still be used in case we decide to add more complex circuit behaviors
    // down the line.
    //
    // References:
    // - https://en.wikipedia.org/wiki/Nodal_analysis
    // - https://en.wikipedia.org/wiki/Mesh_analysis
    // - https://en.wikipedia.org/wiki/System_of_linear_equations
    // - https://github.com/age-series/ElectricalAge/blob/main/src/main/java/mods/eln/sim/mna/SubSystem.java
    // - https://ecircuitcenter.com/SpiceTopics/Nodal%20Analysis/Nodal%20Analysis.htm#top
    // - https://cheever.domains.swarthmore.edu/Ref/mna/MNA2.html
    public class MNASolver
    {
        private int _nodes;
        private int _voltageSources;

        /// <summary>
        /// Matrix of admittance values between each node.
        /// 
        /// Admittance represents the ability for electricity to flow (i.e. reciprocol of resistance for DC circuits).
        /// 
        /// The matrix equation
        /// 
        /// A * v = i
        /// 
        /// Represents a system of equations following Kirchoff's Current Law (KCL), which we end up solving for `v` (the voltage at each node).
        /// </summary>
        private Matrix<Complex>? _A;

        // A * x = z
        private Vector<Complex>? _x;
        private Vector<Complex>? _z;

        public void Initialize(int nodes, int voltageSources)
        {
            _nodes = nodes;
            _voltageSources = voltageSources;

            int totalSize = _nodes + _voltageSources;

            _A = Matrix<Complex>.Build.Dense(totalSize, totalSize);
            _z = Vector<Complex>.Build.Dense(totalSize);
        }

        /// <summary>
        /// Adds the given admittance value between the given nodes.
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
        public void AddAdmittance(int? n, int m, Complex value)
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            _A[m, m] += value;

            if (n != null)
            {
                _A[n.Value, n.Value] += value;
                _A[n.Value, m] -= value;
                _A[m, n.Value] -= value;
            }
        }

        public void AddResistance(int? n, int m, double value)
            => AddAdmittance(n, m, 1.0 / value);

        public void SetVoltage(int n, int v, Complex value)
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            int m = _nodes + v;

            _A[m, n] = 1;
            _A[n, m] = 1;
            _z[m] = value;
        }

        /// <summary>
        /// Adds a current source between the given nodes with the given complex current value.
        /// </summary>
        /// <param name="n"></param>
        /// <param name="m"></param>
        /// <param name="i"></param>
        public void SetCurrent(int? n, int? m, Complex value)
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            if (n != null)
            {
                _z[n.Value] = value;
            }

            if (m != null)
            {
                _z[m.Value] = -value;
            }
        }

        /// <summary>
        /// Solves the system for the voltage at each node.
        /// </summary>
        public void Solve()
        {
            if (_A is null || _z is null) { ThrowNotInitializedException(); }

            _x = _A.Solve(_z);
        }

        /// <summary>
        /// Gets the voltage (relative to common ground) at the given node.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public Complex GetVoltage(int n)
        {
            if (_x is not null && _x.Count > n)
            {
                return _x[n];
            }
            else
            {
                return Complex.Zero;
            }
        }

        /// <summary>
        /// Gets the current for the given voltage source from the result vector
        /// </summary>
        /// <param name="vIndex"></param>
        /// <returns></returns>
        public Complex GetCurrent(int v)
        {
            int m = _nodes + v;

            if (_x is not null && _x.Count > m)
            {
                return _x[m];
            }
            else
            {
                return Complex.Zero;
            }
        }

        [DoesNotReturn]
        private void ThrowNotInitializedException()
        {
            throw new InvalidOperationException("MNASolver is not initialized");
        }
    }
}