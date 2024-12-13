using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using Module = TaleWorlds.MountAndBlade.Module;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Harmony patch for Module class to handle server shutdown events
    /// Implements telemetry and notification handling for server status changes
    /// </summary>
    [HarmonyPatch(typeof(Module))]
    public class Patch_Module
    {
        private static readonly PerformanceMonitor _performanceMonitor = new PerformanceMonitor("ModuleShutdown");

        /// <summary>
        /// Postfix patch for ShutDownWithDelay to handle server shutdown notifications
        /// Records telemetry data and notifies connected clients
        /// </summary>
        /// <param name="reason">The reason for server shutdown</param>
        [HarmonyPatch("ShutDownWithDelay")]
        [HarmonyPostfix]
        public static void Postfix_ShutDownWithDelay(string reason)
        {
            using (_performanceMonitor.MeasureScope())
            {
                try
                {
                    // Record shutdown event
                    _performanceMonitor.RecordMetric("ServerShutdown", 1);
                    _performanceMonitor.RecordMetric("ShutdownReason", reason?.GetHashCode() ?? 0);

                    // Log and notify
                    Logger.LogWarning($"Server initiating shutdown. Reason: {reason}");
                    Logger.SendAdminNotificationToAll($"Server is shutting down. Reason: {reason}", true);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error during server shutdown notification: {ex.Message}", ex);
                    _performanceMonitor.RecordError("ShutdownNotification", ex);
                }
            }
        }
    }
}
