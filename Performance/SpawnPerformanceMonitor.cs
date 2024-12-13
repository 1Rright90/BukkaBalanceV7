using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Performance
{
    /// <summary>
    /// Monitors and manages spawn-related performance metrics in a thread-safe manner.
    /// Implements the Singleton pattern and provides thread-safe access to performance data.
    /// </summary>
    public sealed class SpawnPerformanceMonitor : IDisposable
    {
        private static readonly Lazy<SpawnPerformanceMonitor> _instance =
            new(() => new SpawnPerformanceMonitor(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static SpawnPerformanceMonitor Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, CircularBuffer<float>> _metricHistory;
        private readonly ConcurrentDictionary<string, long> _metrics;
        private readonly ConcurrentQueue<Action> _eventQueue;
        private readonly ConcurrentQueue<ResourceSnapshot> _resourceHistory;
        private readonly SemaphoreSlim _queueSemaphore;
        private readonly SemaphoreSlim _metricsSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger<SpawnPerformanceMonitor> _logger;

        private Task _eventProcessorTask;
        private bool _isDisposed;

        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMilliseconds = 200;
        private const int InitialHistorySize = 100;
        private const int MaxHistorySize = 1000;

        private SpawnPerformanceMonitor()
        {
            _metrics = new ConcurrentDictionary<string, long>();
            _metricHistory = new ConcurrentDictionary<string, CircularBuffer<float>>();
            _eventQueue = new ConcurrentQueue<Action>();
            _resourceHistory = new ConcurrentQueue<ResourceSnapshot>();
            _queueSemaphore = new SemaphoreSlim(0);
            _metricsSemaphore = new SemaphoreSlim(1);
            _cancellationTokenSource = new CancellationTokenSource();

            _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<SpawnPerformanceMonitor>();

            InitializeDefaultMetrics();
            _eventProcessorTask = Task.Run(() => ProcessEventQueueAsync(_cancellationTokenSource.Token));
        }

        private void InitializeDefaultMetrics()
        {
            var defaultMetrics = new[] { "SpawnTime", "SuccessRate", "MemoryUsage", "ConcurrentSpawns" };
            foreach (var metric in defaultMetrics)
            {
                _metricHistory.TryAdd(metric, new CircularBuffer<float>(InitialHistorySize));
            }
        }

        /// <summary>
        /// Increments the specified metric by the given value.
        /// </summary>
        public void IncrementMetric(string metricName, long value = 1)
        {
            if (string.IsNullOrEmpty(metricName)) throw new ArgumentNullException(nameof(metricName));
            _metrics.AddOrUpdate(metricName, value, (_, v) => v + value);
        }

        /// <summary>
        /// Gets the current value of the specified metric.
        /// </summary>
        public long GetMetric(string metricName)
        {
            if (string.IsNullOrEmpty(metricName)) throw new ArgumentNullException(nameof(metricName));
            return _metrics.TryGetValue(metricName, out var value) ? value : 0;
        }

        /// <summary>
        /// Enqueues an event to be processed asynchronously by the event processor.
        /// </summary>
        public void EnqueueEvent(Action action)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SpawnPerformanceMonitor));
            if (action == null) throw new ArgumentNullException(nameof(action));

            _eventQueue.Enqueue(action);
            _queueSemaphore.Release();
        }

        private async Task ProcessEventQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _queueSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    if (_eventQueue.TryDequeue(out var action))
                    {
                        await ExecuteWithRetryAsync(action, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing event queue.");
                }
            }
        }

        private async Task ExecuteWithRetryAsync(Action action, CancellationToken cancellationToken)
        {
            int attempt = 0;

            while (attempt < MaxRetryAttempts && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error executing action. Attempt {attempt + 1} of {MaxRetryAttempts}.");
                    attempt++;

                    if (attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(RetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogError(ex, "Action failed after maximum retry attempts.");
                    }
                }
            }
        }

        /// <summary>
        /// Stores a resource snapshot in the history queue.
        /// </summary>
        public async Task StoreSnapshotAsync(ResourceSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SpawnPerformanceMonitor));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            await _metricsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _resourceHistory.Enqueue(snapshot);

                while (_resourceHistory.Count > MaxHistorySize && _resourceHistory.TryDequeue(out _)) { }
            }
            finally
            {
                _metricsSemaphore.Release();
            }
        }

        public IReadOnlyList<ResourceSnapshot> GetResourceHistory()
        {
            return _resourceHistory.ToList();
        }

        public IReadOnlyDictionary<string, long> GetAllMetrics()
        {
            return new Dictionary<string, long>(_metrics);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _cancellationTokenSource.Cancel();

            try
            {
                _eventProcessorTask?.Wait();
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
                // Ignore cancellation exceptions
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _queueSemaphore.Dispose();
                _metricsSemaphore.Dispose();
            }
        }
    }

    internal sealed class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly int _capacity;
        private int _start;
        private int _count;
        private readonly object _lock = new();

        public CircularBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
            _start = 0;
            _count = 0;
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                int index = (_start + _count) % _capacity;
                _buffer[index] = item;

                if (_count < _capacity)
                {
                    _count++;
                }
                else
                {
                    _start = (_start + 1) % _capacity;
                }
            }
        }

        public T[] ToArray()
        {
            lock (_lock)
            {
                T[] result = new T[_count];
                if (_count == 0) return result;

                if (_start + _count <= _capacity)
                {
                    Array.Copy(_buffer, _start, result, 0, _count);
                }
                else
                {
                    int firstPart = _capacity - _start;
                    Array.Copy(_buffer, _start, result, 0, firstPart);
                    Array.Copy(_buffer, 0, result, firstPart, _count - firstPart);
                }
                return result;
            }
        }
    }

    public sealed class ResourceSnapshot
    {
        public DateTime Timestamp { get; }
        public float CpuUsage { get; }
        public float MemoryUsage { get; }
        public int ConcurrentSpawns { get; }
        public float SpawnSuccessRate { get; }
        public float AverageSpawnTime { get; }
        public float SystemLoad { get; }

        public ResourceSnapshot(
            float cpuUsage,
            float memoryUsage,
            int concurrentSpawns,
            float spawnSuccessRate,
            float averageSpawnTime,
            float systemLoad)
        {
            Timestamp = DateTime.UtcNow;
            CpuUsage = cpuUsage;
            MemoryUsage = memoryUsage;
            ConcurrentSpawns = concurrentSpawns;
            SpawnSuccessRate = spawnSuccessRate;
            AverageSpawnTime = averageSpawnTime;
            SystemLoad = systemLoad;
        }
    }
}