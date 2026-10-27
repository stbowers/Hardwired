#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Hardwired.Utility.Extensions;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.LinearAlgebra.Storage;
using UnityEngine.Assertions;
using Complex = System.Numerics.Complex;

namespace Hardwired.Simulation.Electrical
{
    /// <summary>
    /// A Modified Nodal Analysis solver which can solve most linear and non-linear circuits, both AC and DC, by using a system of equations built up using Kirchoff's laws.
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
        /// <summary>
        /// The error tolerance value for "dx" relative to the current value of x
        /// 
        /// dx < REL_TOLERANCE * x + MIN_TOLERANCE
        /// 
        /// Larger values will be less accurate, but should result in less iterations.
        /// Smaller values will be more accurate, but may take longer to converge.
        /// </summary>
        private const double REL_TOLERANCE = 0.0001;

        /// <summary>
        /// The minimum tolerance value for convergance
        /// 
        /// dx < REL_TOLERANCE * x + MIN_TOLERANCE
        /// </summary>
        private const double MIN_TOLERANCE = 0.0001;

        private List<Unknown> _unknownValues = new();

        /// <summary>
        /// Flag to indicate that the next solve needs to fully re-calculate the initial state, rather than
        /// iterating on the previous solve.
        /// </summary>
        private bool _needsFullSolve = true;

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

        /// <summary>
        /// The jacobian matrix (i.e. matrix of partial derivatives for non-linear components).
        /// 
        /// Stored in "realified" form, as a 2N*2N matrix that stores the real and imaginary parts separately,
        /// since we need to be able to represent partial derivatives with respect to both V and conjugate(V).
        /// 
        /// See `RealifyMatrix()` for more info.
        /// </summary>
        public Matrix<double> Jr = Matrix<double>.Build.Dense(0, 0);

        /// <summary>
        /// The values of any non-linear equations for the current solution `X`
        /// </summary>
        public Matrix<Complex> F = Matrix<Complex>.Build.Dense(0, 0);

        /// <summary>
        /// The "realified" form of `F` (see `RealifyVector()`)
        /// </summary>
        private Matrix<double> _fr = Matrix<double>.Build.Dense(0, 0);

        /// <summary>
        /// Vector of unknown values to be solved for.
        /// </summary>
        public Matrix<Complex> X = Matrix<Complex>.Build.Dense(0, 1);

        /// <summary>
        /// Pre-allocated space for the update vector, dx, to apply to X at each Newton-Raphson iteration.
        /// </summary>
        private Matrix<Complex> _dx = Matrix<Complex>.Build.Dense(0, 1);

        /// <summary>
        /// "realified" form of `_dx`, see `RealifyVector()` for more info.
        /// </summary>
        private Matrix<double> _dxr = Matrix<double>.Build.Dense(0, 1);

        /// <summary>
        /// Vector of known values to be used as inputs.
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
            Assert.IsTrue(a == null || a.Solver == this);
            Assert.IsTrue(b == null || b.Solver == this);

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

            InvalidateTopology();
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
        public void AddVoltageSource(Unknown? a, Unknown? b, [NotNull] ref Unknown? i)
        {
            Assert.IsTrue(a == null || a.Solver == this);
            Assert.IsTrue(b == null || b.Solver == this);
            Assert.IsTrue(i == null || i.Solver == this);

            // Add an unknown for the current
            i ??= AddUnknown(type: "VSourceCurrent");

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

            InvalidateTopology();
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
            Assert.IsTrue(a == null || a.Solver == this);
            Assert.IsTrue(b == null || b.Solver == this);
            Assert.IsTrue(c == null || c.Solver == this);
            Assert.IsTrue(d == null || d.Solver == this);
            Assert.IsTrue(i1 == null || i1.Solver == this);
            Assert.IsTrue(i2 == null || i2.Solver == this);

            // Add 2 new unknowns for the current through each inductor
            i1 ??= AddUnknown(type: "TransformerCurrent");
            i2 ??= AddUnknown(type: "TransformerCurrent");

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

            InvalidateTopology();
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
            Assert.IsTrue(i == null || i.Solver == this);

            if (i == null) { return; }

            // Set input voltage to the given value
            var oldValue = Z[i.Index, 0];
            Z[i.Index, 0] = value;

            // If voltage has changed significantly, invalidate the circuit topology and force a full solve
            if ((oldValue - value).Magnitude > 0.1)
            {
                InvalidateTopology();
            }
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
            Assert.IsTrue(a == null || a.Solver == this);
            Assert.IsTrue(b == null || b.Solver == this);

            if (a != null)
            {
                Z[a.Index, 0] -= value;
            }

            if (b != null)
            {
                Z[b.Index, 0] += value;
            }

            InvalidateTopology();
        }

