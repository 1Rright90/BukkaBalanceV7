using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.Configuration
{
    /// <summary>
    /// Provides application-wide configuration settings with thread-safe singleton access.
    /// </summary>
    public class ApplicationConfig : JsonConfiguration
    {
        private static readonly Lazy<ApplicationConfig> _instance = new Lazy<ApplicationConfig>(() => new ApplicationConfig());
        
        /// <summary>
        /// Gets the singleton instance of the application configuration.
        /// </summary>
        public static ApplicationConfig Instance => _instance.Value;

        private ApplicationConfig() : base("config/app.json") { }

        /// <summary>
        /// Gets or sets the application name.
        /// </summary>
        public string ApplicationName
        {
            get => GetValue<string>("ApplicationName", "YSBCaptain");
            set => SetValue("ApplicationName", value);
        }

        /// <summary>
        /// Gets or sets the application version.
        /// </summary>
        public string Version
        {
            get => GetValue<string>("Version", "1.0.0");
            set => SetValue("Version", value);
        }

        /// <summary>
        /// Gets or sets whether telemetry is enabled.
        /// </summary>
        public bool EnableTelemetry
        {
            get => GetValue<bool>("EnableTelemetry", true);
            set => SetValue("EnableTelemetry", value);
        }

        /// <summary>
        /// Gets or sets the logging level for the application.
        /// </summary>
        public LogLevel LogLevel
        {
            get => GetValue<LogLevel>("LogLevel", LogLevel.Information);
            set => SetValue("LogLevel", value);
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent operations.
        /// </summary>
        public int MaxConcurrentOperations
        {
            get => GetValue<int>("MaxConcurrentOperations", 4);
            set => SetValue("MaxConcurrentOperations", value);
        }

        /// <summary>
        /// Gets or sets the timeout duration for operations.
        /// </summary>
        public TimeSpan OperationTimeout
        {
            get => TimeSpan.FromSeconds(GetValue<double>("OperationTimeoutSeconds", 30));
            set => SetValue("OperationTimeoutSeconds", value.TotalSeconds);
        }

        /// <summary>
        /// Creates and initializes a new instance of the application configuration.
        /// </summary>
        /// <returns>The initialized application configuration instance.</returns>
        public static async Task<ApplicationConfig> CreateAsync()
        {
            await Instance.InitializeAsync().ConfigureAwait(false);
            return Instance;
        }
    }
}
