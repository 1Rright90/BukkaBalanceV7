using System;
using System.Collections.Generic;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core;
using YSBCaptain.Core.Logging;
using YSBCaptain.Extensions;
using YSBCaptain.Gameplay.Systems.Spawning;
using YSBCaptain.Utilities;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Behaviors.Spawning
{
    /// <summary>
    /// Main spawning logic controller that manages bot spawning and formation assignments.
    /// Implements thread-safe operations and resource management following TaleWorlds' patterns.
    /// </summary>
    /// <remarks>
    /// This class:
    /// - Manages bot spawning with configurable limits
    /// - Handles formation assignments
    /// - Implements thread-safe event handling
    /// - Follows TaleWorlds' resource management patterns
    /// - Provides performance monitoring and metrics
    /// </remarks>
    public sealed class MainSpawningLogic
    {
        // Constants aligned with DynamicResourceManager
        private const int MAX_BOTS_PER_SPAWN = 200;
        private const int DEFAULT_BOTS_PER_FORMATION = 30;
        private const int MAX_CONCURRENT_SPAWNS = 5;
        private const int SPAWN_RETRY_DELAY_MS = 100;
        private const int SPAWN_BATCH_SIZE = 10;

        private readonly object _lockObject = new object();
        private readonly YSBCaptainSpawningBehavior _spawningBehavior;
        private readonly ILogger _logger;
        private bool _isInitialized;
        private Mission _currentMission;

        // Thread-safe event handling
        private event Action<MissionPeer> _onPeerSpawned;
        /// <summary>
        /// Event triggered when a peer is spawned.
        /// </summary>
        public event Action<MissionPeer> OnPeerSpawned
        {
            add
            {
                lock (_lockObject)
                {
                    _onPeerSpawned += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onPeerSpawned -= value;
                }
            }
        }

        private event Action<MissionPeer> _onAllAgentsSpawned;
        /// <summary>
        /// Event triggered when all agents for a peer have been spawned.
        /// </summary>
        public event Action<MissionPeer> OnAllAgentsSpawned
        {
            add
            {
                lock (_lockObject)
                {
                    _onAllAgentsSpawned += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onAllAgentsSpawned -= value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the MainSpawningLogic class.
        /// </summary>
        public MainSpawningLogic()
        {
            _spawningBehavior = new YSBCaptainSpawningBehavior();
            _isInitialized = false;
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<MainSpawningLogic>();
        }

        /// <summary>
        /// Thread-safe initialization of the spawning logic.
        /// </summary>
        /// <param name="mission">The mission to initialize spawning for.</param>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        public void Initialize(Mission mission)
        {
            if (mission == null)
            {
                _logger.LogError("Cannot initialize MainSpawningLogic with null mission");
                throw new ArgumentNullException(nameof(mission));
            }

            lock (_lockObject)
            {
                if (_isInitialized)
                {
                    _logger.LogInformation("MainSpawningLogic already initialized");
                    return;
                }

                try
                {
                    _currentMission = mission;
                    _spawningBehavior?.Initialize(mission.GetSpawnComponent());
                    OnPeerSpawned += HandlePeerSpawnedFromVisuals;
                    OnAllAgentsSpawned += HandleAllAgentsFromPeerSpawnedFromVisuals;
                    _isInitialized = true;
                    _logger.LogInformation("MainSpawningLogic initialized successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize MainSpawningLogic");
                    throw new InvalidOperationException("Failed to initialize spawning logic", ex);
                }
            }
        }

        /// <summary>
        /// Safely handles peer spawn events
        /// </summary>
        private void HandlePeerSpawnedFromVisuals(MissionPeer peer)
        {
            if (peer == null)
            {
                _logger.LogInformation("Received null peer in HandlePeerSpawnedFromVisuals");
                return;
            }

            try
            {
                _logger.LogInformation($"Peer {peer.Name} spawned from visuals");

                lock (_lockObject)
                {
                    var handler = _onPeerSpawned;
                    if (handler != null)
                    {
                        handler.Invoke(peer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in HandlePeerSpawnedFromVisuals: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely handles completion of peer agent spawning
        /// </summary>
        private void HandleAllAgentsFromPeerSpawnedFromVisuals(MissionPeer peer)
        {
            if (peer == null)
            {
                _logger.LogInformation("Received null peer in HandleAllAgentsFromPeerSpawnedFromVisuals");
                return;
            }

            try
            {
                _logger.LogInformation($"All agents for peer {peer.Name} spawned from visuals");

                lock (_lockObject)
                {
                    var handler = _onAllAgentsSpawned;
                    if (handler != null)
                    {
                        handler.Invoke(peer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in HandleAllAgentsFromPeerSpawnedFromVisuals: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes the spawn queue with proper error handling
        /// </summary>
        public void ProcessSpawnQueue()
        {
            if (!_isInitialized)
            {
                _logger.LogInformation("Cannot process spawn queue before initialization");
                return;
            }

            try
            {
                SpawnHelper.ProcessSpawnQueue();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing spawn queue: {ex.Message}");
            }
        }

        /// <summary>
        /// Thread-safe retrieval of bots per formation setting
        /// </summary>
        public int GetBotsPerFormation()
        {
            try
            {
                var optionType = TaleWorlds.MountAndBlade.MultiplayerOptions.OptionType.NumberOfBotsPerFormation;
                var botsPerFormation = optionType.GetIntValue(TaleWorlds.MountAndBlade.MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);

                return ValidateBotsPerFormation(botsPerFormation);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error getting bots per formation: {ex.Message}. Using default value.");
                return DEFAULT_BOTS_PER_FORMATION;
            }
        }

        private static int ValidateBotsPerFormation(int value)
        {
            if (value <= 0)
            {
                return DEFAULT_BOTS_PER_FORMATION;
            }

            if (value > MAX_BOTS_PER_SPAWN)
            {
                return MAX_BOTS_PER_SPAWN;
            }

            return value;
        }

        /// <summary>
        /// Cleans up resources and resets the spawning logic.
        /// </summary>
        public void Cleanup()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isInitialized)
                    {
                        OnPeerSpawned -= HandlePeerSpawnedFromVisuals;
                        OnAllAgentsSpawned -= HandleAllAgentsFromPeerSpawnedFromVisuals;
                        _spawningBehavior?.Cleanup();
                        _isInitialized = false;
                        _currentMission = null;
                        _logger.LogInformation("MainSpawningLogic cleaned up successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MainSpawningLogic cleanup");
                throw new InvalidOperationException("Failed to cleanup spawning logic", ex);
            }
        }
    }
}