        /// <summary>
        /// For the current value of `X`, add the evaluation of a non-linear current (`i`) to the `F` vector, and
        /// the evaluation of the partial derivative of I(v) with respect to v (`dIdV`) and conjugate(v) (`dIdVConjugate`)
        /// to the "realified" jacobian, `Jr`.
        /// 
        /// Note that positive branch current flows "from" node a "to" node b.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="i">
        /// The evaluation of the non-lienar equation I(v) - I_0 for the current value of v (from the `X` vector).
        /// </param>
        /// <param name="dIdV">
        /// The evaluation of the partial derivative of I(v) with respect to v for the current value of v.
        /// </param>
        /// <param name="dIdVConjugate">
        /// The evaluation of the partial derivative of I(v) with respect to conjugate(v) for the current value of v.
        /// </param>
        public void AddNonlinearCurrent(Unknown? a, Unknown? b, Complex i, Complex dIdV, Complex dIdVConjugate)
        {
            Assert.IsTrue(a == null || a.Solver == this);
            Assert.IsTrue(b == null || b.Solver == this);

            if (a != null)
            {
                F[a.Index, 0] += i;

                // J[a, a] += C
                Jr[a.Index, a.Index] += (dIdV + dIdVConjugate).Real;
                Jr[a.Index + A.RowCount, a.Index] += (dIdV + dIdVConjugate).Imaginary;
                Jr[a.Index, a.Index + A.RowCount] += -(dIdV - dIdVConjugate).Imaginary;
                Jr[a.Index + A.RowCount, a.Index + A.RowCount] += (dIdV - dIdVConjugate).Real;
            }

            if (b != null)
            {
                F[b.Index, 0] -= i;

                // J[b, b] += C
                Jr[b.Index, b.Index] += (dIdV + dIdVConjugate).Real;
                Jr[b.Index + A.RowCount, b.Index] += (dIdV + dIdVConjugate).Imaginary;
                Jr[b.Index, b.Index + A.RowCount] += -(dIdV - dIdVConjugate).Imaginary;
                Jr[b.Index + A.RowCount, b.Index + A.RowCount] += (dIdV - dIdVConjugate).Real;
            }

            if (a != null && b != null)
            {
                // J[a, b] -= C
                Jr[a.Index, b.Index] -= (dIdV + dIdVConjugate).Real;
                Jr[a.Index + A.RowCount, b.Index] -= (dIdV + dIdVConjugate).Imaginary;
                Jr[a.Index, b.Index + A.RowCount] -= -(dIdV - dIdVConjugate).Imaginary;
                Jr[a.Index + A.RowCount, b.Index + A.RowCount] -= (dIdV - dIdVConjugate).Real;

                // J[b, a] -= C
                Jr[b.Index, a.Index] -= (dIdV + dIdVConjugate).Real;
                Jr[b.Index + A.RowCount, a.Index] -= (dIdV + dIdVConjugate).Imaginary;
                Jr[b.Index, a.Index + A.RowCount] -= -(dIdV - dIdVConjugate).Imaginary;
                Jr[b.Index + A.RowCount, a.Index + A.RowCount] -= (dIdV - dIdVConjugate).Real;
            }
        }

