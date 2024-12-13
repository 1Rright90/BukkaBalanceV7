using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Logging;
using YSBCaptain.Core.HealthMonitoring;

namespace YSBCaptain.Performance
{
    /// <summary>
    /// Provides memory profiling and monitoring capabilities for Mount &amp; Blade II: Bannerlord.
    /// Implements TaleWorlds' patterns for memory management and profiling.
    /// </summary>
    /// <remarks>
    /// This class follows the performance monitoring patterns used in TaleWorlds.Core
    /// and provides thread-safe memory profiling capabilities.
    /// </remarks>
    public class MemoryProfiler : IDisposable
    {
        private readonly ILogger _logger;
        private readonly Timer _gcTimer;
        private readonly TimeSpan _gcCheckInterval;
        private readonly long _gcThreshold;
        private readonly object _lock = new object();
        private bool _isDisposed;
        private DateTime _lastGcTime;

        private readonly ConcurrentQueue<MemorySnapshot> _snapshots;
        private readonly ConcurrentQueue<ObjectAllocation> _objectAllocations;
        private readonly ConcurrentQueue<MissionPerformanceSnapshot> _missionPerformance;
        private readonly ConcurrentDictionary<string, MemorySnapshot> _namedSnapshots;

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IHealthCheck _healthCheck;
        private readonly ITelemetry _telemetry;

        private const int SnapshotIntervalMs = 1000;
        private const int MaxSnapshots = 1000;
        private const int MaxObjectAllocations = 1000;
        private const int MaxMissionSnapshots = 1000;
        private const long HighMemoryThresholdBytes = 1024 * 1024 * 1024; // 1 GB

        public MemoryProfiler(ILogger logger, IHealthCheck healthCheck, ITelemetry telemetry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _healthCheck = healthCheck ?? throw new ArgumentNullException(nameof(healthCheck));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

            _gcCheckInterval = TimeSpan.FromMinutes(5);
            _gcThreshold = HighMemoryThresholdBytes;
            _lastGcTime = DateTime.UtcNow;

            _snapshots = new ConcurrentQueue<MemorySnapshot>();
            _objectAllocations = new ConcurrentQueue<ObjectAllocation>();
            _missionPerformance = new ConcurrentQueue<MissionPerformanceSnapshot>();
            _namedSnapshots = new ConcurrentDictionary<string, MemorySnapshot>();
            _cancellationTokenSource = new CancellationTokenSource();

            _gcTimer = new Timer(CheckGCNeeded, null, _gcCheckInterval, _gcCheckInterval);

            _healthCheck.RegisterSystem("MemoryProfiler");
            _logger.LogInformation("Memory profiler initialized successfully.");
        }

