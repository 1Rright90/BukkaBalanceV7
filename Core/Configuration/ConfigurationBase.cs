using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using YSBCaptain.Core.Base;

namespace YSBCaptain.Core.Configuration
{
    /// <summary>
    /// Base class for configuration management with thread-safe operations.
    /// </summary>
    public abstract class ConfigurationBase : InitializableBase, IConfiguration
    {
        private readonly ConcurrentDictionary<string, object> _settings;
        private readonly SemaphoreSlim _configLock;
        private bool _disposed;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationBase"/> class.
        /// </summary>
        /// <param name="logger">The logger instance for logging events.</param>
        protected ConfigurationBase(ILogger logger) : base(logger)
        {
            _settings = new ConcurrentDictionary<string, object>();
            _configLock = new SemaphoreSlim(1, 1);
            _disposed = false;
            _logger = logger;
        }

        public string this[string key]
        {
            get => GetValue<string>(key);
            set => SetValue(key, value);
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            ThrowIfDisposed();
            var sections = new List<IConfigurationSection>();
            foreach (var key in _settings.Keys)
            {
                sections.Add(GetSection(key));
            }
            return sections;
        }

        public IChangeToken GetReloadToken()
        {
            // Since we don't support configuration reloading in this implementation,
            // return a dummy token that never triggers
            return new ConfigurationReloadToken();
        }

        public IConfigurationSection GetSection(string key)
        {
            ThrowIfDisposed();
            return new ConfigurationSection(this, key);
        }

        /// <summary>
        /// Gets a configuration value with type conversion.
        /// </summary>
        /// <typeparam name="T">The type of the configuration value.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">The default value if the key does not exist.</param>
        /// <returns>The configuration value, or the default value if not found.</returns>
        public virtual T GetValue<T>(string key, T defaultValue = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (_settings.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typedValue)
                    {
                        return typedValue;
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert configuration value for key {Key} to type {Type}", key, typeof(T));
                    return defaultValue;
                }
            }

            return defaultValue;
        }

        /// <summary>
        /// Sets a configuration value.
        /// </summary>
        /// <typeparam name="T">The type of the configuration value.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The value to set.</param>
        public virtual void SetValue<T>(string key, T value)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }

            _settings.AddOrUpdate(key, value, (_, _) => value);
            _logger.LogDebug("Configuration value updated for key {Key}", key);
        }

        /// <summary>
        /// Saves the configuration asynchronously.
        /// </summary>
        protected virtual async Task SaveConfigurationAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Loads the configuration asynchronously.
        /// </summary>
        protected virtual async Task LoadConfigurationAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await Task.CompletedTask;
        }

        protected override void OnDispose()
        {
            try
            {
                base.OnDispose();
                _configLock.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing configuration");
            }
        }

        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }

    internal class ConfigurationSection : IConfigurationSection
    {
        private readonly IConfiguration _configuration;
        private readonly string _path;

        public ConfigurationSection(IConfiguration configuration, string path)
        {
            _configuration = configuration;
            _path = path;
        }

        public string this[string key]
        {
            get => _configuration[_path + ":" + key];
            set => _configuration[_path + ":" + key] = value;
        }

        public string Key => _path;
        public string Path => _path;
        public string Value
        {
            get => _configuration[_path];
            set => _configuration[_path] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetSection(_path).GetChildren();
        public IChangeToken GetReloadToken() => _configuration.GetReloadToken();
        public IConfigurationSection GetSection(string key) => _configuration.GetSection(_path + ":" + key);
    }
}