        /// <summary>
        /// Solves the system for the voltage at each node and current through each voltage source.
        /// 
        /// Provides an initial solution for the simplified equation Ax = z
        /// "x" is then used as the starting "guess" for the Newton-Raphson method for solving non-linear components (via IterateNR())
        /// 
        /// Returns `true` if a solution was found, or `false` if the circuit cannot be solved
        /// </summary>
        private bool SolveInitial()
        {
            // Inputs -- Coerce small or NaN values to 0
            A.CoerceZero(v => v.IsNaN() || v.Magnitude < 10 * double.Epsilon);
            Z.CoerceZero(v => v.IsNaN() || v.Magnitude < 10 * double.Epsilon);

            // (re)initialize X if needed
            if (X.RowCount != A.RowCount)
            {
                X = Matrix<Complex>.Build.Dense(A.RowCount, 1);
            }

            // Solve for x
            A.Solve(Z, X);

            // Output -- Coerce small or NaN values to 0
            X.CoerceZero(v => v.IsNaN() || v.Magnitude < 10 * double.Epsilon);

            return true;
        }

        /// <summary>
        /// Begins a Newton-Raphson iteration by initializing/resetting the J and F matricies with the values corresponding to the linear components
        /// of the circuit. After calling this method, each non-linear component should evaluate its function(s) for the current values of X, and add
        /// them to F and J.
        /// </summary>
        public void BeginNRIteration()
        {
            if (_needsFullSolve)
            {
                Console.WriteLine($"topology changed!");
                SolveInitial();
                _needsFullSolve = false;
            }
            
            // Initialize the jacobian with the partial derivatives of all the linear components (which is just the A matrix, since d/dx Ax = A)
            // Jr = [ R(A) -I(A) ]
            //      [ I(A)  R(A) ]
            RealifyMatrix(A, Jr);

            // Calculate F_0 for linear components
            // Ax + Yx = Z (where "Y" is a matrix of non-linear functions of x)
            // :. F(x) = Ax + Yx - Z = 0
            // The values of Yx (i.e. values of non-linear functions at x) will be added by each non-linear component when updating
            A.Multiply(X, F);
            F.Subtract(Z, F);
        }

        /// <summary>
        /// Updates the `x` vector with a "better guess" for any non-linear terms using the Newton-Raphson method.
        /// 
        /// Solves the equation `J*dx = -f` for a "correction vector" to apply to `x` such that the new value of `x` will be closer to the
        /// correct solution for any non-linear components.
        /// 
        /// Returns `true` if convergance has been reached (i.e. dx was small enough), or `false` if convergance has not yet been reached.
        /// </summary>
        public bool SolveNRIteration(int n)
        {
            // Check if F is near 0 - no iterations needed
            if (F.ForAll(f => f.Magnitude < MIN_TOLERANCE))
            {
                return true;
            }
            
            // _fr = [R(F) I(F)]
            RealifyVector(F, _fr);

            // Solve for update vector
            // _dxr = Jr.Solve(-_fr);
            _fr.Negate(_fr);
            Jr.Solve(_fr, _dxr);
            ComplexifyVector(_dxr, _dx);

            // Check for convergence
            bool hasConverged = true;
            for (int i = 0; i < X.RowCount; i++)
            {
                hasConverged &= _dx[i, 0].Magnitude < REL_TOLERANCE * X[i, 0].Magnitude + MIN_TOLERANCE;
            }

            // Update X
            for (int i = 0; i < X.RowCount; i++)
            {
                // Use a damping factor for the first 3 iterations, to avoid large changes that won't converge if the initial guess was too far off
                // Note - there are other techniques to avoid convergance issues, but this is the easiest and should work well for most of our use cases.
                var k = hasConverged ? 1.0 : n < 3 ? 0.3 : 0.9;
                X[i, 0] += k * _dx[i, 0];
            }

            return hasConverged;
        }

        public Unknown AddUnknown(string type = "NodeVoltage")
            => AddUnknowns(1, type)[0];

