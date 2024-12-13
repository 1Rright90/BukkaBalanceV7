using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.Configuration
{
    /// <summary>
    /// Provides thread-safe configuration management with auto-save capabilities.
    /// </summary>
    public class ConfigurationProvider : IConfigurationProvider, IDisposable
    {
        private readonly string _configPath;
        private readonly ConcurrentDictionary<string, object> _settings;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly Timer _autoSaveTimer;
        private readonly TimeSpan _autoSaveInterval;
        private readonly object _lock = new object();
        private bool _isDisposed;
        private bool _isDirty;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationProvider"/> class.
        /// </summary>
        /// <param name="configPath">Path to the configuration file.</param>
        /// <param name="logger">Logger instance for recording operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when configPath or logger is null.</exception>
        public ConfigurationProvider(string configPath, Microsoft.Extensions.Logging.ILogger logger)
        {
            _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = new ConcurrentDictionary<string, object>();
            _autoSaveInterval = TimeSpan.FromMinutes(5);
            _autoSaveTimer = new Timer(AutoSave, null, _autoSaveInterval, _autoSaveInterval);

            LoadConfiguration();
            _logger.LogDebug("Configuration provider initialized");
        }

        /// <summary>
        /// Gets a configuration value of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <returns>The configuration value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        /// <exception cref="InvalidCastException">Thrown when value cannot be converted to the specified type.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when key is not found in configuration.</exception>
        public T GetValue<T>(string key)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            if (_settings.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error converting value for key {key}", ex);
                    throw new InvalidCastException($"Unable to convert value for key {key} to type {typeof(T).Name}", ex);
                }
            }

            throw new KeyNotFoundException($"Configuration key {key} not found");
        }

        /// <summary>
        /// Gets a configuration value with a default fallback.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">The default value to return if key is not found.</param>
        /// <returns>The configuration value or default value.</returns>
        public T GetValue<T>(string key, T defaultValue)
        {
            ThrowIfDisposed();

            try
            {
                return TryGetValue(key, out T value) ? value : defaultValue;
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a configuration value.
        /// </summary>
        /// <typeparam name="T">The type of the value to set.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The value to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when key is null or empty.</exception>
        public void SetValue<T>(string key, T value)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            try
            {
                _settings.AddOrUpdate(key, value, (_, __) => value);
                _isDirty = true;
                _logger.LogDebug($"Updated configuration value for key: {key}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error setting value for key {key}", ex);
                throw;
            }
        }

        /// <summary>
        /// Attempts to get a configuration value.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">When this method returns, contains the value if found; otherwise, the default value.</param>
        /// <returns>True if the value was found; otherwise, false.</returns>
        public bool TryGetValue<T>(string key, out T value)
        {
            ThrowIfDisposed();
            value = default;

            try
            {
                if (_settings.TryGetValue(key, out var rawValue))
                {
                    value = (T)Convert.ChangeType(rawValue, typeof(T));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving value for key {key}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets a strongly-typed configuration section.
        /// </summary>
        /// <typeparam name="T">The type of configuration section to retrieve.</typeparam>
        /// <returns>A new instance of the configuration section.</returns>
        public T GetConfiguration<T>() where T : class, new()
        {
            ThrowIfDisposed();
            var key = typeof(T).FullName;
            if (_settings.TryGetValue(key, out var value))
            {
                if (value is T config)
                    return config;
                
                try
                {
                    var json = JsonConvert.SerializeObject(value);
                    return JsonConvert.DeserializeObject<T>(json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error deserializing configuration for type {typeof(T).Name}");
                    throw;
                }
            }
            return new T();
        }

        /// <summary>
        /// Saves a configuration section.
        /// </summary>
        /// <typeparam name="T">The type of configuration section to save.</typeparam>
        /// <param name="configuration">The configuration section to save.</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
        public void SaveConfiguration<T>(T configuration) where T : class
        {
            ThrowIfDisposed();
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var key = typeof(T).FullName;
            _settings.AddOrUpdate(key, configuration, (_, __) => configuration);
            _isDirty = true;
            SaveConfiguration();
        }

        /// <summary>
        /// Reloads the configuration from disk.
        /// </summary>
        public void ReloadConfiguration()
        {
            ThrowIfDisposed();
            LoadConfiguration();
        }

        /// <summary>
        /// Validates a configuration object.
        /// </summary>
        /// <typeparam name="T">The type of configuration to validate.</typeparam>
        /// <param name="configuration">The configuration object to validate.</param>
        /// <returns>True if the configuration is valid; otherwise, false.</returns>
        public bool ValidateConfiguration<T>(T configuration) where T : class
        {
            if (configuration == null)
                return false;

            try
            {
                var json = JsonConvert.SerializeObject(configuration);
                JsonConvert.DeserializeObject<T>(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Saves the current configuration to disk.
        /// </summary>
        public void Save()
        {
            ThrowIfDisposed();

            if (!_isDirty) return;

            lock (_lock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                    File.WriteAllText(_configPath, json);
                    
                    _isDirty = false;
                    _logger.LogInformation("Configuration saved successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error saving configuration", ex);
                    throw;
                }
            }
        }

        private void LoadConfiguration()
        {
            if (!File.Exists(_configPath))
            {
                _logger.LogWarning($"Configuration file not found at {_configPath}");
                return;
            }

            try
            {
                lock (_lock)
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonConvert.DeserializeObject<ConcurrentDictionary<string, object>>(json);
                    if (config == null)
                    {
                        _logger.LogWarning("Configuration file is empty or invalid");
                        return;
                    }

                    _settings.Clear();
                    foreach (var kvp in config)
                    {
                        _settings.TryAdd(kvp.Key, kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error loading configuration from {_configPath}");
                throw;
            }
        }

        private void AutoSave(object state)
        {
            try
            {
                if (_isDirty)
                {
                    Save();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-save");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ConfigurationProvider));
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                if (_isDirty)
                {
                    Save();
                }
                _autoSaveTimer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }
}
