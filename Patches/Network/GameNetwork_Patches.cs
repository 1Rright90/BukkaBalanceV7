using System;
using System.Collections.Concurrent;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;
using YSBCaptain.Network;
using YSBCaptain.Network.Compression;
using YSBCaptain.Core.ErrorHandling;
using CompressionInfo = YSBCaptain.Network.Compression.CompressionInfo;
using YSBCaptain.Performance;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Harmony patches for GameNetwork class to handle network packet compression and client connections
    /// Implements performance monitoring and error rate limiting
    /// </summary>
    [HarmonyPatch(typeof(GameNetwork))]
    public static class GameNetwork_Patches
    {
        private static readonly DynamicResourceManager _resourceManager = DynamicResourceManager.Instance;
        private static readonly PerformanceMonitor _performanceMonitor = new PerformanceMonitor("NetworkOperations");
        private const int ERROR_THRESHOLD = 100;

        /// <summary>
        /// Clamps a value between minimum and maximum bounds
        /// </summary>
        private static int ClampValue(int value, int min, int max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        /// <summary>
        /// Postfix patch for ReadCompressedIntFromPacket to ensure values are within valid bounds
        /// </summary>
        [HarmonyPatch("ReadCompressedIntFromPacket")]
        [HarmonyPostfix]
        public static void ReadCompressedIntFromPacket_Postfix(CompressionInfo compressionInfo, ref int __result)
        {
            using (_performanceMonitor.MeasureScope())
            {
                try
                {
                    string key = $"ReadCompressedInt_{__result}";
                    
                    var integerCompression = compressionInfo.GetType().GetNestedType("Integer");
                    var minValueProperty = integerCompression.GetProperty("MinValue");
                    var maxValueProperty = integerCompression.GetProperty("MaxValue");
                    
                    if (__result < (int)minValueProperty.GetValue(compressionInfo) || __result > (int)maxValueProperty.GetValue(compressionInfo))
                    {
                        ErrorRateLimit.HandleError(key, $"Compressed int value out of bounds: {__result} (Min: {(int)minValueProperty.GetValue(compressionInfo)}, Max: {(int)maxValueProperty.GetValue(compressionInfo)})");
                        
                        __result = ClampValue(__result, (int)minValueProperty.GetValue(compressionInfo), (int)maxValueProperty.GetValue(compressionInfo));
                        _performanceMonitor.RecordMetric("ValueClamped", 1);
                    }
                }
                catch (Exception ex)
                {
                    ErrorRateLimit.HandleError("ReadCompressedIntFromPacket", $"Critical error in ReadCompressedIntFromPacket: {ex.Message}", ex);
                    _performanceMonitor.RecordError("ReadCompressedInt", ex);
                }
            }
        }

        /// <summary>
        /// Prefix patch for WriteCompressedIntToPacket to ensure values are within valid bounds
        /// </summary>
        [HarmonyPatch("WriteCompressedIntToPacket")]
        [HarmonyPrefix]
        public static bool WriteCompressedIntToPacket_Prefix(CompressionInfo compressionInfo, int value)
        {
            using (_performanceMonitor.MeasureScope())
            {
                try
                {
                    string key = $"WriteCompressedInt_{value}";
                    
                    var integerCompression = compressionInfo.GetType().GetNestedType("Integer");
                    var minValueProperty = integerCompression.GetProperty("MinValue");
                    var maxValueProperty = integerCompression.GetProperty("MaxValue");
                    
                    if (value < (int)minValueProperty.GetValue(compressionInfo) || value > (int)maxValueProperty.GetValue(compressionInfo))
                    {
                        ErrorRateLimit.HandleError(key, $"Attempted to write compressed int out of bounds: {value} (Min: {(int)minValueProperty.GetValue(compressionInfo)}, Max: {(int)maxValueProperty.GetValue(compressionInfo)})");
                        
                        value = ClampValue(value, (int)minValueProperty.GetValue(compressionInfo), (int)maxValueProperty.GetValue(compressionInfo));
                        _performanceMonitor.RecordMetric("ValueClamped", 1);
                    }
                }
                catch (Exception ex)
                {
                    ErrorRateLimit.HandleError("WriteCompressedIntToPacket", $"Critical error in WriteCompressedIntToPacket: {ex.Message}", ex);
                    _performanceMonitor.RecordError("WriteCompressedInt", ex);
                }
                
                return true;
            }
        }

        /// <summary>
        /// Prefix patch for HandleNewClientConnect to monitor and validate new client connections
        /// </summary>
        [HarmonyPatch(typeof(GameNetwork), "HandleNewClientConnect")]
        [HarmonyPrefix]
        private static void HandleNewClientConnect(NetworkCommunicator networkPeer)
        {
            using (_performanceMonitor.MeasureScope())
            {
                try
                {
                    if (networkPeer == null)
                    {
                        ErrorRateLimit.HandleError("HandleNewClientConnect", "Received null networkPeer in HandleNewClientConnect");
                        return;
                    }

                    // Get network stats
                    var networkStats = networkPeer.GetNetworkStats();
                    if (networkStats != null)
                    {
                        var latency = networkStats.AverageLatency;
                        var clampedLatency = ClampValue(latency, 0, 1000);
                        Logger.LogInformation($"New client connected. Latency: {clampedLatency}ms");
                        _performanceMonitor.RecordMetric("ClientLatency", clampedLatency);
                    }
                    else
                    {
                        ErrorRateLimit.HandleError("HandleNewClientConnect", "Unable to get network stats for new client");
                        _performanceMonitor.RecordMetric("FailedStatsRetrieval", 1);
                    }
                }
                catch (Exception ex)
                {
                    ErrorRateLimit.HandleError("HandleNewClientConnect", $"Critical error in HandleNewClientConnect patch: {ex.Message}", ex);
                    _performanceMonitor.RecordError("ClientConnect", ex);
                }
            }
        }
    }
}