        public Unknown[] AddUnknowns(int count, string type = "NodeVoltage")
        {
            Unknown[] newUnknowns = new Unknown[count];

            for (int i = 0; i < count; i++)
            {
                newUnknowns[i] = new(this) { Index = _unknownValues.Count, Type = type };
                _unknownValues.Add(newUnknowns[i]);
            }

            // Resize matricies
            var newSize = _unknownValues.Count;
            A = A.Resize(newSize, newSize);
            Jr = Jr.Resize(2*newSize, 2*newSize);
            _fr = _fr.Resize(2*newSize, 1);
            X = X.Resize(newSize, 1);
            _dx = _dx.Resize(newSize, 1);
            _dxr = _dxr.Resize(2*newSize, 1);
            F = F.Resize(newSize, 1);
            Z = Z.Resize(newSize, 1);

            InvalidateTopology();

            return newUnknowns;
        }

        private void RemoveUnknown(Unknown? unknown)
        {
            if (unknown == null || unknown.Index < 0) { return; }

            if (!_unknownValues.Remove(unknown))
            {
                Hardwired.LogDebug($"Warning: Couldn't remove node {unknown.DebugName}, not in circuit");
                return;
            }

            var n = _unknownValues.Count;

            // Remove row/column, but keep rest of matrix intact
            A = A.RemoveRowColumn(unknown.Index, unknown.Index);
            X = X.RemoveRow(unknown.Index);
            Z = Z.RemoveRow(unknown.Index);

            // Just resize (will be fully re-calculated at each step, no need to keep values)
            Jr = Jr.Resize(2*n, 2*n);
            F = F.Resize(n, 1);
            _fr = _fr.Resize(2*n, 1);
            _dx = _dx.Resize(n, 1);
            _dxr = _dxr.Resize(2*n, 1);

            // re-index remaining unknowns
            for (int i = unknown.Index; i < _unknownValues.Count; i++)
            {
                _unknownValues[i].Index = i;
            }

            unknown.Index = -1;

            InvalidateTopology();
        }

        /// <summary>
        /// Called whenever the circuit topology has changed in such a way that the next solve needs to be a full solve rather than
        /// iterating on the previous solution.
        /// 
        /// Should be called whenever 'A' or 'Z' are updated.
        /// </summary>
        private void InvalidateTopology()
        {
            _needsFullSolve = true;
        }

        /// <summary>
        /// Gets the solved value of the given unknown, or null if the unknown is invalid or the system has not been solved yet.
        /// </summary>
        /// <param name="unknown"></param>
        /// <returns></returns>
        public Complex? GetValue(Unknown? unknown)
        {
            Assert.IsTrue(unknown == null || unknown.Solver == this);

            if (unknown == null) { return null; }
            if (unknown.Index < 0 || unknown.Index >= X.RowCount) { return null; }

            var val = X[unknown.Index, 0];

            if (val.IsNaN()) { return null; }

            return val;
        }

        /// <summary>
        /// Gets the solved value of the given unknown, or 0 if the unknown is invalid or the system has not been solved yet.
        /// </summary>
        /// <param name="unknown"></param>
        /// <returns></returns>
        public Complex GetValueOrDefault(Unknown? unknown)
        {
            Complex? value = GetValue(unknown);

            if (value == null)
            {
                return 0;
            }
            
            return value.Value;
        }

