using System;
using HarmonyLib;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Performance;
using YSBCaptain.Extensions;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Harmony patch for CreateAgent constructor to ensure proper agent initialization and formation assignment
    /// Implements performance monitoring and memory profiling for agent creation
    /// </summary>
    [HarmonyPatch(typeof(CreateAgent))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[]
    {
        typeof(int),
        typeof(BasicCharacterObject),
        typeof(Monster),
        typeof(Equipment),
        typeof(MissionEquipment),
        typeof(BodyProperties),
        typeof(int),
        typeof(bool),
        typeof(int),
        typeof(int),
        typeof(uint),
        typeof(uint),
        typeof(int),
        typeof(Equipment),
        typeof(bool),
        typeof(Vec3),
        typeof(Vec2),
        typeof(NetworkCommunicator)
    })]
    public static class Patch_CreateAgent
    {
        private static readonly System.Reflection.PropertyInfo FormationIndexProperty = 
            typeof(CreateAgent).GetProperty("FormationIndex");
        private static readonly PerformanceMonitor _performanceMonitor = new PerformanceMonitor("AgentCreation");
        private const int MAX_FORMATION_INDEX = 9;

        /// <summary>
        /// Postfix patch to validate and adjust formation index after agent creation
        /// Monitors memory usage and performance metrics during agent creation
        /// </summary>
        /// <param name="__instance">The CreateAgent instance being created</param>
        [HarmonyPostfix]
        private static void Postfix(CreateAgent __instance)
        {
            using (_performanceMonitor.MeasureScope())
            {
                try
                {
                    // Record memory usage for profiling
                    _performanceMonitor.RecordMetric("AgentCreationMemory", GC.GetTotalMemory(false));

                    // Ensure formation index is within valid range (0-9)
                    int currentIndex = __instance.FormationIndex;
                    int newIndex = currentIndex % (MAX_FORMATION_INDEX + 1);
                    
                    if (newIndex != currentIndex)
                    {
                        FormationIndexProperty?.SetValue(__instance, newIndex);
                        Logging.Logger.LogWarning($"[CreateAgent] Formation index adjusted from {currentIndex} to {newIndex} for agent {__instance.AgentIndex}");
                        _performanceMonitor.RecordMetric("FormationIndexAdjustments", 1);
                    }
                    else if (Logging.Logger.IsDebugEnabled)
                    {
                        Logging.Logger.LogDebug($"[CreateAgent] Agent {__instance.AgentIndex} created in formation {newIndex}");
                        _performanceMonitor.RecordMetric("ValidFormationAssignments", 1);
                    }

                    // Monitor agent creation success
                    _performanceMonitor.RecordMetric("AgentsCreated", 1);
                }
                catch (Exception ex)
                {
                    Logging.Logger.LogError($"[CreateAgent] Critical error during agent creation: {ex.Message}", ex);
                    _performanceMonitor.RecordError("AgentCreation", ex);
                }
            }
        }
    }
}
