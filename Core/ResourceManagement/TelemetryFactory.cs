using Microsoft.Extensions.Logging;
using YSBCaptain.Core.HealthMonitoring;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.ResourceManagement
{
    /// <summary>
    /// Factory for creating and managing telemetry instances in alignment with TaleWorlds' patterns.
    /// </summary>
    /// <remarks>
    /// This factory provides telemetry creation with proper logging and validation,
    /// following Mount &amp; Blade II: Bannerlord's telemetry management patterns.
    /// </remarks>
    internal static class TelemetryFactory
    {
        /// <summary>
        /// Creates a new telemetry instance with default settings.
        /// </summary>
        /// <returns>A new instance of <see cref="ITelemetry"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when telemetry creation fails.</exception>
        public static ITelemetry Create()
        {
            try
            {
                var logger = LoggerFactory.Create(builder => 
                    builder.AddConsole()
                           .SetMinimumLevel(LogLevel.Information))
                    .CreateLogger<Telemetry>();
                
                return new Telemetry(logger);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create telemetry instance.", ex);
            }
        }
    }
}
