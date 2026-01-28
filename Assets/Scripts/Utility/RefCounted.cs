#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace Hardwired.Utility
{
    public static class RefCounted
    {
        [return: NotNullIfNotNull(nameof(value))]
        public static RefCounted<T>? Create<T>(T? value)
            where T : IDisposable
        {
            if (value == null) { return null; }

            return new RefCounted<T>(value);
        }
    }

    public class RefCounted<T> : IDisposable
        where T: IDisposable
    {
        private Inner? _inner;

        public T Value
        {
            get
            {
                if (_inner == null) { throw new ObjectDisposedException(nameof(RefCounted<T>)); }

                return _inner.Value;
            }
        }

        private RefCounted(Inner inner)
        {
            _inner = inner;
            Interlocked.Increment(ref _inner.RefCount);
        }

        public RefCounted(T value) : this(new Inner(value))
        {
        }

        public RefCounted<T> Clone()
        {
            if (_inner == null) { throw new ObjectDisposedException(nameof(RefCounted<T>)); }

            return new RefCounted<T>(_inner);
        }

        public void Dispose()
        {
            if (_inner == null) { throw new ObjectDisposedException(nameof(RefCounted<T>)); }

            if (Interlocked.Decrement(ref _inner.RefCount) == 0)
            {
                _inner.Value.Dispose();
            }

            _inner = null;
        }

        private sealed class Inner
        {
            public T Value;

            public int RefCount;

            public Inner(T value)
            {
                Value = value;
                RefCount = 0;
            }
        }
    }
}