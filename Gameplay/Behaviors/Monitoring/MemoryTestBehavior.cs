using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.HealthMonitoring;
using YSBCaptain.Core.Logging;
using YSBCaptain.Core.ResourceManagement;
using YSBCaptain.Performance;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Behaviors.Monitoring
{
    /// <summary>
    /// Test behavior for memory allocation and cleanup monitoring.
    /// Used to validate memory management and resource cleanup mechanisms.
    /// </summary>
    /// <remarks>
    /// This behavior:
    /// - Simulates memory allocations under controlled conditions
    /// - Tests resource cleanup mechanisms
    /// - Monitors memory pressure and cleanup effectiveness
    /// - Provides detailed metrics for memory management analysis
    /// </remarks>
    public class MemoryTestBehavior : MissionBehavior
    {
        private readonly WeakReference<List<object>> _testObjects;
        private readonly IResourceManager _resourceManager;
        private readonly IHealthMonitor _healthMonitor;
        private readonly AppConfiguration _config;
        private readonly ILogger _logger;
        private float _cleanupTimer;
        private bool _isActive;
        private int _allocationCount;
        private readonly int _maxAllocations;

        /// <summary>
        /// Initializes a new instance of the MemoryTestBehavior class.
        /// </summary>
        /// <param name="config">Application configuration containing test settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
        public MemoryTestBehavior(AppConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _testObjects = new WeakReference<List<object>>(new List<object>());
            _resourceManager = ResourceManager.Instance;
            _healthMonitor = HealthMonitor.Instance;
            _cleanupTimer = 0f;
            _isActive = true;
            _allocationCount = 0;
            _maxAllocations = config.Resources.MaxConcurrentOperations * 100;
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<MemoryTestBehavior>();
        }

        /// <summary>
        /// Gets the behavior type for mission registration.
        /// </summary>
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        /// <summary>
        /// Initializes the behavior when added to a mission.
        /// </summary>
        public override void OnBehaviorInitialize()
        {
            try
            {
                base.OnBehaviorInitialize();
                
                if (Mission.Current != null)
                {
                    _logger.LogInformation($"Memory test behavior initialized for mission: {Mission.Current.Name}");
                    _healthMonitor.RecordMetric("MemoryTestInit", 1, Mission.Current.CurrentTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing memory test behavior");
                throw new InvalidOperationException("Failed to initialize memory test behavior", ex);
            }
        }

        /// <summary>
        /// Called every mission tick to perform memory tests.
        /// </summary>
        /// <param name="dt">Time elapsed since last tick in seconds.</param>
        public override void OnMissionTick(float dt)
        {
            if (!_isActive || Mission.Current == null)
                return;

            try
            {
                _cleanupTimer += dt;

                if (_allocationCount < _maxAllocations && 
                    _config.Performance.EnableDetailedMetrics)
                {
                    AllocateTestObjects();
                }

                if (_cleanupTimer >= _config.Resources.ResourceCheckInterval.TotalSeconds)
                {
                    PerformCleanup();
                    _cleanupTimer = 0f;
                }

                if (_config.Resources.EnableAutoCleanup)
                {
                    CheckMemoryPressure();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in memory test behavior tick");
                throw new InvalidOperationException("Failed to process memory test tick", ex);
            }
        }

        /// <summary>
        /// Allocates test objects to simulate memory pressure.
        /// </summary>
        private void AllocateTestObjects()
        {
            try
            {
                if (_testObjects.TryGetTarget(out var objects))
                {
                    var testObject = new byte[1024 * 1024]; // 1MB allocation
                    objects.Add(testObject);
                    _allocationCount++;

                    if (_config.Performance.EnableDetailedMetrics)
                    {
                        _logger.LogDebug($"Allocated test object {_allocationCount}/{_maxAllocations}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error allocating test objects");
                throw new InvalidOperationException("Failed to allocate test objects", ex);
            }
        }

        /// <summary>
        /// Performs cleanup of allocated test objects.
        /// </summary>
        private void PerformCleanup()
        {
            try
            {
                if (_testObjects.TryGetTarget(out var objects))
                {
                    var count = objects.Count;
                    objects.Clear();
                    _allocationCount = 0;

                    _logger.LogInformation($"Cleaned up {count} test objects");
                    _healthMonitor.RecordMetric("MemoryTestCleanup", count, Mission.Current?.CurrentTime ?? 0f);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing cleanup");
                throw new InvalidOperationException("Failed to perform cleanup", ex);
            }
        }

        /// <summary>
        /// Checks current memory pressure and triggers cleanup if needed.
        /// </summary>
        private void CheckMemoryPressure()
        {
            try
            {
                var memoryInfo = GC.GetGCMemoryInfo();
                var pressurePercent = (double)memoryInfo.MemoryLoadBytes / memoryInfo.TotalAvailableMemoryBytes * 100;

                if (pressurePercent > _config.Resources.MemoryPressureThreshold)
                {
                    _logger.LogWarning($"High memory pressure detected: {pressurePercent:F2}%");
                    PerformCleanup();
                    _resourceManager.RequestCleanup();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking memory pressure");
                throw new InvalidOperationException("Failed to check memory pressure", ex);
            }
        }

        /// <summary>
        /// Called when the behavior is being destroyed.
        /// </summary>
        public override void OnRemoveBehavior()
        {
            try
            {
                base.OnRemoveBehavior();
                PerformCleanup();
                _isActive = false;
                _logger.LogInformation("Memory test behavior removed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing memory test behavior");
                throw;
            }
        }

        public override void OnMissionModeChange(MissionMode oldMode, MissionMode newMode)
        {
            try
            {
                base.OnMissionModeChange(oldMode, newMode);

                // Adjust behavior based on mission mode
                switch (newMode)
                {
                    case MissionMode.Battle:
                    case MissionMode.Deployment:
                        _isActive = true;
                        break;
                    case MissionMode.Conversation:
                    case MissionMode.CutScene:
                        _isActive = false;
                        PerformCleanup();
                        break;
                }

                _logger.LogInformation($"Memory test behavior adjusted for mode change: {oldMode} -> {newMode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling mission mode change");
                throw;
            }
        }
    }
}
