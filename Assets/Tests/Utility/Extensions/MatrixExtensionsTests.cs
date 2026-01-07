#nullable enable

using System;
using Hardwired.Utility.Extensions;
using MathNet.Numerics.LinearAlgebra;
using NUnit.Framework;

namespace Hardwired.Tests.Utility.Extensions
{
    public class MatrixExtensionsTests
    {
        [DatapointSource]
        private static int[] intValues = new int[] { 0, 1, 2, 3, 4, 5 };

        [Theory]
        public void RemoveRowColumnWorks(int n, int m, int i, int j)
        {
            // Can only remove row/column within bounds of matrix
            Assume.That(i < n);
            Assume.That(j < m);

            // Create a new matrix where the row/column we want to delete is 1s, and everything else is 0s
            Matrix<double> m1 = Matrix<double>.Build.Dense(n, m, (x, y) => (x == i || y == j) ? 1f : 0f);

            // Check
            Assert.That(m1.Row(i).ForAll(v => v == 1f));
            Assert.That(m1.Column(j).ForAll(v => v == 1f));

            // Test RemoveRowColumn()
            Matrix<double> m2 = m1.RemoveRowColumn(i, j);

            // New matrix should have one less row/column
            Assert.That(m2.RowCount == n - 1);
            Assert.That(m2.ColumnCount == m - 1);

            // All values in new matrix should be 0
            Assert.That(m2.ForAll(v => v == 0f, Zeros.Include));
        }
    }
}

