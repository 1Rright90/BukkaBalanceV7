using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.Resources
{
    /// <summary>
    /// Manages dynamic resources with thread-safe operations and proper cleanup.
    /// </summary>
    /// <remarks>
    /// Follows TaleWorlds' resource management patterns and provides thread-safe access
    /// to resources with proper cleanup and memory management.
    /// </remarks>
    public sealed class DynamicResourceManager : BaseDisposable
    {
        private static readonly Lazy<DynamicResourceManager> _instance = 
            new Lazy<DynamicResourceManager>(() => new DynamicResourceManager());
        
        private readonly ConcurrentDictionary<string, WeakReference<object>> _resources;
        private readonly ILogger<DynamicResourceManager> _logger;
        private readonly SemaphoreSlim _lock;

        /// <summary>
        /// Gets the singleton instance of the DynamicResourceManager.
        /// </summary>
        public static DynamicResourceManager Instance => _instance.Value;

        private DynamicResourceManager()
        {
            _resources = new ConcurrentDictionary<string, WeakReference<object>>();
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<DynamicResourceManager>();
            _lock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Registers a resource with the specified key.
        /// </summary>
        /// <param name="key">The unique identifier for the resource.</param>
        /// <param name="resource">The resource to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when key or resource is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when registration fails.</exception>
        public async Task RegisterResourceAsync(string key, object resource)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (resource == null) throw new ArgumentNullException(nameof(resource));

            try
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                _resources.AddOrUpdate(key, 
                    new WeakReference<object>(resource),
                    (_, existing) => new WeakReference<object>(resource));
                _logger.LogInformation("Resource registered successfully: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register resource: {Key}", key);
                throw new InvalidOperationException($"Failed to register resource: {key}", ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Attempts to retrieve a resource of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of resource to retrieve.</typeparam>
        /// <param name="key">The key of the resource to retrieve.</param>
        /// <param name="resource">The retrieved resource if successful.</param>
        /// <returns>True if the resource was retrieved successfully; otherwise, false.</returns>
        public async Task<(bool success, T resource)> TryGetResourceAsync<T>(string key) where T : class
        {
            ThrowIfDisposed();
            
            try
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                
                if (!_resources.TryGetValue(key, out var weakRef))
                {
                    _logger.LogDebug("Resource not found: {Key}", key);
                    return (false, null);
                }

                if (!weakRef.TryGetTarget(out var target))
                {
                    _logger.LogDebug("Resource reference expired: {Key}", key);
                    _resources.TryRemove(key, out _);
                    return (false, null);
                }

                if (target is T typedResource)
                {
                    _logger.LogDebug("Resource retrieved successfully: {Key}", key);
                    return (true, typedResource);
                }

                _logger.LogWarning("Resource type mismatch for key: {Key}", key);
                return (false, null);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Unregisters a resource with the specified key.
        /// </summary>
        /// <param name="key">The key of the resource to unregister.</param>
        public async Task UnregisterResourceAsync(string key)
        {
            ThrowIfDisposed();
            
            try
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                if (_resources.TryRemove(key, out _))
                {
                    _logger.LogInformation("Resource unregistered successfully: {Key}", key);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        protected override void DisposeManagedResources()
        {
            _resources.Clear();
            _lock.Dispose();
        }
    }
}
