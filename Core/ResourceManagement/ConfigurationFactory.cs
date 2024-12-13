using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Configuration;

namespace YSBCaptain.Core.ResourceManagement
{
    /// <summary>
    /// Factory for creating and managing configuration instances in alignment with TaleWorlds' patterns.
    /// </summary>
    /// <remarks>
    /// This factory ensures configuration instances are created with proper logging and validation,
    /// following Mount &amp; Blade II: Bannerlord's configuration management patterns.
    /// </remarks>
    internal static class ConfigurationFactory
    {
        /// <summary>
        /// Creates a new configuration instance with default settings.
        /// </summary>
        /// <returns>A new instance of <see cref="ConfigurationBase"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when configuration creation fails.</exception>
        public static ConfigurationBase Create()
        {
            try
            {
                var logger = LoggerFactory.Create(builder => 
                    builder.AddConsole()
                           .SetMinimumLevel(LogLevel.Information))
                    .CreateLogger<JsonConfiguration>();
                
                return new JsonConfiguration(logger);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create configuration instance.", ex);
            }
        }
    }
}
