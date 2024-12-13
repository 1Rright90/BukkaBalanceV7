using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Handles Formation initialization and core functionality
    /// </summary>
    [HarmonyPatch(typeof(Formation))]
    public class Patch_Formation
    {
        private static readonly IResourceManager _resourceManager = UnifiedResourceManager.Instance;
        private static readonly ConcurrentDictionary<int, DateTime> _formationCreationTimes = new ConcurrentDictionary<int, DateTime>();
        private const int MAX_FORMATIONS_PER_INTERVAL = 10;
        private static readonly TimeSpan FORMATION_CREATION_INTERVAL = TimeSpan.FromSeconds(1);

        private static bool ShouldAllowFormationCreation()
        {
            var now = DateTime.UtcNow;
            _formationCreationTimes.Values.RemoveWhere(time => now - time > FORMATION_CREATION_INTERVAL);
            return _formationCreationTimes.Count < MAX_FORMATIONS_PER_INTERVAL;
        }

        [HarmonyPatch(MethodType.Constructor, new[] { typeof(Team), typeof(int) })]
        [HarmonyPrefix]
        public static bool Prefix_Constructor(Formation __instance, Team team, int index, ref bool __result)
        {
            try
            {
                if (team == null)
                {
                    Logger.LogWarning("[Patch_Formation] Formation constructor called with null team");
                    __result = false;
                    return false;
                }

                if (index < 0 || index >= 32)
                {
                    Logger.LogWarning($"[Patch_Formation] Invalid formation index: {index}");
                    __result = false;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("[Patch_Formation] Error in constructor prefix", ex);
                return true;
            }
        }

        [HarmonyPatch(MethodType.Constructor, new[] { typeof(Team), typeof(int) })]
        [HarmonyPostfix]
        public static void Postfix_Constructor(Formation __instance)
        {
            try
            {
                if (!ShouldAllowFormationCreation())
                {
                    Logger.LogWarning($"[Patch_Formation] Formation creation rate limited. Current count: {_formationCreationTimes.Count}");
                    return;
                }

                _formationCreationTimes.TryAdd(__instance.GetHashCode(), DateTime.UtcNow);

                // Initialize required fields
                var type = typeof(Formation);
                var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

                foreach (var field in fields)
                {
                    if (field.Name.Contains("CompressionInfo") && field.GetValue(__instance) == null)
                    {
                        try
                        {
                            var compressionType = field.FieldType;
                            var compressionInstance = Activator.CreateInstance(compressionType);
                            field.SetValue(__instance, compressionInstance);
                            Logger.LogDebug($"[Patch_Formation] Successfully initialized {field.Name}");
                        }
                        catch (Exception fieldEx)
                        {
                            Logger.LogWarning($"[Patch_Formation] Could not initialize {field.Name}: {fieldEx.Message}");
                        }
                    }
                }

                // Apply performance-based adjustments
                var density = _resourceManager.GetMetric("FormationDensity", 1.0f);
                __instance.FormationSpacing = density;

                // Initialize query system if needed
                if (__instance.QuerySystem == null)
                {
                    try
                    {
                        var querySystemField = type.GetField("_querySystem", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (querySystemField != null)
                        {
                            var querySystem = new FormationQuerySystem(__instance);
                            querySystemField.SetValue(__instance, querySystem);
                            Logger.LogDebug("[Patch_Formation] Successfully initialized QuerySystem");
                        }
                    }
                    catch (Exception queryEx)
                    {
                        Logger.LogWarning($"[Patch_Formation] Could not initialize QuerySystem: {queryEx.Message}");
                    }
                }

                Logger.LogDebug($"[Patch_Formation] Formation created - Index: {__instance.Index}, FormationIndex: {__instance.FormationIndex}, Density: {density}");
            }
            catch (Exception ex)
            {
                Logger.LogError("[Patch_Formation] Error in constructor postfix", ex);
            }
        }

        [HarmonyPatch("OnTick")]
        [HarmonyPrefix]
        public static bool Prefix_OnTick(Formation __instance)
        {
            try
            {
                if (!__instance.IsValid())
                {
                    Logger.LogWarning($"[Patch_Formation] Formation {__instance.Index} is invalid, skipping tick");
                    return false;
                }

                var spawnTimer = __instance.GetSpawnTimer();
                if (spawnTimer != null)
                {
                    spawnTimer.Update(Mission.Current.Time);
                }

                // Performance optimization
                if (__instance.CountOfUnits == 0)
                {
                    return false; // Skip processing empty formations
                }

                // Dynamic update rate based on performance
                var updateInterval = _resourceManager.GetMetric("ProcessingDelay", 0.1f);
                if (Mission.Current.CurrentTime % updateInterval < 0.016f)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("[Patch_Formation] Error in OnTick prefix", ex);
                return true;
            }
        }

        [HarmonyPatch("UpdateOrder")]
        [HarmonyPrefix]
        public static bool Prefix_UpdateOrder(Formation __instance)
        {
            try
            {
                if (__instance == null)
                {
                    Logger.LogWarning("[Patch_Formation] Formation instance is null in UpdateOrder");
                    return false;
                }

                // Ensure all required fields are initialized before updating order
                if (__instance.QuerySystem == null)
                {
                    Logger.LogWarning("[Patch_Formation] QuerySystem is null in UpdateOrder");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("[Patch_Formation] Error in UpdateOrder prefix", ex);
                return true;
            }
        }
    }
}
