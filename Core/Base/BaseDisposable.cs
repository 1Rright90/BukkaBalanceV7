using System;
using System.Threading;
using System.Threading.Tasks;

namespace YSBCaptain.Core.Base
{
    /// <summary>
    /// Base class for disposable objects with thread-safe disposal pattern
    /// </summary>
    public abstract class BaseDisposable : IDisposable
    {
        private volatile int _disposalState;
        protected readonly object _disposeLock = new object();
        
        private const int NotDisposed = 0;
        private const int Disposing = 1;
        private const int Disposed = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDisposable"/> class.
        /// </summary>
        protected BaseDisposable()
        {
            _disposalState = NotDisposed;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposalState, Disposing, NotDisposed) != NotDisposed)
            {
                return;
            }

            try
            {
                Dispose(true);
            }
            finally
            {
                _disposalState = Disposed;
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Performs asynchronous cleanup of resources.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected virtual Task DisposeAsyncCore(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeManagedResources();
            }
            
            DisposeUnmanagedResources();
        }

        /// <summary>
        /// Releases managed resources.
        /// </summary>
        protected virtual void DisposeManagedResources() { }

        /// <summary>
        /// Releases unmanaged resources.
        /// </summary>
        protected virtual void DisposeUnmanagedResources() { }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this instance has been disposed.
        /// </summary>
        /// <param name="memberName">Name of the member being accessed.</param>
        protected void ThrowIfDisposed(string memberName = "")
        {
            if (_disposalState == Disposed)
            {
                throw new ObjectDisposedException(GetType().Name, string.IsNullOrEmpty(memberName) 
                    ? null 
                    : $"Cannot access {memberName} on disposed object");
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        protected bool IsDisposed => _disposalState == Disposed;

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="BaseDisposable"/> is reclaimed by garbage collection.
        /// </summary>
        ~BaseDisposable()
        {
            Dispose(false);
        }
    }
}
