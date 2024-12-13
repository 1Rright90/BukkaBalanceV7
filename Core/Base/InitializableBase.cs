using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Core.Base
{
    /// <summary>
    /// Base class providing initialization, disposal, and component lifecycle management
    /// </summary>
    public abstract class InitializableBase : IInitializable, IDisposable
    {
        protected readonly ILogger _logger;
        protected bool _isInitialized;
        protected bool _isDisposed;
        protected readonly object _lock = new object();
        protected CancellationTokenSource _cancellationTokenSource;

        protected InitializableBase(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _isInitialized = false;
            _isDisposed = false;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public virtual void Initialize()
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    OnInitialize();
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error during initialization: {ex.Message}");
                    throw;
                }
            }
        }

        public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            if (_isInitialized)
                return;

            try
            {
                await OnInitializeAsync(linkedCts.Token).ConfigureAwait(false);
                _isInitialized = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Initialization was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during async initialization: {ex.Message}");
                throw;
            }
        }

        protected virtual void OnInitialize()
        {
            // Base implementation does nothing
        }

        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public bool IsInitialized
        {
            get
            {
                ThrowIfDisposed();
                return _isInitialized;
            }
        }

        protected void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException($"{GetType().Name} is not initialized");
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                try
                {
                    _cancellationTokenSource?.Cancel();
                    _cancellationTokenSource?.Dispose();
                    OnDispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error during disposal: {ex.Message}");
                }
            }

            _isDisposed = true;
        }

        protected virtual void OnDispose()
        {
            // Base implementation does nothing
        }

        ~InitializableBase()
        {
            Dispose(false);
        }
    }
}
