using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;
using YSBCaptain.Core.Models;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Gameplay.Systems.Spawning
{
    /// <summary>
    /// Manages the spawning of agents in the game world, handling spawn requests and maintaining spawn state.
    /// Implements the Singleton pattern for global access.
    /// </summary>
    /// <remarks>
    /// This class provides:
    /// - Asynchronous spawn request processing
    /// - Spawn request queuing and prioritization
    /// - System load monitoring and throttling
    /// - Resource management integration
    /// - Thread-safe operations
    /// </remarks>
    public sealed class SpawnManager : ISpawnManager, IDisposable
    {
        private static readonly Lazy<SpawnManager> _instance = 
            new Lazy<SpawnManager>(() => new SpawnManager(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Gets the singleton instance of the SpawnManager.
        /// </summary>
        public static SpawnManager Instance => _instance.Value;

        private readonly ILogger<SpawnManager> _logger;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly IResourceManager _resourceManager;
        private readonly ConcurrentDictionary<string, SpawnRequest> _activeRequests;
        private readonly ConcurrentQueue<SpawnRequest> _pendingRequests;
        private readonly SemaphoreSlim _requestSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private readonly int _maxConcurrentSpawns;
        private readonly int _requestTimeoutMs;
        private readonly int _maxQueueSize;
        private readonly float _loadThreshold;
        
        private int _currentSpawns;
        private bool _isDisposed;

        /// <summary>
        /// Private constructor to enforce singleton pattern.
        /// Initializes spawn system components and starts the request processing queue.
        /// </summary>
        private SpawnManager()
        {
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                                 .CreateLogger<SpawnManager>();
            _performanceMonitor = SpawnPerformanceMonitor.Instance;
            _resourceManager = UnifiedResourceManager.Instance;
            
            try
            {
                InitializeSpawnSystem();
                
                _activeRequests = new ConcurrentDictionary<string, SpawnRequest>();
                _pendingRequests = new ConcurrentQueue<SpawnRequest>();
                _requestSemaphore = new SemaphoreSlim(1, 1);
                _cancellationTokenSource = new CancellationTokenSource();

                _maxConcurrentSpawns = 100;
                _requestTimeoutMs = 30000;
                _maxQueueSize = 1000;
                _loadThreshold = 0.8f;
                _currentSpawns = 0;
                _isDisposed = false;

                _ = StartProcessingQueue().ConfigureAwait(false);
                
                _logger.LogInformation("SpawnManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SpawnManager");
                throw new InvalidOperationException("Failed to initialize SpawnManager", ex);
            }
        }

        /// <summary>
        /// Initializes the spawn system resources and registers them with the resource manager.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when resource registration fails.</exception>
        private void InitializeSpawnSystem()
        {
            try
            {
                _resourceManager.RegisterResource("SpawnPool", new SpawnPoolResource());
                _resourceManager.RegisterResource("SpawnPoints", new SpawnPointCollection());
                _logger.LogInformation("Spawn system resources initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize spawn system resources");
                throw new InvalidOperationException("Failed to initialize spawn system resources", ex);
            }
        }

        /// <summary>
        /// Requests an asynchronous spawn operation with the specified parameters.
        /// </summary>
        /// <param name="request">The spawn request containing spawn parameters.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the request.</param>
        /// <returns>A SpawnResult indicating the outcome of the spawn request.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the SpawnManager has been disposed.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public async Task<SpawnResult> RequestSpawnAsync(SpawnRequest request, CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
            {
                _logger.LogError("Attempted to use disposed SpawnManager");
                throw new ObjectDisposedException(nameof(SpawnManager));
            }

            if (request == null)
            {
                _logger.LogError("Null spawn request provided");
                throw new ArgumentNullException(nameof(request));
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
            try
            {
                await _requestSemaphore.WaitAsync(linkedCts.Token).ConfigureAwait(false);
                try
                {
                    if (_pendingRequests.Count >= _maxQueueSize)
                    {
                        _logger.LogWarning("Spawn request queue is full. Request rejected.");
                        return new SpawnResult(SpawnStatus.QueueFull);
                    }

                    var systemLoad = await _resourceManager.GetSystemLoadAsync(linkedCts.Token).ConfigureAwait(false);
                    if (systemLoad > _loadThreshold)
                    {
                        _logger.LogWarning($"System load too high ({systemLoad:F2}). Request rejected.");
                        return new SpawnResult(SpawnStatus.SystemOverloaded);
                    }

                    _pendingRequests.Enqueue(request);
                    _activeRequests.TryAdd(request.Id, request);
                    
                    _logger.LogDebug($"Spawn request {request.Id} queued successfully");
                    _performanceMonitor.TrackMetric("PendingSpawnRequests", _pendingRequests.Count);
                    
                    return new SpawnResult(SpawnStatus.Pending);
                }
                finally
                {
                    _requestSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Spawn request {request.Id} was cancelled");
                return new SpawnResult(SpawnStatus.Cancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing spawn request {request.Id}");
                return new SpawnResult(SpawnStatus.Error, ex);
            }
        }

        /// <summary>
        /// Waits for the completion of a spawn request.
        /// </summary>
        /// <param name="request">The spawn request to wait for.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the wait.</param>
        /// <returns>A SpawnResult indicating the outcome of the spawn request.</returns>
        private async Task<SpawnResult> WaitForSpawnCompletionAsync(SpawnRequest request, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(_requestTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    if (_activeRequests.TryGetValue(request.Id, out var currentRequest))
                    {
                        if (currentRequest.Status != SpawnStatus.Pending)
                        {
                            _activeRequests.TryRemove(request.Id, out _);
                            return new SpawnResult(currentRequest.Status);
                        }
                    }
                    else
                    {
                        return new SpawnResult(SpawnStatus.NotFound);
                    }

                    await Task.Delay(100, linkedCts.Token).ConfigureAwait(false);
                }

                return new SpawnResult(SpawnStatus.Timeout);
            }
            catch (OperationCanceledException)
            {
                return new SpawnResult(SpawnStatus.Cancelled);
            }
        }

        /// <summary>
        /// Starts the request processing queue.
        /// </summary>
        private async Task StartProcessingQueue()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested && !_isDisposed)
            {
                try
                {
                    if (await _resourceManager.IsCpuThrottledAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
                    {
                        await Task.Delay(500, _cancellationTokenSource.Token).ConfigureAwait(false);
                        continue;
                    }

                    if (Interlocked.CompareExchange(ref _currentSpawns, _currentSpawns, _maxConcurrentSpawns) >= _maxConcurrentSpawns)
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token).ConfigureAwait(false);
                        continue;
                    }

                    if (!_pendingRequests.TryDequeue(out var request))
                    {
                        await Task.Delay(100, _cancellationTokenSource.Token).ConfigureAwait(false);
                        continue;
                    }

                    // Fire and forget with proper error handling
                    _ = ProcessSpawnRequestAsync(request)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger.LogError(t.Exception.Flatten().InnerException, 
                                    "Unhandled error in spawn request processing");
                            }
                        }, TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (OperationCanceledException) when (_isDisposed || _cancellationTokenSource.Token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in spawn queue processing");
                    await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Processes a spawn request asynchronously.
        /// </summary>
        /// <param name="request">The spawn request to process.</param>
        private async Task ProcessSpawnRequestAsync(SpawnRequest request)
        {
            var spawnId = Guid.NewGuid().ToString();
            
            try
            {
                await _performanceMonitor.BeginSpawnOperationAsync(spawnId, _cancellationTokenSource.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _currentSpawns);

                var result = await ExecuteSpawnAsync(request).ConfigureAwait(false);
                
                if (_activeRequests.TryGetValue(request.Id, out var currentRequest))
                {
                    currentRequest.Status = result.Status;
                    currentRequest.ErrorMessage = result.ErrorMessage;
                }

                await _performanceMonitor.EndSpawnOperationAsync(spawnId, result.Status == SpawnStatus.Success, _cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing spawn request {request.Id}");
                if (_activeRequests.TryGetValue(request.Id, out var currentRequest))
                {
                    currentRequest.Status = SpawnStatus.Error;
                    currentRequest.ErrorMessage = ex.Message;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _currentSpawns);
            }
        }

        /// <summary>
        /// Executes a spawn operation asynchronously.
        /// </summary>
        /// <param name="request">The spawn request to execute.</param>
        /// <returns>A SpawnResult indicating the outcome of the spawn operation.</returns>
        private async Task<SpawnResult> ExecuteSpawnAsync(SpawnRequest request)
        {
            try
            {
                // Simulate spawn execution
                await Task.Delay(new Random().Next(100, 500), _cancellationTokenSource.Token).ConfigureAwait(false);
                return new SpawnResult(SpawnStatus.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing spawn for request {request.Id}");
                return new SpawnResult(SpawnStatus.Error, ex.Message);
            }
        }

        /// <summary>
        /// Disposes the SpawnManager instance.
        /// </summary>
        public async Task DisposeAsync()
        {
            await DisposeAsync(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the SpawnManager instance.
        /// </summary>
        /// <param name="disposing">True if disposing, false if finalizing.</param>
        private async Task DisposeAsync(bool disposing)
        {
            if (!disposing || _isDisposed)
                return;

            _isDisposed = true;
            _cancellationTokenSource.Cancel();

            // Wait for any pending operations to complete
            while (Interlocked.CompareExchange(ref _currentSpawns, _currentSpawns, 0) > 0)
            {
                await Task.Delay(100, _cancellationTokenSource.Token).ConfigureAwait(false);
            }

            _cancellationTokenSource.Dispose();
            _requestSemaphore.Dispose();

            // Clear collections
            _activeRequests.Clear();
            while (_pendingRequests.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Disposes the spawn manager.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _pendingRequests.Clear();
                _activeRequests.Clear();
                _isDisposed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing spawn manager");
            }
        }
    }

    /// <summary>
    /// Represents a spawn request.
    /// </summary>
    public sealed class SpawnRequest
    {
        private int _status;

        /// <summary>
        /// Gets the unique identifier of the spawn request.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the spawn parameters.
        /// </summary>
        public SpawnParameters Parameters { get; }

        /// <summary>
        /// Gets or sets the status of the spawn request.
        /// </summary>
        public SpawnStatus Status 
        { 
            get => (SpawnStatus)Interlocked.CompareExchange(ref _status, 0, 0);
            set => Interlocked.Exchange(ref _status, (int)value);
        }

        /// <summary>
        /// Gets or sets the error message of the spawn request.
        /// </summary>
        private string _errorMessage;
        public string ErrorMessage
        {
            get => Volatile.Read(ref _errorMessage);
            set => Volatile.Write(ref _errorMessage, value);
        }

        /// <summary>
        /// Initializes a new instance of the SpawnRequest class.
        /// </summary>
        /// <param name="parameters">The spawn parameters.</param>
        public SpawnRequest(SpawnParameters parameters)
        {
            Id = Guid.NewGuid().ToString();
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            _status = (int)SpawnStatus.Pending;
        }
    }

    /// <summary>
    /// Represents the result of a spawn operation.
    /// </summary>
    public sealed class SpawnResult
    {
        /// <summary>
        /// Gets the status of the spawn operation.
        /// </summary>
        public SpawnStatus Status { get; }

        /// <summary>
        /// Gets the error message of the spawn operation.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of the SpawnResult class.
        /// </summary>
        /// <param name="status">The status of the spawn operation.</param>
        /// <param name="errorMessage">The error message of the spawn operation.</param>
        public SpawnResult(SpawnStatus status, string errorMessage = null)
        {
            Status = status;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Represents the parameters of a spawn operation.
    /// </summary>
    public sealed class SpawnParameters
    {
        /// <summary>
        /// Gets the type of the spawn operation.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the properties of the spawn operation.
        /// </summary>
        public ImmutableDictionary<string, object> Properties { get; }

        /// <summary>
        /// Initializes a new instance of the SpawnParameters class.
        /// </summary>
        /// <param name="type">The type of the spawn operation.</param>
        /// <param name="properties">The properties of the spawn operation.</param>
        public SpawnParameters(string type, IDictionary<string, object> properties)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Properties = properties?.ToImmutableDictionary() ?? 
                        ImmutableDictionary<string, object>.Empty;
        }
    }

    /// <summary>
    /// Defines the possible statuses of a spawn operation.
    /// </summary>
    public enum SpawnStatus
    {
        /// <summary>
        /// The spawn operation is pending.
        /// </summary>
        Pending,
        /// <summary>
        /// The spawn operation was successful.
        /// </summary>
        Success,
        /// <summary>
        /// The spawn operation failed.
        /// </summary>
        Error,
        /// <summary>
        /// The spawn operation was cancelled.
        /// </summary>
        Cancelled,
        /// <summary>
        /// The spawn operation timed out.
        /// </summary>
        Timeout,
        /// <summary>
        /// The spawn request queue is full.
        /// </summary>
        QueueFull,
        /// <summary>
        /// The system is overloaded.
        /// </summary>
        SystemOverloaded,
        /// <summary>
        /// The spawn request was not found.
        /// </summary>
        NotFound
    }
}