        public async Task StartMonitoringAsync()
        {
            _logger.LogInformation("Memory monitoring started.");

            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var snapshot = CreateMemorySnapshot();
                        if (snapshot != null)
                        {
                            _snapshots.Enqueue(snapshot);
                            CleanupQueue(_snapshots, MaxSnapshots);

                            _telemetry.TrackMetric("Memory_WorkingSet", snapshot.WorkingSet, "bytes");
                            _telemetry.TrackMetric("Memory_GC", snapshot.GcTotalMemory, "bytes");

                            if (snapshot.WorkingSet > HighMemoryThresholdBytes)
                            {
                                _healthCheck.UpdateStatus("MemoryProfiler", HealthStatus.Warning, $"High memory usage: {FormatBytes(snapshot.WorkingSet)}");
                            }
                            else
                            {
                                _healthCheck.UpdateStatus("MemoryProfiler", HealthStatus.Healthy, $"Current memory: {FormatBytes(snapshot.WorkingSet)}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error during memory monitoring", ex);
                        _healthCheck.UpdateStatus("MemoryProfiler", HealthStatus.Warning, $"Monitoring error: {ex.Message}");
                    }

                    await Task.Delay(SnapshotIntervalMs, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Memory monitoring canceled.");
            }
        }

        public void StopMonitoring()
        {
            _logger.LogInformation("Memory monitoring stopped.");
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }

        public void TakeSnapshot(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            var snapshot = CreateMemorySnapshot();
            if (snapshot != null && _namedSnapshots.TryAdd(name, snapshot))
            {
                _logger.LogInformation($"Snapshot '{name}' taken successfully.");
                _telemetry.TrackMetric($"MemorySnapshot_{name}", snapshot.WorkingSet, "bytes");
            }
        }

        public void CompareSnapshots(string snapshot1, string snapshot2)
        {
            if (!_namedSnapshots.TryGetValue(snapshot1, out var s1) || !_namedSnapshots.TryGetValue(snapshot2, out var s2))
            {
                _logger.LogWarning($"Snapshots not found: {snapshot1}, {snapshot2}");
                return;
            }

            var workingSetDiff = s2.WorkingSet - s1.WorkingSet;
            var gcDiff = s2.GcTotalMemory - s1.GcTotalMemory;

            _logger.LogInformation($"Memory diff between '{snapshot1}' and '{snapshot2}': Working Set: {FormatBytes(workingSetDiff)}, GC Memory: {FormatBytes(gcDiff)}");
            _telemetry.TrackMetric($"MemoryDiff_{snapshot1}_{snapshot2}", workingSetDiff, "bytes");
        }

        private MemorySnapshot CreateMemorySnapshot()
        {
            try
            {
                using var process = Process.GetCurrentProcess();
                checked
                {
                    var snapshot = new MemorySnapshot
                    {
                        Timestamp = DateTime.UtcNow,
                        WorkingSet = process.WorkingSet64,
                        PrivateMemory = process.PrivateMemorySize64,
                        VirtualMemory = process.VirtualMemorySize64,
                        GcTotalMemory = GC.GetTotalMemory(false)
                    };

                    // Validate memory values
                    if (snapshot.WorkingSet < 0 || snapshot.PrivateMemory < 0 || 
                        snapshot.VirtualMemory < 0 || snapshot.GcTotalMemory < 0)
                    {
                        _logger.LogError("Invalid memory values detected in snapshot");
                        return null;
                    }

                    return snapshot;
                }
            }
            catch (OverflowException ex)
            {
                _logger.LogError("Memory value overflow detected", ex);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error creating memory snapshot", ex);
                return null;
            }
        }

        private void CheckGCNeeded(object state)
        {
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > _gcThreshold)
            {
                GC.Collect();
                _logger.LogInformation($"GC triggered. Freed memory: {FormatBytes(currentMemory - GC.GetTotalMemory(false))}");
            }
        }

        /// <summary>
        /// Formats bytes into a human-readable string with proper size units.
        /// </summary>
        /// <param name="bytes">The number of bytes to format.</param>
        /// <returns>A formatted string representing the size.</returns>
        /// <exception cref="OverflowException">Thrown when calculations would cause an overflow.</exception>
        private string FormatBytes(long bytes)
        {
            try
            {
                checked
                {
                    string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
                    int i = 0;
                    double dblBytes = bytes;

                    while (Math.Abs(dblBytes) >= 1024 && i < suffixes.Length - 1)
                    {
                        dblBytes /= 1024;
                        i++;
                    }

                    return $"{dblBytes:0.00} {suffixes[i]}";
                }
            }
            catch (OverflowException ex)
            {
                throw new OverflowException("Overflow while formatting bytes", ex);
            }
        }

        /// <summary>
        /// Cleans up queues to prevent memory growth.
        /// </summary>
        /// <typeparam name="T">The type of items in the queue.</typeparam>
        /// <param name="queue">The queue to clean up.</param>
        /// <param name="maxItems">Maximum number of items to keep.</param>
        /// <exception cref="ArgumentNullException">Thrown when queue is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when maxItems is less than or equal to 0.</exception>
        private static void CleanupQueue<T>(ConcurrentQueue<T> queue, int maxItems)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));
            if (maxItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxItems));

            while (queue.Count > maxItems && queue.TryDequeue(out _)) { }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _gcTimer.Dispose();
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _isDisposed = true;

            _logger.LogInformation("Memory profiler disposed.");
        }
    }

    public class MemorySnapshot
    {
        public DateTime Timestamp { get; set; }
        public long WorkingSet { get; set; }
        public long PrivateMemory { get; set; }
        public long VirtualMemory { get; set; }
        public long GcTotalMemory { get; set; }
    }
}
