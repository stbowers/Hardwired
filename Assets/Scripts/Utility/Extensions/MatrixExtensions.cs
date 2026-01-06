#nullable enable

using System;
using System.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace Hardwired.Utility.Extensions
{
    public static class MatrixExtensions
    {
        /// <summary>
        /// Creates a new matrix with both the given row and column removed.
        /// 
        /// This is equivalent to
        /// 
        /// ```
        /// m = m.RemoveRow(rowIndex);
        /// m = m.RemoveColumn(colIndex);
        /// ```
        /// 
        /// But avoids the extra allocation since the entire operation is done at once.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="matrix"></param>
        /// <param name="rowIndex"></param>
        /// <param name="colIndex"></param>
        /// <returns></returns>
        public static Matrix<T> RemoveRowColumn<T>(this Matrix<T> matrix, int rowIndex, int colIndex)
            where T : struct, IEquatable<T>, IFormattable
        {
            Matrix<T> newMatrix = Matrix<T>.Build.SameAs(matrix, matrix.RowCount - 1, matrix.ColumnCount - 1);

            // A | B
            // --+--
            // C | D

            // Copy A
            matrix.Storage.CopySubMatrixTo(newMatrix.Storage, 0, 0, rowIndex, 0, 0, colIndex, ExistingData.AssumeZeros);
            // Copy B
            matrix.Storage.CopySubMatrixTo(newMatrix.Storage, 0, 0, rowIndex, colIndex + 1, colIndex, matrix.ColumnCount - colIndex - 1, ExistingData.AssumeZeros);
            // Copy C
            matrix.Storage.CopySubMatrixTo(newMatrix.Storage, rowIndex + 1, rowIndex, matrix.RowCount - rowIndex - 1, 0, 0, colIndex, ExistingData.AssumeZeros);
            // Copy D
            matrix.Storage.CopySubMatrixTo(newMatrix.Storage, rowIndex + 1, rowIndex, matrix.RowCount - rowIndex - 1, colIndex + 1, colIndex, matrix.ColumnCount - colIndex - 1, ExistingData.AssumeZeros);

            return newMatrix;
        }
    }
}