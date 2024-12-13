using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.HealthMonitoring;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Models;
using YSBCaptain.Core.Performance;
using YSBCaptain.Core.ResourceManagement;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace YSBCaptain.Core
{
    /// <summary>
    /// Core module for YSBCaptain that manages advanced formation and tactical AI capabilities.
    /// Implements TaleWorlds' MBSubModuleBase for seamless integration with Bannerlord.
    /// </summary>
    /// <remarks>
    /// This class follows TaleWorlds' module patterns and provides:
    /// - Resource management
    /// - System monitoring
    /// - Performance metrics
    /// - Configuration management
    /// All implementations align with Mount and Blade II: Bannerlord's runtime environment.
    /// </remarks>
    public sealed class YSBCaptainCore : MBSubModuleBase, IAsyncDisposable
    {
        private static readonly Lazy<YSBCaptainCore> _instance = new Lazy<YSBCaptainCore>(
            () => new YSBCaptainCore(), 
            LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly IDynamicResourceManager _resourceManager;
        private readonly ISystemMonitor _systemMonitor;
        private readonly IPerformanceMetrics _metrics;
        private readonly ICaptainConfig _config;
        private readonly ILogger<YSBCaptainCore> _logger;
        private volatile bool _isInitialized;
        private volatile bool _isDisposed;
        private readonly TaskCompletionSource<bool> _initializationTcs;
        private readonly AsyncLock _asyncLock = new AsyncLock();

        /// <summary>
        /// Gets the singleton instance of the YSBCaptainCore.
        /// </summary>
        public static YSBCaptainCore Instance => _instance.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="YSBCaptainCore"/> class.
        /// </summary>
        private YSBCaptainCore()
        {
            _initializationTcs = new TaskCompletionSource<bool>();
            _resourceManager = UnifiedResourceManager.Instance;
            _systemMonitor = new SystemMonitor();
            _metrics = new PerformanceMetrics();
            _config = new CaptainConfiguration();
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<YSBCaptainCore>();
        }

        /// <summary>
        /// Called before the initial module screen is set as root.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected override async Task OnBeforeInitialModuleScreenSetAsRootAsync()
        {
            try
            {
                await base.OnBeforeInitialModuleScreenSetAsRootAsync();
                _logger.LogInformation("YSBCaptainCore initializing");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during YSBCaptainCore initialization");
                throw;
            }
        }

        /// <summary>
        /// Initializes the core systems asynchronously.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        public async Task InitializeAsync()
        {
            if (_isInitialized || _isDisposed) return;

            using var initLock = await _asyncLock.LockAsync().ConfigureAwait(false);
            if (_isInitialized || _isDisposed) return;

            try
            {
                await _resourceManager.InitializeAsync().ConfigureAwait(false);
                await _systemMonitor.StartMonitoringAsync().ConfigureAwait(false);
                await _metrics.InitializeAsync().ConfigureAwait(false);
                await _config.LoadAsync().ConfigureAwait(false);

                _isInitialized = true;
                _initializationTcs.SetResult(true);
                _logger.LogInformation("YSBCaptain core systems initialized successfully");
            }
            catch (Exception ex)
            {
                _initializationTcs.SetException(ex);
                _logger.LogError(ex, "Error initializing YSBCaptain core systems");
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        $"Error initializing YSBCaptain core systems: {ex.Message}", 
                        Colors.Red));
                throw new InvalidOperationException("Failed to initialize YSBCaptain core systems", ex);
            }
        }

        /// <summary>
        /// Disposes of the core systems asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            using var disposeLock = await _asyncLock.LockAsync().ConfigureAwait(false);
            if (_isDisposed) return;

            try
            {
                await Task.WhenAll(
                    _resourceManager?.DisposeAsync() ?? Task.CompletedTask,
                    _systemMonitor?.DisposeAsync() ?? Task.CompletedTask,
                    _metrics?.DisposeAsync() ?? Task.CompletedTask
                ).ConfigureAwait(false);

                _isDisposed = true;
                _logger.LogInformation("YSBCaptain core systems disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing YSBCaptain core systems");
                throw;
            }
        }

        /// <summary>
        /// Handles the mission initialization asynchronously.
        /// </summary>
        /// <param name="mission">The mission being initialized.</param>
        private async Task OnMissionInitializeAsync(Mission mission)
        {
            try
            {
                mission.AddMissionBehavior(new MemoryMonitoringBehavior());
                InformationManager.DisplayMessage(new InformationMessage($"Mission initialized: {mission.Name}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnMissionInitialize");
                InformationManager.DisplayMessage(new InformationMessage($"Error in OnMissionInitialize: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Handles the mission cleanup asynchronously.
        /// </summary>
        private async Task OnMissionCleanupAsync()
        {
            try
            {
                await _resourceManager.RequestCleanupAsync().ConfigureAwait(false);
                InformationManager.DisplayMessage(new InformationMessage("Mission cleanup completed"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnMissionCleanup");
                InformationManager.DisplayMessage(new InformationMessage($"Error in OnMissionCleanup: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Handles the game initialization asynchronously.
        /// </summary>
        /// <param name="game">The game being initialized.</param>
        private async Task OnGameInitializeAsync(Game game)
        {
            try
            {
                await _config.InitializeAsync().ConfigureAwait(false);
                InformationManager.DisplayMessage(new InformationMessage("Game initialized successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGameInitialize");
                InformationManager.DisplayMessage(new InformationMessage($"Error in OnGameInitialize: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Handles the game end asynchronously.
        /// </summary>
        private async Task OnGameEndAsync()
        {
            try
            {
                await _resourceManager.RequestCleanupAsync().ConfigureAwait(false);
                InformationManager.DisplayMessage(new InformationMessage("Game ended successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGameEnd");
                InformationManager.DisplayMessage(new InformationMessage($"Error in OnGameEnd: {ex.Message}", Colors.Red));
            }
        }

        /// <summary>
        /// Waits for the core systems to be initialized.
        /// </summary>
        /// <param name="timeoutMs">The timeout in milliseconds.</param>
        /// <returns>A task representing the asynchronous operation, with a boolean result indicating success.</returns>
        public async Task<bool> WaitForInitializationAsync(int timeoutMs = 30000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await _initializationTcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Initialization timed out after {timeoutMs}ms");
                return false;
            }
        }

        /// <summary>
        /// Initializes the core systems and subsystems.
        /// </summary>
        private async Task InitializeCoreSystems()
        {
            await _resourceManager.InitializeAsync().ConfigureAwait(false);
            await _systemMonitor.StartMonitoringAsync().ConfigureAwait(false);

            if (_config.TelemetryEnabled)
            {
                await _metrics.InitializeAsync().ConfigureAwait(false);
            }
        }
    }
}