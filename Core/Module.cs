using System;
using System.Threading.Tasks;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Captain;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Engine;
using TaleWorlds.Network;
using TaleWorlds.MountAndBlade.Multiplayer.Models;
using HarmonyLib;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.ResourceManagement;
using YSBCaptain.Core.Logging;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Core
{
    /// <summary>
    /// Main module class for YSBCaptain, providing integration with Mount &amp; Blade II: Bannerlord.
    /// Follows TaleWorlds' module patterns and conventions.
    /// </summary>
    /// <remarks>
    /// This module provides:
    /// - Memory profiling and monitoring
    /// - Performance metrics collection
    /// - Resource management
    /// - Configuration management
    /// All implementations align with TaleWorlds' Native code patterns.
    /// </remarks>
    public class YSBCaptainSubModule : MBSubModuleBase
    {
        private readonly string _harmonyId = "com.ysbcaptain.module";
        private volatile bool _initialized;
        private readonly ILogger _logger;

        public YSBCaptainSubModule()
        {
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<YSBCaptainSubModule>();
        }

        /// <summary>
        /// Called when the module is loaded. Applies Harmony patches.
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            try
            {
                base.OnSubModuleLoad();

                var harmony = new Harmony(_harmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                _logger.LogInformation("YSBCaptain module patches applied successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply YSBCaptain patches");
                throw new InvalidOperationException("Failed to initialize YSBCaptain module", ex);
            }
        }

        /// <summary>
        /// Called before the initial module screen is set as root.
        /// Initializes core module functionality.
        /// </summary>
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            try
            {
                base.OnBeforeInitialModuleScreenSetAsRoot();

                if (!_initialized)
                {
                    InitializeModule();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnBeforeInitialModuleScreenSetAsRoot");
                InformationManager.DisplayMessage(
                    new InformationMessage(
                        "Error initializing YSBCaptain module. Check the log for details.",
                        Colors.Red));
                throw;
            }
        }

        /// <summary>
        /// Called when a game is started. Initializes game-specific components.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="gameStarterObject">The game starter object.</param>
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            try
            {
                base.OnGameStart(game, gameStarterObject);

                if (gameStarterObject is GameStarter starter)
                {
                    InitializeYSBCaptain(starter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGameStart");
                throw;
            }
        }

        private void InitializeModule()
        {
            try
            {
                // Initialize core systems
                CoreManager.Instance.Initialize();
                AppConfiguration.Instance.Initialize();

                _initialized = true;
                _logger.LogInformation("YSBCaptain module initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize YSBCaptain module");
                throw new InvalidOperationException("Failed to initialize YSBCaptain module", ex);
            }
        }

        private void InitializeYSBCaptain(GameStarter starter)
        {
            try
            {
                // Add behaviors
                starter.AddBehavior(new MemoryMonitoringBehavior(AppConfiguration.Instance));
                _logger.LogInformation("YSBCaptain behaviors registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize YSBCaptain behaviors");
                throw new InvalidOperationException("Failed to initialize YSBCaptain behaviors", ex);
            }
        }

        /// <summary>
        /// Called when the game ends.
        /// </summary>
        /// <param name="game">The game instance.</param>
        public override void OnGameEnd(Game game)
        {
            try
            {
                _logger.LogInformation("Game ending, cleaning up resources");
                // Cleanup code here
                base.OnGameEnd(game);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during game end cleanup");
            }
        }

        /// <summary>
        /// Called when mission behaviors are initialized.
        /// </summary>
        /// <param name="mission">The mission instance.</param>
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            try
            {
                _logger.LogInformation("Initializing mission behaviors");
                base.OnMissionBehaviorInitialize(mission);
                // Additional initialization code here
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing mission behaviors");
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            try
            {
                base.OnApplicationTick(dt);

                // Update core systems
                CoreManager.Instance.Update(dt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in application tick");
                throw;
            }
        }

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            try
            {
                base.OnGameLoaded(game, initializerObject);

                // Initialize game-specific configurations
                AppConfiguration.Instance.Game.Initialize();

                _logger.LogInformation("Game loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGameLoaded");
                throw;
            }
        }

        public override void OnGameInitializationFinished(Game game)
        {
            try
            {
                base.OnGameInitializationFinished(game);

                // Final initialization steps
                CoreManager.Instance.OnGameInitializationFinished(game);

                _logger.LogInformation("Game initialization finished");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGameInitializationFinished");
                throw;
            }
        }
    }
}
