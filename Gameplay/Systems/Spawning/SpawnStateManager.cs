using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Captain;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.ObjectSystem;
using YSBCaptain.Core.Logging;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Systems.Spawning
{
    /// <summary>
    /// Manages the state of spawning in the game, including round state and spawn timers.
    /// Integrates with TaleWorlds' mission and round controller systems.
    /// </summary>
    /// <remarks>
    /// This class provides:
    /// - Spawn state tracking and management
    /// - Round state synchronization
    /// - Enforced spawn timer management
    /// - Mission integration
    /// </remarks>
    public class SpawnStateManager
    {
        private bool _isSpawningEnabled;
        private bool _isRoundInProgress;
        private Mission _currentMission;
        private MultiplayerRoundController _roundController;
        private readonly ConcurrentDictionary<MissionPeer, Timer> _enforcedSpawnTimers;
        private readonly ILogger<SpawnStateManager> _logger;

        /// <summary>
        /// Gets whether spawning is currently enabled.
        /// </summary>
        public bool IsSpawningEnabled => _isSpawningEnabled;

        /// <summary>
        /// Gets whether a round is currently in progress.
        /// </summary>
        public bool IsRoundInProgress => _isRoundInProgress;

        /// <summary>
        /// Initializes a new instance of the SpawnStateManager class.
        /// </summary>
        public SpawnStateManager(ILogger<SpawnStateManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _enforcedSpawnTimers = new ConcurrentDictionary<MissionPeer, Timer>();
            _logger.LogInformation("SpawnStateManager constructed successfully");
        }

        /// <summary>
        /// Initializes the SpawnStateManager with the specified mission.
        /// </summary>
        /// <param name="mission">The mission to initialize with.</param>
        /// <exception cref="ArgumentNullException">Thrown when mission is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        public void Initialize(Mission mission)
        {
            try
            {
                _currentMission = mission ?? throw new ArgumentNullException(nameof(mission));
                _roundController = mission.GetMissionBehavior<MultiplayerRoundController>();

                if (_roundController != null)
                {
                    _roundController.OnRoundStarted += OnRoundStarted;
                    _roundController.OnRoundEnding += OnRoundEnding;
                    _logger.LogDebug("Round controller event handlers registered");
                }
                else
                {
                    _logger.LogWarning("No round controller found in mission");
                }

                _logger.LogInformation("SpawnStateManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SpawnStateManager");
                throw new InvalidOperationException("Failed to initialize SpawnStateManager", ex);
            }
        }

        /// <summary>
        /// Handles the round started event.
        /// </summary>
        private void OnRoundStarted()
        {
            try
            {
                _isRoundInProgress = true;
                _logger.LogInformation("Round started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling round start");
            }
        }

        /// <summary>
        /// Handles the round ending event.
        /// </summary>
        private void OnRoundEnding()
        {
            try
            {
                _isRoundInProgress = false;
                _logger.LogInformation("Round ending");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling round end");
            }
        }

        /// <summary>
        /// Enables spawning in the game.
        /// </summary>
        public void EnableSpawning()
        {
            try
            {
                _isSpawningEnabled = true;
                _logger.LogInformation("Spawning enabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enabling spawning");
                throw new InvalidOperationException("Failed to enable spawning", ex);
            }
        }

        /// <summary>
        /// Disables spawning in the game.
        /// </summary>
        public void DisableSpawning()
        {
            try
            {
                _isSpawningEnabled = false;
                _logger.LogInformation("Spawning disabled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disabling spawning");
                throw new InvalidOperationException("Failed to disable spawning", ex);
            }
        }

        /// <summary>
        /// Updates all active spawn timers.
        /// </summary>
        /// <param name="dt">The time delta since the last update.</param>
        public void UpdateTimers(float dt)
        {
            if (!_isSpawningEnabled)
            {
                return;
            }

            try
            {
                // Update enforced spawn timers
                foreach (var timer in _enforcedSpawnTimers.Values)
                {
                    if (timer.Check(_currentMission.CurrentTime))
                    {
                        foreach (var key in _enforcedSpawnTimers.Keys)
                        {
                            if (_enforcedSpawnTimers[key].Equals(timer))
                            {
                                if (_enforcedSpawnTimers.TryRemove(key, out _))
                                {
                                    _logger.LogDebug($"Removed expired spawn timer for peer {key.Name}");
                                }
                                break;
                            }
                        }
                    }
                }

                _logger.LogTrace($"Updated {_enforcedSpawnTimers.Count} spawn timers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating spawn timers");
                throw new InvalidOperationException("Failed to update spawn timers", ex);
            }
        }

        /// <summary>
        /// Resets the SpawnStateManager to its initial state.
        /// </summary>
        public void Reset()
        {
            try
            {
                _isSpawningEnabled = false;
                _isRoundInProgress = false;
                _enforcedSpawnTimers.Clear();

                if (_roundController != null)
                {
                    _roundController.OnRoundStarted -= OnRoundStarted;
                    _roundController.OnRoundEnding -= OnRoundEnding;
                    _logger.LogDebug("Round controller event handlers unregistered");
                }

                _logger.LogInformation("SpawnStateManager reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting SpawnStateManager");
                throw new InvalidOperationException("Failed to reset SpawnStateManager", ex);
            }
        }

        /// <summary>
        /// Adds an enforced spawn timer for a mission peer.
        /// </summary>
        /// <param name="peer">The mission peer to add a timer for.</param>
        /// <param name="duration">The duration of the timer in seconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when peer is null.</exception>
        /// <exception cref="ArgumentException">Thrown when duration is negative.</exception>
        public void AddEnforcedSpawnTimer(MissionPeer peer, float duration)
        {
            if (peer == null)
            {
                _logger.LogError("Attempted to add timer for null peer");
                throw new ArgumentNullException(nameof(peer));
            }

            if (duration < 0)
            {
                _logger.LogError($"Invalid timer duration: {duration}");
                throw new ArgumentException("Duration must be non-negative", nameof(duration));
            }

            try
            {
                var timer = new Timer(_currentMission.CurrentTime, duration);
                if (_enforcedSpawnTimers.TryAdd(peer, timer))
                {
                    _logger.LogDebug($"Added spawn timer for peer {peer.Name} with duration {duration}s");
                }
                else
                {
                    _logger.LogWarning($"Failed to add spawn timer for peer {peer.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding spawn timer for peer {peer.Name}");
                throw new InvalidOperationException("Failed to add spawn timer", ex);
            }
        }

        /// <summary>
        /// Handles a new client connecting to the game.
        /// </summary>
        /// <param name="networkPeer">The network peer representing the new client.</param>
        public void HandleNewClient(NetworkCommunicator networkPeer)
        {
            if (networkPeer?.VirtualPlayer?.IsSynced == true)
            {
                _logger.LogInformation($"New client connected: {networkPeer.UserName}");
            }
        }

        /// <summary>
        /// Handles a late-joining client connecting to the game.
        /// </summary>
        /// <param name="networkPeer">The network peer representing the late-joining client.</param>
        public void HandleLateNewClient(NetworkCommunicator networkPeer)
        {
            if (networkPeer?.VirtualPlayer?.IsSynced == true)
            {
                _logger.LogInformation($"Late new client handled: {networkPeer.UserName}");
            }
        }
    }
}
