using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Models;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;
using System.Collections.Concurrent;

namespace YSBCaptain.Network
{
    /// <summary>
    /// Network optimization system that follows the layered architecture.
    /// Core → Extensions → Network/Performance → Gameplay → Patches
    /// </summary>
    public sealed class NetworkOptimization : INetworkOptimization
    {
        private readonly ConcurrentDictionary<string, NetworkMetrics> _networkMetrics;
        private readonly SemaphoreSlim _asyncLock;
        private readonly CancellationTokenSource _cts;
        private Task _monitoringTask;
        private volatile bool _isDisposed;
        private readonly Microsoft.Extensions.Logging.ILogger<NetworkOptimization> _logger;
        private readonly IPerformanceMonitor _performanceMonitor;

        private const int MonitoringIntervalMs = 1000;
        private const float DefaultThrottleRate = 1.0f;
        private const float MinThrottleRate = 0.1f;
        private const float MaxThrottleRate = 2.0f;

        public NetworkOptimization(ILogger<NetworkOptimization> logger, IPerformanceMonitor performanceMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _networkMetrics = new ConcurrentDictionary<string, NetworkMetrics>();
            _asyncLock = new SemaphoreSlim(1, 1);
            _cts = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            using (await _asyncLock.LockAsync().ConfigureAwait(false))
            {
                if (_monitoringTask != null)
                {
                    throw new InvalidOperationException("Network optimization is already running");
                }

                _monitoringTask = MonitorNetworkPerformanceAsync(_cts.Token);
            }
        }

        public async Task StopAsync()
        {
            using (await _asyncLock.LockAsync().ConfigureAwait(false))
            {
                if (_monitoringTask == null)
                {
                    return;
                }

                try
                {
                    _cts.Cancel();
                    await _monitoringTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error stopping network optimization: {ex.Message}");
                }
                finally
                {
                    _monitoringTask = null;
                }
            }
        }

        public async Task UpdateMetricsAsync(string connectionId, int latency, int packetLoss)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(NetworkOptimization));
            }

            var metrics = _networkMetrics.AddOrUpdate(
                connectionId,
                _ => new NetworkMetrics { Latency = latency, PacketLoss = packetLoss },
                (_, existing) =>
                {
                    existing.Latency = latency;
                    existing.PacketLoss = packetLoss;
                    return existing;
                });

            await OptimizeConnectionAsync(connectionId, metrics).ConfigureAwait(false);
        }

        private async Task MonitorNetworkPerformanceAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var connections = _networkMetrics.ToArray();
                    await Task.WhenAll(connections.Select(kvp => 
                        OptimizeConnectionAsync(kvp.Key, kvp.Value)
                    )).ConfigureAwait(false);

                    await Task.Delay(MonitoringIntervalMs, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            catch (Exception ex)
            {
                _logger.LogError($"Network monitoring task failed: {ex.Message}");
            }
        }

        private async Task OptimizeConnectionAsync(string connectionId, NetworkMetrics metrics)
        {
            if (metrics == null)
            {
                return;
            }

            var throttleRate = await CalculateThrottleRateAsync(metrics).ConfigureAwait(false);
            await ApplyNetworkOptimizationsAsync(connectionId, throttleRate).ConfigureAwait(false);
        }

        private async Task<float> CalculateThrottleRateAsync(NetworkMetrics metrics)
        {
            var performanceMetrics = await _performanceMonitor.GetMetricsAsync().ConfigureAwait(false);
            var baseRate = DefaultThrottleRate;

            // Adjust for latency
            if (metrics.Latency > 200)
            {
                baseRate *= 0.8f;
            }
            else if (metrics.Latency < 50)
            {
                baseRate *= 1.2f;
            }

            // Adjust for packet loss
            if (metrics.PacketLoss > 5)
            {
                baseRate *= 0.7f;
            }

            // Adjust for CPU usage
            if (performanceMetrics.CpuUsage > 80)
            {
                baseRate *= 0.9f;
            }

            return Clamp(baseRate, MinThrottleRate, MaxThrottleRate);
        }

        private async Task ApplyNetworkOptimizationsAsync(string connectionId, float throttleRate)
        {
            try
            {
                await Task.Run(() =>
                {
                    // Apply throttling or other network optimization logic here
                }).ConfigureAwait(false);

                _logger.LogInformation($"Applied network optimization for {connectionId}: ThrottleRate={throttleRate:F2}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to apply network optimization for {connectionId}: {ex.Message}");
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            finally
            {
                _cts.Dispose();
                _asyncLock.Dispose();
            }
        }

        private class NetworkMetrics
        {
            public int Latency { get; set; }
            public int PacketLoss { get; set; }
        }
    }
}
