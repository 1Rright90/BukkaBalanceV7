using System;
using System.Threading;
using System.Threading.Tasks;

namespace YSBCaptain.Core.Base
{
    /// <summary>
    /// Base class for components that require initialization
    /// </summary>
    public abstract class BaseInitializable : BaseDisposable, IInitializable
    {
        private volatile int _initState;
        protected readonly object _initLock = new object();
        protected readonly SemaphoreSlim _asyncLock;
        
        private const int NotInitialized = 0;
        private const int Initializing = 1;
        private const int Initialized = 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseInitializable"/> class.
        /// </summary>
        protected BaseInitializable()
        {
            _initState = NotInitialized;
            _asyncLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Initializes this instance synchronously.
        /// </summary>
        public void Initialize()
        {
            ThrowIfDisposed("Initialize");

            if (Interlocked.CompareExchange(ref _initState, Initializing, NotInitialized) != NotInitialized)
            {
                if (_initState == Initialized)
                    return;
                throw new InvalidOperationException($"{GetType().Name} is already being initialized");
            }

            try
            {
                OnInitialize();
                _initState = Initialized;
            }
            catch (Exception ex)
            {
                _initState = NotInitialized;
                throw new InvalidOperationException($"Failed to initialize {GetType().Name}", ex);
            }
        }

        /// <summary>
        /// Initializes this instance asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task InitializeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed("InitializeAsync");

            await _asyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_initState == Initialized)
                    return;

                if (Interlocked.CompareExchange(ref _initState, Initializing, NotInitialized) != NotInitialized)
                {
                    throw new InvalidOperationException($"{GetType().Name} is already being initialized");
                }

                await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
                _initState = Initialized;
            }
            catch (Exception ex)
            {
                _initState = NotInitialized;
                throw new InvalidOperationException($"Failed to initialize {GetType().Name}", ex);
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        /// <summary>
        /// When overridden in a derived class, performs synchronous initialization.
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// When overridden in a derived class, performs asynchronous initialization.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            OnInitialize();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Releases the managed resources used by the <see cref="BaseInitializable"/>.
        /// </summary>
        protected override void DisposeManagedResources()
        {
            _asyncLock.Dispose();
            base.DisposeManagedResources();
        }

        /// <summary>
        /// Throws if this instance is not initialized.
        /// </summary>
        /// <param name="memberName">Name of the member being accessed.</param>
        protected void ThrowIfNotInitialized(string memberName = "")
        {
            if (_initState != Initialized)
            {
                throw new InvalidOperationException(string.IsNullOrEmpty(memberName) 
                    ? $"{GetType().Name} is not initialized" 
                    : $"Cannot access {memberName} before initialization");
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is initialized.
        /// </summary>
        public bool IsInitialized => _initState == Initialized;
    }
}
