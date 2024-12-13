using System;
using System.IO;

namespace YSBCaptain.Core.Logging
{
    /// <summary>
    /// Provides configuration and initialization for the YSBCaptain logging system.
    /// </summary>
    public static class LoggingConfig
    {
        /// <summary>
        /// Gets the default directory for storing log files.
        /// </summary>
        private static readonly string DefaultLogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        /// <summary>
        /// Initializes the logging system with default settings.
        /// Uses Information as the minimum log level and the default log directory.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(DefaultLogDirectory))
                {
                    Directory.CreateDirectory(DefaultLogDirectory);
                }
                Logger.Initialize(DefaultLogDirectory, YSBLogLevel.Information);
            }
            catch (Exception ex)
            {
                // Fallback to console if initialization fails
                Console.WriteLine($"Failed to initialize logging: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Initializes the logging system with custom settings.
        /// </summary>
        /// <param name="logDirectory">The directory where log files will be stored.</param>
        /// <param name="minimumLevel">The minimum level of messages to log.</param>
        public static void Initialize(string logDirectory, YSBLogLevel minimumLevel)
        {
            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }
                Logger.Initialize(logDirectory, minimumLevel);
            }
            catch (Exception ex)
            {
                // Fallback to console if initialization fails
                Console.WriteLine($"Failed to initialize logging: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
