using System;
using TaleWorlds.MountAndBlade.Network.Messages;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Network.Validation;

namespace YSBCaptain.Network.Extensions
{
    /// <summary>
    /// Provides extension methods for GameNetworkMessage to enhance safety and validation
    /// Follows TaleWorlds' networking patterns and adds additional safety checks
    /// </summary>
    public static class NetworkMessageExtensions
    {
        /// <summary>
        /// Safely writes an integer value to the network message with range validation
        /// </summary>
        /// <param name="message">The network message to write to</param>
        /// <param name="value">The integer value to write</param>
        /// <param name="min">Minimum allowed value</param>
        /// <param name="max">Maximum allowed value</param>
        /// <param name="errorHandler">Handler for validation errors</param>
        /// <param name="performanceMonitor">Monitor for tracking performance metrics</param>
        /// <param name="context">Context information for error reporting</param>
        /// <returns>True if the value was written as-is, false if it was adjusted to fit the range</returns>
        public static bool WriteIntegerSafe(
            this GameNetworkMessage message,
            int value,
            int min,
            int max,
            IErrorHandler errorHandler,
            IPerformanceMonitor performanceMonitor,
            string context)
        {
            var validator = new NetworkMessageValidator(errorHandler, performanceMonitor);
            
            if (!validator.ValidateIntegerCompression(value, min, max, context))
            {
                // If validation fails, write a safe default value within the range
                var safeValue = Math.Max(min, Math.Min(max, value));
                message.WriteIntToPacket(safeValue, new Integer_compression_info { min_value = min, max_value = max });
                
                errorHandler.HandleWarning("NetworkMessage_ValueAdjusted",
                    $"Value {value} was adjusted to {safeValue} to fit compression range [{min}, {max}]. Context: {context}");
                
                return false;
            }

            message.WriteIntToPacket(value, new Integer_compression_info { min_value = min, max_value = max });
            return true;
        }
    }
}
