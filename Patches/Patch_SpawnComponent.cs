using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Behaviors;
using TaleWorlds.Core;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Harmony patch for SpawnComponent to customize spawning behavior in flag domination mode
    /// Implements performance monitoring and custom spawn frame behavior
    /// </summary>
    [HarmonyPatch(typeof(SpawnComponent))]
    public class Patch_SpawnComponent
    {
        private static readonly PerformanceMonitor _performanceMonitor = new PerformanceMonitor("SpawnSystem");

        /// <summary>
        /// Prefix patch for SetFlagDominationSpawningBehavior to implement custom spawn behavior
        /// Monitors performance and resource usage during spawn behavior changes
        /// </summary>
        /// <returns>False to override original method, true to run original method</returns>
        [HarmonyPatch("SetFlagDominationSpawningBehavior")]
        [HarmonyPrefix]
        public static bool Prefix_SetFlagDominationSpawningBehavior()
        {
            using (_performanceMonitor.MeasureScope())
            {
                try
                {
                    if (Mission.Current != null)
                    {
                        var spawnComponent = Mission.Current.GetMissionBehavior<SpawnComponent>();
                        if (spawnComponent != null)
                        {
                            // Set up custom spawn behaviors
                            using (_performanceMonitor.MeasureScope("SpawnBehaviorSetup"))
                            {
                                spawnComponent.SetNewSpawnFrameBehavior(new FlagDominationSpawnFrameBehavior());
                                spawnComponent.SetNewSpawningBehavior(new YSBCaptainSpawningBehavior());
                                
                                _performanceMonitor.RecordMetric("SpawnBehaviorChanges", 1);
                                Logger.LogInformation("Custom spawn behaviors initialized successfully");
                            }
                        }
                        else
                        {
                            Logger.LogWarning("SpawnComponent not found in current mission");
                            _performanceMonitor.RecordMetric("MissingSpawnComponent", 1);
                        }
                    }
                    else
                    {
                        Logger.LogWarning("No active mission found during spawn behavior setup");
                        _performanceMonitor.RecordMetric("MissingMission", 1);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Critical error in spawn behavior setup: {ex.Message}", ex);
                    _performanceMonitor.RecordError("SpawnBehaviorSetup", ex);
                }
                return false;
            }
        }
    }
}
