using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.ResourceManagement
{
    public class DynamicResourceManager : IResourceManager, IDisposable
    {
        private static readonly Lazy<DynamicResourceManager> _instance = new Lazy<DynamicResourceManager>(() => new DynamicResourceManager());
        public static DynamicResourceManager Instance => _instance.Value;

        private readonly ILogger<DynamicResourceManager> _logger;
        private readonly ConcurrentDictionary<string, object> _resources;
        private readonly ConcurrentDictionary<string, IResourceManager.ResourceStatus> _resourceStatuses;
        private bool _isDisposed;

        public event EventHandler<string> ResourceAcquired;
        public event EventHandler<string> ResourceReleased;

        private DynamicResourceManager()
        {
            _resources = new ConcurrentDictionary<string, object>();
            _resourceStatuses = new ConcurrentDictionary<string, IResourceManager.ResourceStatus>();
        }

        public DynamicResourceManager(ILogger<DynamicResourceManager> logger) : this()
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T> AcquireResourceAsync<T>(string resourceId) where T : class
        {
            if (string.IsNullOrEmpty(resourceId))
                throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));

            try
            {
                if (_resources.TryGetValue(resourceId, out object resource))
                {
                    if (resource is T typedResource)
                    {
                        _logger?.LogDebug($"Resource {resourceId} retrieved from cache");
                        _resourceStatuses.TryUpdate(resourceId, IResourceManager.ResourceStatus.InUse, IResourceManager.ResourceStatus.Available);
                        OnResourceAcquired(resourceId);
                        return typedResource;
                    }
                }

                _logger?.LogInformation($"Loading resource {resourceId}");
                var newResource = await LoadResourceAsync<T>(resourceId);
                if (newResource != null)
                {
                    _resources.TryAdd(resourceId, newResource);
                    _resourceStatuses.TryAdd(resourceId, IResourceManager.ResourceStatus.InUse);
                    OnResourceAcquired(resourceId);
                    return newResource;
                }
                
                _resourceStatuses.TryAdd(resourceId, IResourceManager.ResourceStatus.Error);
                throw new InvalidOperationException($"Failed to load resource {resourceId}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to acquire resource {resourceId}");
                _resourceStatuses.TryUpdate(resourceId, IResourceManager.ResourceStatus.Error, IResourceManager.ResourceStatus.Available);
                throw;
            }
        }

        public void ReleaseResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
                throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));

            if (_resourceStatuses.TryUpdate(resourceId, IResourceManager.ResourceStatus.Available, IResourceManager.ResourceStatus.InUse))
            {
                _logger?.LogDebug($"Resource {resourceId} released");
                OnResourceReleased(resourceId);
            }
        }

        public async Task<bool> IsResourceAvailableAsync(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
                throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));

            return await Task.FromResult(
                _resourceStatuses.TryGetValue(resourceId, out var status) && 
                status == IResourceManager.ResourceStatus.Available
            );
        }

        public Dictionary<string, IResourceManager.ResourceStatus> GetResourceStatuses()
        {
            return _resourceStatuses.ToDictionary(kvp => kvp.Key, kvp => (IResourceManager.ResourceStatus)kvp.Value);
        }

        private async Task<T> LoadResourceAsync<T>(string resourceId) where T : class
        {
            // Implement resource loading logic here based on resource type
            await Task.Delay(100); // Simulate loading

            // Example implementation (replace with actual resource loading logic)
            if (typeof(T).Name == "TextureResource")
            {
                // Load texture
                return null;
            }
            else if (typeof(T).Name == "ModelResource")
            {
                // Load 3D model
                return null;
            }
            
            return null;
        }

        protected virtual void OnResourceAcquired(string resourceId)
        {
            ResourceAcquired?.Invoke(this, resourceId);
        }

        protected virtual void OnResourceReleased(string resourceId)
        {
            ResourceReleased?.Invoke(this, resourceId);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _resources.Clear();
            _resourceStatuses.Clear();
            
            GC.SuppressFinalize(this);
        }
    }
}
