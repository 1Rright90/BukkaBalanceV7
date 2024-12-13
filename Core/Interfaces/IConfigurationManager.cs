using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace YSBCaptain.Core.Interfaces
{
    public interface IConfigurationManager : IDisposable
    {
        /// <summary>
        /// Gets the underlying IConfiguration instance.
        /// </summary>
        IConfiguration Configuration { get; }

        /// <summary>
        /// Gets a configuration value by key.
        /// </summary>
        /// <typeparam name="T">Type to convert the value to.</typeparam>
        /// <param name="key">Configuration key.</param>
        /// <param name="defaultValue">Default value if key not found.</param>
        /// <returns>Configuration value or default.</returns>
        T GetValue<T>(string key, T defaultValue = default);

        /// <summary>
        /// Sets a configuration value.
        /// </summary>
        /// <typeparam name="T">Type of value to set.</typeparam>
        /// <param name="key">Configuration key.</param>
        /// <param name="value">Value to set.</param>
        void SetValue<T>(string key, T value);

        /// <summary>
        /// Saves the current configuration to storage.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reloads the configuration from storage.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task ReloadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Occurs when a configuration value changes.
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    }

    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object OldValue { get; }
        public object NewValue { get; }

        public ConfigurationChangedEventArgs(string key, object oldValue, object newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
