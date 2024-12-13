using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Utilities;

namespace YSBCaptain.Gameplay.Systems.Spawning
{
    /// <summary>
    /// Manages a queue of spawn requests and processes them asynchronously.
    /// Inherits from BaseComponent to integrate with the game's component system.
    /// </summary>
    /// <remarks>
    /// This class provides:
    /// - Thread-safe spawn request queuing
    /// - Asynchronous request processing
    /// - Object pooling for spawn requests
    /// - Performance monitoring
    /// - Structured logging
    /// </remarks>
    public class SpawnQueueManager : BaseComponent
    {
        private readonly ILogger<SpawnQueueManager> _logger;
        private readonly ConcurrentQueue<SpawnRequest> _spawnQueue;
        private readonly ObjectPool<SpawnRequest> _requestPool;
        private readonly IPerformanceMonitor _performanceMonitor;
        private bool _isProcessing;

        /// <summary>
        /// Initializes a new instance of the SpawnQueueManager class.
        /// </summary>
        /// <param name="logger">Logger for structured logging.</param>
        /// <param name="performanceMonitor">Monitor for tracking performance metrics.</param>
        /// <param name="requestPool">Object pool for spawn requests.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
        public SpawnQueueManager(
            ILogger<SpawnQueueManager> logger,
            IPerformanceMonitor performanceMonitor,
            ObjectPool<SpawnRequest> requestPool) : base("SpawnQueueManager")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _requestPool = requestPool ?? throw new ArgumentNullException(nameof(requestPool));
            _spawnQueue = new ConcurrentQueue<SpawnRequest>();
            
            _logger.LogInformation("SpawnQueueManager constructed successfully");
        }

        /// <summary>
        /// Enqueues a spawn request for processing.
        /// </summary>
        /// <param name="request">The spawn request to enqueue.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when enqueueing fails.</exception>
        public async Task EnqueueSpawnRequestAsync(SpawnRequest request)
        {
            if (request == null)
            {
                _logger.LogError("Attempted to enqueue null spawn request");
                throw new ArgumentNullException(nameof(request));
            }

            try
            {
                _spawnQueue.Enqueue(request);
                _logger.LogDebug($"Enqueued spawn request {request.GetId()}");
                _performanceMonitor.TrackMetric("SpawnQueueLength", _spawnQueue.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue spawn request");
                throw new InvalidOperationException("Failed to enqueue spawn request", ex);
            }
        }

        /// <summary>
        /// Processes all pending spawn requests in the queue.
        /// Ensures only one processing operation runs at a time.
        /// </summary>
        public async Task ProcessQueueAsync()
        {
            if (_isProcessing)
            {
                _logger.LogDebug("Queue processing already in progress");
                return;
            }

            _isProcessing = true;
            _logger.LogDebug("Starting queue processing");

            try
            {
                while (_spawnQueue.TryDequeue(out var request))
                {
                    try
                    {
                        await ProcessSpawnRequestAsync(request);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process spawn request {request.GetId()}");
                        request.Complete(false, ex);
                    }
                    finally
                    {
                        _requestPool.Return(request);
                        _performanceMonitor.TrackMetric("ActiveSpawnRequests", _spawnQueue.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during queue processing");
                throw new InvalidOperationException("Failed to process spawn queue", ex);
            }
            finally
            {
                _isProcessing = false;
                _logger.LogDebug("Queue processing completed");
            }
        }

        /// <summary>
        /// Processes a single spawn request asynchronously.
        /// </summary>
        /// <param name="request">The spawn request to process.</param>
        /// <exception cref="InvalidOperationException">Thrown when processing fails.</exception>
        private async Task ProcessSpawnRequestAsync(SpawnRequest request)
        {
            _logger.LogDebug($"Processing spawn request {request.GetId()}");

            try
            {
                using (_performanceMonitor.TrackOperation("SpawnRequestProcessing"))
                {
                    // Implementation of actual spawn logic would go here
                    // This is just a placeholder
                    await Task.Delay(100); // Simulate some work

                    request.Complete(true, null);
                    _logger.LogInformation($"Successfully processed spawn request {request.GetId()}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing spawn request {request.GetId()}");
                request.Complete(false, ex);
                throw new InvalidOperationException($"Failed to process spawn request {request.GetId()}", ex);
            }
        }

        /// <summary>
        /// Starts the SpawnQueueManager and initializes required resources.
        /// </summary>
        public override async Task StartAsync()
        {
            try
            {
                _logger.LogInformation("SpawnQueueManager starting");
                await base.StartAsync();
                _logger.LogInformation("SpawnQueueManager started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SpawnQueueManager");
                throw new InvalidOperationException("Failed to start SpawnQueueManager", ex);
            }
        }

        /// <summary>
        /// Stops the SpawnQueueManager and cleans up resources.
        /// </summary>
        public override async Task StopAsync()
        {
            try
            {
                _logger.LogInformation("SpawnQueueManager stopping");
                await base.StopAsync();
                _logger.LogInformation("SpawnQueueManager stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop SpawnQueueManager");
                throw new InvalidOperationException("Failed to stop SpawnQueueManager", ex);
            }
        }
    }

    /// <summary>
    /// Defines the pooling policy for SpawnRequest objects.
    /// Implements object pooling to reduce memory allocation and improve performance.
    /// </summary>
    public class SpawnRequestPoolPolicy : IPooledObjectPolicy<SpawnRequest>
    {
        /// <summary>
        /// Creates a new SpawnRequest instance for the pool.
        /// </summary>
        /// <returns>A new SpawnRequest instance.</returns>
        public SpawnRequest Create()
        {
            return new SpawnRequest();
        }

        /// <summary>
        /// Returns a SpawnRequest to the pool after resetting its state.
        /// </summary>
        /// <param name="obj">The SpawnRequest to return to the pool.</param>
        /// <returns>True if the object was successfully returned to the pool, false otherwise.</returns>
        public bool Return(SpawnRequest obj)
        {
            if (obj == null)
                return false;

            obj.Reset();
            return true;
        }
    }
}