        /// <summary>
        /// "realificates" the complex-valued N*M matrix, z, into a real-valued 2N*2M matrix, r, by copying the real
        /// and imaginary parts into blocks in the form:
        /// 
        /// [ R(z) -I(z) ]
        /// [ I(z)  R(z) ]
        /// 
        /// This "realified" matrix can then be used with similarly "realified" vectors to calculate complex systems
        /// of equations, as normal:
        /// 
        /// c = A*b
        /// 
        /// [ R(c) ] = [ R(A) -I(A) ] * [ R(b) ]
        /// [ I(c) ]   [ I(A)  R(A) ]   [ I(b) ]
        /// 
        /// However, the advantage to using a "realified" matrix over just an ordinary complex matrix is that the
        /// real and imaginary parts can be treated as independent variables.
        /// This is especially important for solving non-holomorphic systems of complex non-linear equations, where
        /// in order to correclty evaluate the jacobian/gradient we must take partial derivatives with respect to
        /// both the complex variable and its conjugate.
        /// </summary>
        /// <param name="z">
        /// The complex-valued matrix to realify.
        /// </param>
        /// <param name="r">
        /// The real-valued matrix to store the result in.
        /// </param>
        private static void RealifyMatrix(Matrix<Complex> z, Matrix<double> r)
        {
            Assert.AreEqual(2*z.RowCount, r.RowCount);
            Assert.AreEqual(2*z.ColumnCount, r.ColumnCount);

            for (int j = 0; j < z.ColumnCount; j++)
            {
                for (int i = 0; i < z.RowCount; i++)
                {
                    r[i, j] = z[i, j].Real;
                    r[i + z.RowCount, j] = z[i, j].Imaginary;
                    r[i, j + z.ColumnCount] = -z[i, j].Imaginary;
                    r[i + z.RowCount, j + z.ColumnCount] = z[i, j].Real;
                }
            }
        }

        /// <summary>
        /// "realifies" the complex-valued N*1 matrix (vector), z, into a real-valued 2N*1 vector, r, by copying the real
        /// and imaginary parts into blocks in the form:
        /// 
        /// [ R(z) ]
        /// [ I(z) ]
        /// 
        /// </summary>
        /// <param name="z">
        /// The complex-valued matrix to realify.
        /// </param>
        /// <param name="r">
        /// The real-valued matrix to store the result in.
        /// </param>
        private static void RealifyVector(Matrix<Complex> z, Matrix<double> r)
        {
            Assert.AreEqual(2*z.RowCount, r.RowCount);
            Assert.AreEqual(1, z.ColumnCount);
            Assert.AreEqual(1, r.ColumnCount);

            for (int i = 0; i < z.RowCount; i++)
            {
                r[i, 0] = z[i, 0].Real;
                r[i + z.RowCount, 0] = z[i, 0].Imaginary;
            }
        }

        /// <summary>
        /// "complexifies" the real-valued 2Nx1 vector, r, into a complex-valued Nx1 matrix, z, by taking the real
        /// and imaginary part from each block in the form:
        /// 
        /// r = [ R(z) ]
        ///     [ I(z) ]
        /// 
        /// Inverse of `RealifyVector()`.
        /// </summary>
        /// <param name="r">
        /// The real-valued matrix to complexify.
        /// </param>
        /// <param name="z">
        /// The complex-valued matrix to store the result in.
        /// </param>
        private static void ComplexifyVector(Matrix<double> r, Matrix<Complex> z)
        {
            Assert.AreEqual(2*z.RowCount, r.RowCount);
            Assert.AreEqual(1, z.ColumnCount);
            Assert.AreEqual(1, r.ColumnCount);

            for (int i = 0; i < z.RowCount; i++)
            {
                var x = r[i, 0];
                var y = r[i + z.RowCount, 0];
                z[i, 0] = new Complex(x, y);
            }
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
        public class Unknown : IDisposable
        {
            private WeakReference<MNASolver>? _solverRef;

            public MNASolver? Solver {
                get
                {
                    if (_solverRef?.TryGetTarget(out var value) == true)
                    {
                        return value;
                    }

                    return null;
                }
            }

            public int Index;

            public string Type = string.Empty;

            public string DebugName => $"{Type}[{Index}]";

            public Unknown(MNASolver solver)
            {
                _solverRef = new(solver);
            }

            public void Dispose()
            {
                var solverRef = Interlocked.Exchange(ref _solverRef!, null);
                if (solverRef?.TryGetTarget(out var solver) == true)
                {
                    solver.RemoveUnknown(this);
                }
            }
        }
    }
}