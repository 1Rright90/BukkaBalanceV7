using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Core.Configuration
{
    /// <summary>
    /// JSON-based configuration implementation with thread-safe updates and notifications.
    /// </summary>
    public class JsonConfiguration : ConfigurationBase
    {
        private const string DEFAULT_CONFIG_PATH = "config.json";
        private readonly string _configPath;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly ConcurrentDictionary<string, object> _settings;
        private readonly ReaderWriterLockSlim _fileLock;
        private readonly ILogger _logger;

        public event Action<string, object> ConfigurationChanged;

        public JsonConfiguration(ILogger logger, string configPath = DEFAULT_CONFIG_PATH) : base(logger)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            _settings = new ConcurrentDictionary<string, object>();
            _serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            _fileLock = new ReaderWriterLockSlim();
            _logger = logger;
        }

        protected override async Task LoadConfigurationAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning("Configuration file not found at {Path}", _configPath);
                return;
            }

            try
            {
                _fileLock.EnterReadLock();
                using var fileStream = new FileStream(_configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var config = await JsonSerializer.DeserializeAsync<ConcurrentDictionary<string, object>>(
                    fileStream, _serializerOptions, cancellationToken);

                if (config != null)
                {
                    foreach (var kvp in config)
                    {
                        SetValue(kvp.Key, kvp.Value);
                    }
                }

                _logger.LogInformation("Configuration loaded successfully from {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration from {Path}", _configPath);
                throw;
            }
            finally
            {
                if (_fileLock.IsReadLockHeld)
                {
                    _fileLock.ExitReadLock();
                }
            }
        }

        protected override async Task SaveConfigurationAsync(CancellationToken cancellationToken)
        {
            try
            {
                _fileLock.EnterWriteLock();
                
                var directory = Path.GetDirectoryName(_configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fileStream = new FileStream(_configPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(fileStream, _settings, _serializerOptions, cancellationToken);
                _logger.LogInformation("Configuration saved successfully to {Path}", _configPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration to {Path}", _configPath);
                throw;
            }
            finally
            {
                if (_fileLock.IsWriteLockHeld)
                {
                    _fileLock.ExitWriteLock();
                }
            }
        }

        public override T GetValue<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (_settings.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is JsonElement jsonElement)
                    {
                        return jsonElement.Deserialize<T>(_serializerOptions);
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error converting value for key {key} to type {typeof(T)}");
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public override void SetValue<T>(string key, T value)
        {
            base.SetValue(key, value);
            ConfigurationChanged?.Invoke(key, value);
        }

        /// <summary>
        /// Validates a configuration key and value.
        /// </summary>
        /// <param name="key">Configuration key.</param>
        /// <param name="value">Configuration value.</param>
        /// <returns>True if valid; otherwise, false.</returns>
        public bool ValidateConfiguration(string key, object value)
        {
            return !string.IsNullOrEmpty(key) && value != null;
        }

        /// <summary>
        /// Clears all settings in the configuration.
        /// </summary>
        public void ClearSettings()
        {
            _settings.Clear();
            _logger.LogInformation("Cleared all configuration settings.");
        }

        /// <summary>
        /// Returns a copy of all current configuration settings.
        /// </summary>
        /// <returns>A dictionary of all settings.</returns>
        public ConcurrentDictionary<string, object> GetAllSettings()
        {
            return new ConcurrentDictionary<string, object>(_settings);
        }

        protected override void DisposeManagedResources()
        {
            _fileLock.Dispose();
            base.DisposeManagedResources();
        }

        protected override void OnDispose()
        {
            try
            {
                SaveConfiguration();
                base.OnDispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during configuration disposal");
                throw;
            }
        }
    }
}
