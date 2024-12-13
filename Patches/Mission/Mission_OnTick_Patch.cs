using System;
using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Mission = TaleWorlds.MountAndBlade.Mission;
using HarmonyLib;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.HealthMonitoring;
using YSBCaptain.Core.Logging;
using YSBCaptain.Core.ResourceManagement;
using YSBCaptain.Performance;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Patch for Mission.OnTick to add performance monitoring and resource management
    /// </summary>
    [HarmonyPatch(typeof(Mission), "OnTick")]
    public static class Mission_OnTick_Patch
    {
        private static readonly Stopwatch _stopwatch = new();
        private static DateTime _lastLogTime = DateTime.MinValue;
        private static readonly AppConfiguration _config = new AppConfiguration();
        private static readonly IHealthMonitor _healthMonitor = HealthMonitor.Instance;
        private static readonly IResourceManager _resourceManager = ResourceManager.Instance;
        private static int _highTickCountWarning = 0;
        private static bool _isInitialized;

        static Mission_OnTick_Patch()
        {
            try
            {
                _config.Initialize();
                _isInitialized = true;
                Logger.LogInformation("Mission_OnTick_Patch initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize Mission_OnTick_Patch", ex);
                _isInitialized = false;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Mission __instance, float dt)
        {
            if (!_isInitialized || __instance == null)
                return;

            try
            {
                _stopwatch.Restart();
                
                // Monitor mission performance
                MonitorMissionPerformance(__instance, dt);
                
                // Check resource usage
                CheckResourceUsage(__instance);
                
                _stopwatch.Stop();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in Mission_OnTick_Patch", ex);
            }
        }

        private static void MonitorMissionPerformance(Mission mission, float dt)
        {
            var elapsedMs = _stopwatch.ElapsedMilliseconds;
            var performanceThresholdMs = (int)_config.Performance.MonitoringInterval.TotalMilliseconds;

            // Check if we're exceeding our target frame time
            if (elapsedMs > performanceThresholdMs)
            {
                _highTickCountWarning++;
                
                if (_highTickCountWarning >= 3) // Three consecutive slow frames
                {
                    var message = $"Performance warning: Mission tick took {elapsedMs}ms (Target: {performanceThresholdMs}ms)";
                    Logger.LogWarning(message);
                    
                    _healthMonitor.ReportIssue(
                        HealthIssueType.HighCpuUsage, 
                        $"High tick time: {elapsedMs}ms"
                    );

                    if (_config.Performance.EnableDetailedMetrics)
                    {
                        // Log additional mission stats
                        var agentCount = mission.Agents.Count;
                        var formationCount = mission.Teams.Sum(t => t.Formations.Count);
                        
                        Logger.LogDebug(
                            $"Mission Stats - Agents: {agentCount}, " +
                            $"Formations: {formationCount}, " +
                            $"Time: {mission.CurrentTime:F2}"
                        );
                    }

                    // Request cleanup if needed
                    if (_config.Resources.EnableAutoCleanup)
                    {
                        _resourceManager.RequestCleanup();
                    }

                    // Reset warning counter after logging
                    _highTickCountWarning = 0;
                }
            }
            else
            {
                _highTickCountWarning = 0;
            }

            // Record performance metrics
            if (_config.Performance.EnableDetailedMetrics && 
                DateTime.Now - _lastLogTime > _config.Performance.MonitoringInterval)
            {
                _healthMonitor.RecordMetric("MissionTickTime", elapsedMs, mission.CurrentTime);
                _lastLogTime = DateTime.Now;
            }
        }

        private static void CheckResourceUsage(Mission mission)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var memoryMB = process.WorkingSet64 / (1024 * 1024);
                var memoryThresholdMB = _config.Performance.MemoryThresholdPercent * process.MaxWorkingSet.ToInt64() / (1024 * 1024 * 100);

                if (memoryMB > memoryThresholdMB)
                {
                    Logger.LogWarning($"High memory usage in mission: {memoryMB}MB / {memoryThresholdMB}MB");
                    
                    // Display in-game warning if critically high
                    if (memoryMB > memoryThresholdMB * 1.2)
                    {
                        InformationManager.DisplayMessage(
                            new InformationMessage(
                                "Warning: Mission is using high memory. Performance may be affected.",
                                Color.FromUint(0xFF0000)
                            )
                        );
                    }

                    // Trigger cleanup
                    if (_config.Resources.EnableAutoCleanup)
                    {
                        _resourceManager.RequestCleanup();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error checking resource usage", ex);
            }
        }
    }
}
