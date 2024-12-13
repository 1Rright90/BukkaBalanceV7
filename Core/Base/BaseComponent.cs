using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Performance;

namespace YSBCaptain.Core.Base
{
    /// <summary>
    /// Base class for components with lifecycle management and logging
    /// </summary>
    public abstract class BaseComponent : IDisposable
    {
        /// <summary>
        /// Gets the name of this component.
        /// </summary>
        public string ComponentName { get; }

        /// <summary>
        /// Gets the logger for this component.
        /// </summary>
        protected readonly ILogger<BaseComponent> _logger;

        /// <summary>
        /// Gets the performance monitor for this component.
        /// </summary>
        protected readonly IPerformanceMonitor _performanceMonitor;

        protected CancellationTokenSource _cancellationTokenSource;
        protected bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseComponent"/> class.
        /// </summary>
        /// <param name="componentName">Name of the component.</param>
        /// <param name="logger">Logger for the component.</param>
        /// <param name="performanceMonitor">The performance monitor.</param>
        protected BaseComponent(
            string componentName,
            ILogger<BaseComponent> logger,
            IPerformanceMonitor performanceMonitor = null)
        {
            ComponentName = componentName ?? throw new ArgumentNullException(nameof(componentName));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor ?? new NullPerformanceMonitor();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Starts this component asynchronously.
        /// </summary>
        public virtual async Task StartAsync()
        {
            if (_isDisposed)
            {
                _logger.LogWarning($"Component {ComponentName} is already disposed");
                return;
            }

            try
            {
                await InitializeAsync().ConfigureAwait(false);
                _logger.LogInformation($"Component {ComponentName} started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start component {ComponentName}");
                throw;
            }
        }

        /// <summary>
        /// Stops this component asynchronously.
        /// </summary>
        public virtual async Task StopAsync()
        {
            if (_isDisposed)
            {
                _logger.LogWarning($"Component {ComponentName} is already disposed");
                return;
            }

            try
            {
                await DisposeAsyncCore(CancellationToken.None).ConfigureAwait(false);
                _logger.LogInformation($"Component {ComponentName} stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping component {ComponentName}");
                throw;
            }
        }

        /// <summary>
        /// Releases the managed resources used by the <see cref="BaseComponent"/>.
        /// </summary>
        public virtual void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            GC.SuppressFinalize(this);
            OnDispose();
        }

        protected virtual void OnDispose() { }

        protected virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task DisposeAsyncCore(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private class NullPerformanceMonitor : IPerformanceMonitor
        {
            public void Dispose() { }

            public Task StartAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new PerformanceMetrics());
            }

            public Task LogEventAsync(string eventName, string details = null)
            {
                return Task.CompletedTask;
            }
        }
    }
}
