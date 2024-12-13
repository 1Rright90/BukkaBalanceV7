using System;
using System.Collections.Concurrent;
using System.Threading;
using TaleWorlds.Library;

namespace YSBCaptain.Performance
{
    /// <summary>
    /// Manages a pool of reusable buffers to reduce memory allocation and garbage collection overhead.
    /// Follows TaleWorlds' Native code patterns for memory management.
    /// </summary>
    /// <remarks>
    /// This class is designed to work with Mount &amp; Blade II: Bannerlord's memory management system
    /// and follows the same patterns used in TaleWorlds.Library for buffer management.
    /// </remarks>
    internal sealed class BufferManager : IDisposable
    {
        private readonly ConcurrentBag<byte[]> _buffers;
        private readonly int _bufferSize;
        private readonly int _maxBuffers;
        private int _currentBuffers;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the BufferManager class.
        /// </summary>
        /// <param name="bufferSize">The size of each buffer in bytes.</param>
        /// <param name="maxBuffers">The maximum number of buffers to maintain in the pool.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when bufferSize or maxBuffers is less than or equal to 0.</exception>
        /// <exception cref="OverflowException">Thrown when bufferSize would result in an array too large to allocate.</exception>
        public BufferManager(int bufferSize, int maxBuffers = 100)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (maxBuffers <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBuffers));

            try
            {
                checked
                {
                    if ((long)bufferSize * sizeof(byte) > int.MaxValue)
                        throw new OverflowException("Buffer size too large");
                }
            }
            catch (OverflowException ex)
            {
                Debug.Print($"Buffer size overflow: {ex.Message}");
                throw;
            }

            _bufferSize = bufferSize;
            _maxBuffers = maxBuffers;
            _buffers = new ConcurrentBag<byte[]>();
        }

        /// <summary>
        /// Rents a buffer from the pool or creates a new one if necessary.
        /// </summary>
        /// <returns>A buffer of the specified size.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the maximum number of buffers has been reached.</exception>
        public byte[] Rent()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BufferManager));

            if (_buffers.TryTake(out byte[] buffer))
            {
                return buffer;
            }

            if (Interlocked.Increment(ref _currentBuffers) <= _maxBuffers)
            {
                try
                {
                    return new byte[_bufferSize];
                }
                catch (OutOfMemoryException ex)
                {
                    Interlocked.Decrement(ref _currentBuffers);
                    Debug.Print($"Failed to allocate buffer: {ex.Message}");
                    throw;
                }
            }

            Interlocked.Decrement(ref _currentBuffers);
            throw new InvalidOperationException("Maximum number of buffers reached");
        }

        /// <summary>
        /// Returns a buffer to the pool.
        /// </summary>
        /// <param name="buffer">The buffer to return.</param>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        /// <exception cref="ArgumentException">Thrown when buffer size does not match the pool's buffer size.</exception>
        public void Return(byte[] buffer)
        {
            if (_disposed) return;

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (buffer.Length != _bufferSize)
                throw new ArgumentException("Buffer size mismatch", nameof(buffer));

            Array.Clear(buffer, 0, buffer.Length);
            _buffers.Add(buffer);
        }

        /// <summary>
        /// Disposes the buffer manager and clears all buffers.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            while (_buffers.TryTake(out _))
            {
            }
        }
    }
}
