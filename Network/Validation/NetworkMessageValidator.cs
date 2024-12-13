using System;
using TaleWorlds.MountAndBlade.Network.Messages;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Network.Validation
{
    /// <summary>
    /// Validates network messages and compression parameters
    /// Follows TaleWorlds' networking patterns for consistent validation
    /// </summary>
    public class NetworkMessageValidator
    {
        private readonly IErrorHandler _errorHandler;
        private readonly IPerformanceMonitor _performanceMonitor;

        /// <summary>
        /// Initializes a new network message validator
        /// </summary>
        /// <param name="errorHandler">Handler for validation errors</param>
        /// <param name="performanceMonitor">Monitor for tracking validation metrics</param>
        /// <exception cref="ArgumentNullException">Thrown when errorHandler or performanceMonitor is null</exception>
        public NetworkMessageValidator(IErrorHandler errorHandler, IPerformanceMonitor performanceMonitor)
        {
            _errorHandler = errorHandler ?? throw new ArgumentNullException(nameof(errorHandler));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Validates integer compression parameters
        /// </summary>
        /// <param name="value">The value to validate</param>
        /// <param name="min">Minimum allowed value</param>
        /// <param name="max">Maximum allowed value</param>
        /// <param name="context">Context information for error reporting</param>
        /// <returns>True if validation passes, false otherwise</returns>
        public bool ValidateIntegerCompression(int value, int min, int max, string context)
        {
            if (value < min || value > max)
            {
                var error = new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Value {value} is outside the valid compression range. Min: {min}, Max: {max}. Context: {context}"
                );
                
                _errorHandler.HandleError("NetworkMessage_CompressionValidation", error);
                _performanceMonitor.TrackMetric("NetworkMessage_CompressionErrors", 1);
                
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates a game network message
        /// </summary>
        /// <param name="message">The message to validate</param>
        /// <param name="context">Context information for error reporting</param>
        /// <returns>True if validation passes, false otherwise</returns>
        public bool ValidateGameNetworkMessage(GameNetworkMessage message, string context)
        {
            if (message == null)
            {
                var error = new ArgumentNullException(nameof(message), $"Null network message received. Context: {context}");
                _errorHandler.HandleError("NetworkMessage_NullMessage", error);
                _performanceMonitor.TrackMetric("NetworkMessage_NullErrors", 1);
                return false;
            }

            try
            {
                // Add any additional message validation logic here
                return true;
            }
            catch (Exception ex)
            {
                _errorHandler.HandleError("NetworkMessage_ValidationFailed", ex, context);
                _performanceMonitor.TrackMetric("NetworkMessage_ValidationErrors", 1);
                return false;
            }
        }
    }
}
