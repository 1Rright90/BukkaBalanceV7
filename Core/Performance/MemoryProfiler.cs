using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Logging;
using YSBCaptain.Core.Telemetry;

namespace YSBCaptain.Core.Performance
{
    /// <summary>
    /// Provides comprehensive memory profiling and monitoring capabilities.
    /// </summary>
    public class MemoryProfiler : IDisposable
    {
        private static readonly object _lock = new object();
        private static readonly Lazy<MemoryProfiler> _instance = new Lazy<MemoryProfiler>(() => new MemoryProfiler(), true);
        private readonly ConcurrentDictionary<string, ConcurrentQueue<MemorySnapshot>> _snapshots;
        private readonly ConcurrentDictionary<string, DateTime> _lastCleanupTime;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;
        private readonly ITelemetry _telemetry;
        private readonly int _maxSnapshotsPerCategory;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _monitoringInterval;
        private readonly DateTime _monitoringStartTime;
        private bool _isMonitoring;
        private bool _isDisposed;

        private MemoryProfiler()
        {
            _snapshots = new ConcurrentDictionary<string, ConcurrentQueue<MemorySnapshot>>();
            _lastCleanupTime = new ConcurrentDictionary<string, DateTime>();
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = LoggerFactory.Create(GetType());
            _telemetry = TelemetryFactory.Create();
            _maxSnapshotsPerCategory = 1000;
            _cleanupInterval = TimeSpan.FromMinutes(5);
            _monitoringInterval = TimeSpan.FromSeconds(30);
            _monitoringStartTime = DateTime.UtcNow;
            _isMonitoring = false;
            _isDisposed = false;
        }

        public static MemoryProfiler Instance => _instance.Value;

        public async Task StartMonitoringAsync()
        {
            ThrowIfDisposed();
            
            if (_isMonitoring) return;

            _isMonitoring = true;
            _logger.LogInformation("Memory profiling started");

            try
            {
                await MonitorMemoryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error starting memory monitoring: {ex.Message}");
                _isMonitoring = false;
            }
        }

        public async Task StopMonitoringAsync()
        {
            ThrowIfDisposed();
            
            if (!_isMonitoring) return;

            try
            {
                _cancellationTokenSource.Cancel();
                _isMonitoring = false;
                _logger.LogInformation("Memory profiling stopped");
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping memory monitoring: {ex.Message}");
            }
        }

        private async Task MonitorMemoryAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var snapshot = await TakeMemorySnapshotAsync("System").ConfigureAwait(false);
                    await _telemetry.TrackMetricAsync("Memory.WorkingSet", snapshot.WorkingSet).ConfigureAwait(false);
                    await _telemetry.TrackMetricAsync("Memory.TotalAllocated", snapshot.TotalMemory).ConfigureAwait(false);
                    await _telemetry.TrackMetricAsync("Memory.GC.Gen0Collections", snapshot.GCGen0Collections).ConfigureAwait(false);
                    await _telemetry.TrackMetricAsync("Memory.GC.Gen1Collections", snapshot.GCGen1Collections).ConfigureAwait(false);
                    await _telemetry.TrackMetricAsync("Memory.GC.Gen2Collections", snapshot.GCGen2Collections).ConfigureAwait(false);

                    await CleanupSnapshotsAsync().ConfigureAwait(false);
                    await Task.Delay(_monitoringInterval, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in memory monitoring: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
        }

        public async Task<MemorySnapshot> TakeMemorySnapshotAsync(string category)
        {
            ThrowIfDisposed();
            
            try
            {
                var snapshot = new MemorySnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    Category = category,
                    TotalMemory = GC.GetTotalMemory(false),
                    WorkingSet = Process.GetCurrentProcess().WorkingSet64,
                    GCGen0Collections = GC.CollectionCount(0),
                    GCGen1Collections = GC.CollectionCount(1),
                    GCGen2Collections = GC.CollectionCount(2),
                    LargeObjectHeapSize = GC.GetGeneration(GC.GetGCMemoryInfo().LargeObjectHeapSize),
                    FragmentedBytes = GC.GetGCMemoryInfo().FragmentedBytes
                };

                var queue = _snapshots.GetOrAdd(category, _ => new ConcurrentQueue<MemorySnapshot>());
                queue.Enqueue(snapshot);

                await EnforceSnapshotLimitAsync(category).ConfigureAwait(false);
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error taking memory snapshot for category {category}: {ex.Message}");
                throw;
            }
        }

        public IEnumerable<MemorySnapshot> GetSnapshots(string category)
        {
            ThrowIfDisposed();
            
            return _snapshots.TryGetValue(category, out var queue) 
                ? queue.ToArray() 
                : Array.Empty<MemorySnapshot>();
        }

        private async Task EnforceSnapshotLimitAsync(string category)
        {
            if (!_snapshots.TryGetValue(category, out var queue)) return;

            while (queue.Count > _maxSnapshotsPerCategory)
            {
                if (queue.TryDequeue(out _))
                {
                    await Task.Yield();
                }
            }
        }

        private async Task CleanupSnapshotsAsync()
        {
            foreach (var category in _snapshots.Keys)
            {
                try
                {
                    if (!_lastCleanupTime.TryGetValue(category, out var lastCleanup) ||
                        DateTime.UtcNow - lastCleanup > _cleanupInterval)
                    {
                        await CleanupCategoryAsync(category).ConfigureAwait(false);
                        _lastCleanupTime.AddOrUpdate(category, DateTime.UtcNow, (_, __) => DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error cleaning up snapshots for category {category}: {ex.Message}");
                }
            }
        }

        private async Task CleanupCategoryAsync(string category)
        {
            if (!_snapshots.TryGetValue(category, out var queue)) return;

            var cutoffTime = DateTime.UtcNow - TimeSpan.FromHours(1);
            var newQueue = new ConcurrentQueue<MemorySnapshot>();

            while (queue.TryDequeue(out var snapshot))
            {
                if (snapshot.Timestamp >= cutoffTime)
                {
                    newQueue.Enqueue(snapshot);
                }
                await Task.Yield();
            }

            _snapshots.TryUpdate(category, newQueue, queue);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MemoryProfiler));
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _snapshots.Clear();
            _lastCleanupTime.Clear();
        }
    }

    public class MemorySnapshot
    {
        public DateTime Timestamp { get; set; }
        public string Category { get; set; }
        public long TotalMemory { get; set; }
        public long WorkingSet { get; set; }
        public int GCGen0Collections { get; set; }
        public int GCGen1Collections { get; set; }
        public int GCGen2Collections { get; set; }
        public long LargeObjectHeapSize { get; set; }
        public long FragmentedBytes { get; set; }

        public string GetFormattedWorkingSet() => FormatBytes(WorkingSet);
        public string GetFormattedTotalMemory() => FormatBytes(TotalMemory);
        public string GetFormattedLOHSize() => FormatBytes(LargeObjectHeapSize);
        public string GetFormattedFragmentation() => FormatBytes(FragmentedBytes);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }
}
