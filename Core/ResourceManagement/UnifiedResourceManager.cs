using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.ResourceManagement
{
    /// <summary>
    /// Provides unified management of resources with thread-safe operations and performance monitoring.
    /// Implements the singleton pattern for global resource management across the application.
    /// </summary>
    /// <remarks>
    /// This manager follows TaleWorlds' resource management patterns and provides centralized
    /// control over resource lifecycle, monitoring, and metrics collection.
    /// </remarks>
    public sealed class UnifiedResourceManager : InitializableBase, IResourceManager, IAsyncDisposable
    {
        private static readonly Lazy<UnifiedResourceManager> _instance = new(() => new UnifiedResourceManager());
        
        /// <summary>
        /// Gets the singleton instance of the UnifiedResourceManager.
        /// </summary>
        public static UnifiedResourceManager Instance => _instance.Value;

        private readonly ResourcePool _resourcePool;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly MetricsCollector _metricsCollector;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Microsoft.Extensions.Logging.ILogger<UnifiedResourceManager> _logger;
        private readonly SemaphoreSlim _initLock;
        private bool _isDisposed;

        /// <summary>
        /// Occurs when a resource is successfully acquired.
        /// </summary>
        public event EventHandler<string> ResourceAcquired;
        
        /// <summary>
        /// Occurs when a resource is successfully released.
        /// </summary>
        public event EventHandler<string> ResourceReleased;

        private UnifiedResourceManager()
        {
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<UnifiedResourceManager>();
            _cancellationTokenSource = new CancellationTokenSource();
            _initLock = new SemaphoreSlim(1, 1);

            _resourcePool = new ResourcePool(_logger, OnResourceAcquired, OnResourceReleased);
            _resourceMonitor = new ResourceMonitor(_logger);
            _metricsCollector = new MetricsCollector(_logger);
        }

        /// <summary>
        /// Initializes core functionality.
        /// </summary>
        protected override void OnInitialize()
        {
            try
            {
                base.OnInitialize();
                _resourceMonitor.StartMonitoring(_cancellationTokenSource.Token, _resourcePool);
                _logger.LogInformation("UnifiedResourceManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing resource manager");
                throw;
            }
        }

        /// <summary>
        /// Initializes core functionality asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.OnInitializeAsync(cancellationToken);
                await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                OnInitialize();
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>
        /// Acquires a resource asynchronously using the provided factory.
        /// </summary>
        /// <typeparam name="T">The type of resource to acquire.</typeparam>
        /// <param name="resourceId">The unique identifier for the resource.</param>
        /// <param name="resourceFactory">The factory function to create the resource.</param>
        /// <returns>The acquired resource.</returns>
        /// <exception cref="ArgumentNullException">Thrown when resourceId or factory is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when resource acquisition fails.</exception>
        public async Task<T> AcquireResourceAsync<T>(string resourceId, Func<Task<T>> resourceFactory) where T : class
        {
            if (string.IsNullOrEmpty(resourceId)) throw new ArgumentNullException(nameof(resourceId));
            if (resourceFactory == null) throw new ArgumentNullException(nameof(resourceFactory));

            try
            {
                return await _resourcePool.AcquireAsync(resourceId, resourceFactory).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire resource: {ResourceId}", resourceId);
                throw new InvalidOperationException($"Failed to acquire resource: {resourceId}", ex);
            }
        }

        public async Task<T> AcquireResourceAsync<T>(string resourceId) where T : class
        {
            try
            {
                return await LoadResourceAsync<T>(resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to acquire resource {resourceId}");
                throw;
            }
        }

        public async Task<bool> IsResourceAvailableAsync(string resourceId)
        {
            try
            {
                return await Task.FromResult(_resourceMonitor.GetResourceStatuses().ContainsKey(resourceId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check resource availability for {resourceId}");
                return false;
            }
        }

        /// <summary>
        /// Releases a resource.
        /// </summary>
        /// <param name="resourceId">The unique identifier for the resource.</param>
        public void ReleaseResource(string resourceId)
        {
            _resourcePool.Release(resourceId);
        }

        /// <summary>
        /// Gets the current statuses of all resources.
        /// </summary>
        /// <returns>A dictionary of resource statuses.</returns>
        public Dictionary<string, IResourceManager.ResourceStatus> GetResourceStatuses()
        {
            var statuses = new Dictionary<string, IResourceManager.ResourceStatus>();
            
            foreach (var resource in _resourcePool.GetAllResources())
            {
                statuses[resource.Key] = new IResourceManager.ResourceStatus
                {
                    IsLoaded = resource.Value.IsLoaded,
                    LastAccessTime = resource.Value.LastAccessTime,
                    LoadCount = resource.Value.LoadCount,
                    MemoryUsage = resource.Value.MemoryUsage
                };
            }

            return statuses;
        }

        /// <summary>
        /// Gets a metric value.
        /// </summary>
        /// <param name="key">The key of the metric.</param>
        /// <returns>The metric value.</returns>
        public double GetMetric(string key)
        {
            return _metricsCollector.GetMetric(key);
        }

        /// <summary>
        /// Asynchronously disposes of the resource manager.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            _cancellationTokenSource.Cancel();
            await _resourceMonitor.DisposeAsync().ConfigureAwait(false);
            await _resourcePool.DisposeAsync().ConfigureAwait(false);
            _metricsCollector.Dispose();

            _cancellationTokenSource.Dispose();
            _isDisposed = true;
        }

        private void OnResourceAcquired(string resourceId)
        {
            ResourceAcquired?.Invoke(this, resourceId);
        }

        private void OnResourceReleased(string resourceId)
        {
            ResourceReleased?.Invoke(this, resourceId);
        }

        private async Task<T> LoadResourceAsync<T>(string resourceId) where T : class
        {
            // Implement the logic to load the resource
            throw new NotImplementedException();
        }
    }

    internal class ResourcePool : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, WeakReference<object>> _resources = new();
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly Microsoft.Extensions.Logging.ILogger<UnifiedResourceManager> _logger;
        private readonly Action<string> _onAcquired;
        private readonly Action<string> _onReleased;

        public ResourcePool(Microsoft.Extensions.Logging.ILogger<UnifiedResourceManager> logger, Action<string> onAcquired, Action<string> onReleased)
        {
            _logger = logger;
            _onAcquired = onAcquired;
            _onReleased = onReleased;
        }

        /// <summary>
        /// Acquires a resource asynchronously using the provided factory.
        /// </summary>
        /// <typeparam name="T">The type of resource to acquire.</typeparam>
        /// <param name="resourceId">The unique identifier for the resource.</param>
        /// <param name="resourceFactory">The factory function to create the resource.</param>
        /// <returns>The acquired resource.</returns>
        public async Task<T> AcquireAsync<T>(string resourceId, Func<Task<T>> resourceFactory) where T : class
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_resources.TryGetValue(resourceId, out var weakRef) && weakRef.TryGetTarget(out var existingResource))
                {
                    return existingResource as T;
                }

                var newResource = await resourceFactory().ConfigureAwait(false);
                _resources[resourceId] = new WeakReference<object>(newResource);
                _onAcquired?.Invoke(resourceId);
                return newResource;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<T> AcquireAsync<T>(string resourceId) where T : class
        {
            try
            {
                return await LoadResourceAsync<T>(resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to acquire resource {resourceId}");
                throw;
            }
        }

        public async Task<bool> IsResourceAvailableAsync(string resourceId)
        {
            try
            {
                return await Task.FromResult(_resources.ContainsKey(resourceId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to check resource availability for {resourceId}");
                return false;
            }
        }

        /// <summary>
        /// Releases a resource.
        /// </summary>
        /// <param name="resourceId">The unique identifier for the resource.</param>
        public void Release(string resourceId)
        {
            if (_resources.TryRemove(resourceId, out _))
            {
                _onReleased?.Invoke(resourceId);
                _logger.LogDebug($"Resource {resourceId} released.");
            }
        }

        public Dictionary<string, object> GetAllResources()
        {
            var resources = new Dictionary<string, object>();
            foreach (var resource in _resources)
            {
                if (resource.Value.TryGetTarget(out var target))
                {
                    resources[resource.Key] = target;
                }
            }
            return resources;
        }

        /// <summary>
        /// Asynchronously disposes of the resource pool.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                _resources.Clear();
            }
            finally
            {
                _lock.Release();
                _lock.Dispose();
            }
        }
    }

    internal class ResourceMonitor : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, ResourceStatus> _statuses = new();
        private readonly Microsoft.Extensions.Logging.ILogger<UnifiedResourceManager> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public ResourceMonitor(Microsoft.Extensions.Logging.ILogger<UnifiedResourceManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Starts monitoring resources.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the monitoring.</param>
        /// <param name="resourcePool">The resource pool to monitor.</param>
        public void StartMonitoring(CancellationToken cancellationToken, ResourcePool resourcePool)
        {
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, cancellationToken).ConfigureAwait(false);

                    await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        // Periodically update statuses
                    }
                    finally
                    {
                        _lock.Release();
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Gets the current statuses of all resources.
        /// </summary>
        /// <returns>A dictionary of resource statuses.</returns>
        public ConcurrentDictionary<string, ResourceStatus> GetResourceStatuses()
        {
            return new ConcurrentDictionary<string, ResourceStatus>(_statuses);
        }

        /// <summary>
        /// Asynchronously disposes of the resource monitor.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                _statuses.Clear();
            }
            finally
            {
                _lock.Release();
                _lock.Dispose();
            }
        }
    }

    internal class MetricsCollector : IDisposable
    {
        private readonly ConcurrentDictionary<string, double> _metrics = new();
        private readonly Microsoft.Extensions.Logging.ILogger<UnifiedResourceManager> _logger;

        public MetricsCollector(Microsoft.Extensions.Logging.ILogger<UnifiedResourceManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets a metric value.
        /// </summary>
        /// <param name="key">The key of the metric.</param>
        /// <returns>The metric value.</returns>
        public double GetMetric(string key)
        {
            return _metrics.GetValueOrDefault(key);
        }

        /// <summary>
        /// Disposes of the metrics collector.
        /// </summary>
        public void Dispose()
        {
            _metrics.Clear();
        }
    }
}