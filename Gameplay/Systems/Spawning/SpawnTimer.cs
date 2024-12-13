using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Systems.Spawning
{
    /// <summary>
    /// Manages timing for spawning operations within a formation.
    /// Provides functionality for controlling spawn intervals and timing.
    /// </summary>
    /// <remarks>
    /// This class provides:
    /// - Formation-specific spawn timing
    /// - Configurable spawn intervals
    /// - Active/inactive state management
    /// - Time validation and error handling
    /// </remarks>
    public sealed class SpawnTimer
    {
        private readonly Formation _formation;
        private float _lastSpawnTime;
        private float _nextSpawnTime;
        private readonly float _spawnInterval;
        private bool _isActive;
        private readonly ILogger<SpawnTimer> _logger;

        /// <summary>
        /// Initializes a new instance of the SpawnTimer class.
        /// </summary>
        /// <param name="formation">The formation this timer is associated with.</param>
        /// <param name="logger">Logger for structured logging.</param>
        /// <param name="spawnInterval">The interval between spawns in seconds.</param>
        /// <exception cref="ArgumentNullException">Thrown when formation or logger is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when spawnInterval is less than or equal to zero.</exception>
        public SpawnTimer(Formation formation, ILogger<SpawnTimer> logger, float spawnInterval = 5.0f)
        {
            if (formation == null)
            {
                throw new ArgumentNullException(nameof(formation));
            }
            
            if (spawnInterval <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnInterval), "Spawn interval must be greater than zero.");
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _formation = formation;
            _spawnInterval = spawnInterval;
            _lastSpawnTime = 0;
            _nextSpawnTime = spawnInterval;
            _isActive = true;

            _logger.LogInformation($"SpawnTimer created for formation {_formation.Index} with interval {spawnInterval}s");
        }

        /// <summary>
        /// Updates the spawn timer based on the current time.
        /// </summary>
        /// <param name="currentTime">The current game time in seconds.</param>
        /// <exception cref="ArgumentException">Thrown when currentTime is not a valid number.</exception>
        public void Update(float currentTime)
        {
            if (!_isActive)
            {
                _logger.LogTrace($"Spawn timer for formation {_formation.Index} is inactive");
                return;
            }

            if (float.IsNaN(currentTime) || float.IsInfinity(currentTime))
            {
                _logger.LogError($"Invalid current time value: {currentTime}");
                throw new ArgumentException("Current time must be a valid number", nameof(currentTime));
            }

            try
            {
                if (currentTime >= _nextSpawnTime)
                {
                    _lastSpawnTime = currentTime;
                    _nextSpawnTime = currentTime + _spawnInterval;
                    _logger.LogDebug($"Spawn timer updated for formation {_formation.Index}. Next spawn at: {_nextSpawnTime}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating spawn timer for formation {_formation.Index}");
                throw new InvalidOperationException("Failed to update spawn timer", ex);
            }
        }

        /// <summary>
        /// Checks if spawning is allowed at the current time.
        /// </summary>
        /// <param name="currentTime">The current game time in seconds.</param>
        /// <returns>True if spawning is allowed, false otherwise.</returns>
        public bool CanSpawn(float currentTime)
        {
            try
            {
                if (!_isActive)
                {
                    _logger.LogTrace($"Cannot spawn: Timer for formation {_formation.Index} is inactive");
                    return false;
                }

                if (float.IsNaN(currentTime) || float.IsInfinity(currentTime))
                {
                    _logger.LogWarning($"Cannot spawn: Invalid current time value: {currentTime}");
                    return false;
                }

                bool canSpawn = currentTime >= _nextSpawnTime;
                _logger.LogTrace($"Formation {_formation.Index} can spawn: {canSpawn}");
                return canSpawn;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking spawn availability for formation {_formation.Index}");
                return false;
            }
        }

        /// <summary>
        /// Resets the spawn timer to start counting from the current time.
        /// </summary>
        /// <param name="currentTime">The current game time in seconds.</param>
        /// <exception cref="ArgumentException">Thrown when currentTime is not a valid number.</exception>
        public void Reset(float currentTime)
        {
            if (float.IsNaN(currentTime) || float.IsInfinity(currentTime))
            {
                var message = $"Cannot reset timer with invalid time value: {currentTime}";
                _logger.LogError(message);
                throw new ArgumentException(message, nameof(currentTime));
            }

            try
            {
                _lastSpawnTime = currentTime;
                _nextSpawnTime = currentTime + _spawnInterval;
                _logger.LogDebug($"Spawn timer reset for formation {_formation.Index}. Next spawn at: {_nextSpawnTime}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting spawn timer for formation {_formation.Index}");
                throw new InvalidOperationException("Failed to reset spawn timer", ex);
            }
        }

        /// <summary>
        /// Deactivates the spawn timer, preventing any spawns.
        /// </summary>
        public void Deactivate()
        {
            try
            {
                _isActive = false;
                _logger.LogInformation($"Spawn timer deactivated for formation {_formation.Index}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deactivating spawn timer for formation {_formation.Index}");
                throw new InvalidOperationException("Failed to deactivate spawn timer", ex);
            }
        }

        /// <summary>
        /// Activates the spawn timer, allowing spawns to occur.
        /// </summary>
        public void Activate()
        {
            try
            {
                _isActive = true;
                _logger.LogInformation($"Spawn timer activated for formation {_formation.Index}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error activating spawn timer for formation {_formation.Index}");
                throw new InvalidOperationException("Failed to activate spawn timer", ex);
            }
        }

        /// <summary>
        /// Gets the time remaining until the next spawn is allowed.
        /// </summary>
        /// <param name="currentTime">The current game time in seconds.</param>
        /// <returns>The time in seconds until the next spawn, or float.MaxValue if spawning is not possible.</returns>
        public float GetTimeUntilNextSpawn(float currentTime)
        {
            try
            {
                if (!_isActive)
                {
                    _logger.LogTrace($"Timer inactive for formation {_formation.Index}, returning max value");
                    return float.MaxValue;
                }

                if (float.IsNaN(currentTime) || float.IsInfinity(currentTime))
                {
                    _logger.LogWarning($"Invalid current time value: {currentTime}, returning max value");
                    return float.MaxValue;
                }

                float timeUntilSpawn = Math.Max(0, _nextSpawnTime - currentTime);
                _logger.LogTrace($"Time until next spawn for formation {_formation.Index}: {timeUntilSpawn}s");
                return timeUntilSpawn;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calculating time until next spawn for formation {_formation.Index}");
                return float.MaxValue;
            }
        }
    }
}
