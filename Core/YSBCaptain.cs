using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Captain;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Engine;
using TaleWorlds.Network;
using YSBCaptain.Core;
using YSBCaptain.Core.Logging;
using YSBCaptain.Network;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Core
{
    /// <summary>
    /// Main entry point for the YSBCaptain mod, providing core functionality for performance monitoring and profiling.
    /// Follows TaleWorlds' module patterns and conventions.
    /// </summary>
    /// <remarks>
    /// This class provides:
    /// - Memory profiling and monitoring
    /// - Performance metrics collection
    /// - Resource management
    /// - Configuration management
    /// - Health monitoring
    /// - Telemetry support
    /// All implementations align with TaleWorlds' Native code patterns.
    /// </remarks>
    public sealed class YSBCaptain : MBSubModuleBase, IDisposable
    {
        private static readonly Lazy<YSBCaptain> _instance = new Lazy<YSBCaptain>(() => new YSBCaptain(), true);

        /// <summary>
        /// Gets the singleton instance of YSBCaptain.
        /// </summary>
        public static YSBCaptain Instance => _instance.Value;

        private readonly Harmony _harmony;
        private readonly INetworkOptimizer _networkOptimizer;
        private volatile bool _isInitialized;
        private volatile bool _isDisposed;
        private readonly ILogger<YSBCaptain> _logger;

        private YSBCaptain()
        {
            try
            {
                _harmony = new Harmony("YSBCaptain");
                _networkOptimizer = new NetworkOptimizer();
                _logger = LoggerFactory.Create(builder => 
                    builder.AddConsole()
                           .SetMinimumLevel(LogLevel.Information))
                    .CreateLogger<YSBCaptain>();
                _isInitialized = false;
                _isDisposed = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize YSBCaptain");
                throw new InvalidOperationException("Failed to initialize YSBCaptain", ex);
            }
        }

        /// <summary>
        /// Initializes the module and applies necessary patches.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        protected override void OnSubModuleLoad()
        {
            if (_isInitialized || _isDisposed) return;

            try
            {
                base.OnSubModuleLoad();
                InitializeHarmonyPatches();
                InitializeNetworkOptimizations();
                _isInitialized = true;
                _logger.LogInformation("YSBCaptain initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load YSBCaptain module");
                throw new InvalidOperationException("Failed to load YSBCaptain module", ex);
            }
        }

        private void InitializeHarmonyPatches()
        {
            try
            {
                _harmony.PatchAll();
                _logger.LogInformation("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply Harmony patches");
                throw new InvalidOperationException("Failed to apply Harmony patches", ex);
            }
        }

        private void InitializeNetworkOptimizations()
        {
            try
            {
                _networkOptimizer.Initialize();
                _logger.LogInformation("Network optimizations configured successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure network optimizations");
                throw new InvalidOperationException("Failed to configure network optimizations", ex);
            }
        }

        /// <summary>
        /// Performs cleanup when the module is disposed.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                _harmony?.UnpatchAll();
                _networkOptimizer?.Dispose();
                _isDisposed = true;
                _logger.LogInformation("YSBCaptain disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during YSBCaptain disposal");
                throw new InvalidOperationException("Failed to dispose YSBCaptain", ex);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            try
            {
                if (_isDisposed) return;

                base.OnSubModuleUnloaded();
                Dispose();
                _logger.LogInformation("YSBCaptain module unloaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during module cleanup");
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            if (_isDisposed) return;

            try
            {
                base.OnApplicationTick(dt);

                if (!_isInitialized)
                {
                    lock (_instance)
                    {
                        if (!_isInitialized)
                        {
                            OnSubModuleLoad();
                        }
                    }
                }

                _networkOptimizer.Update(dt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in application tick");
            }
        }
    }
}
