using System;
using System.Threading;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Core.Configuration
{
    /// <summary>
    /// Manages game options and configurations, particularly handling bot formation settings.
    /// Provides thread-safe access to multiplayer game settings.
    /// </summary>
    public class MultiplayerOptions
    {
        private readonly object _lock = new object();

        // Instance fields for configuration
        private int _maxNumberOfPlayers;
        private int _numberOfBotsPerFormation;

        private const int AbsoluteMaxPlayers = 100;
        private const int AbsoluteMaxBotsPerFormation = 200;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiplayerOptions"/> class.
        /// </summary>
        public MultiplayerOptions()
        {
            _maxNumberOfPlayers = 30;
            _numberOfBotsPerFormation = 30;
        }

        /// <summary>
        /// Gets or sets the maximum number of players allowed in the game.
        /// Thread-safe access.
        /// </summary>
        public int MaxNumberOfPlayers
        {
            get
            {
                lock (_lock)
                {
                    return _maxNumberOfPlayers;
                }
            }
            set
            {
                if (value <= 0 || value > AbsoluteMaxPlayers)
                {
                    Logger.LogWarning($"Invalid MaxNumberOfPlayers value: {value}. Must be between 1 and {AbsoluteMaxPlayers}");
                    return;
                }

                lock (_lock)
                {
                    _maxNumberOfPlayers = value;
                    Logger.LogInformation($"MaxNumberOfPlayers set to {value}");
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of bots per formation in the game.
        /// Thread-safe access.
        /// </summary>
        public int NumberOfBotsPerFormation
        {
            get
            {
                lock (_lock)
                {
                    return _numberOfBotsPerFormation;
                }
            }
            set
            {
                if (value <= 0)
                {
                    var ex = new ArgumentOutOfRangeException(nameof(value), value, "Bot count must be greater than 0");
                    Logger.LogError($"Invalid bot count: {value}. Must be greater than 0.", ex);
                    throw ex;
                }

                int adjustedValue = Math.Min(value, AbsoluteMaxBotsPerFormation);
                if (adjustedValue != value)
                {
                    Logger.LogWarning($"Bot count {value} exceeds maximum allowed value of {AbsoluteMaxBotsPerFormation}. Adjusting to maximum.");
                }

                lock (_lock)
                {
                    _numberOfBotsPerFormation = adjustedValue;
                    Logger.LogInformation($"Number of bots per formation set to {adjustedValue}");
                }
            }
        }

        /// <summary>
        /// Updates both player count and bots per formation atomically.
        /// </summary>
        /// <param name="playerCount">New max player count</param>
        /// <param name="botsPerFormation">New bots per formation count</param>
        /// <returns>True if both values were updated successfully</returns>
        public bool UpdateGameLimits(int playerCount, int botsPerFormation)
        {
            if (playerCount <= 0 || playerCount > AbsoluteMaxPlayers)
            {
                Logger.LogWarning($"Invalid player count: {playerCount}. Must be between 1 and {AbsoluteMaxPlayers}");
                return false;
            }

            if (botsPerFormation <= 0 || botsPerFormation > AbsoluteMaxBotsPerFormation)
            {
                Logger.LogWarning($"Invalid bot count: {botsPerFormation}. Must be between 1 and {AbsoluteMaxBotsPerFormation}");
                return false;
            }

            lock (_lock)
            {
                try
                {
                    _maxNumberOfPlayers = playerCount;
                    _numberOfBotsPerFormation = botsPerFormation;
                    Logger.LogInformation($"Game limits updated - Players: {playerCount}, Bots per formation: {botsPerFormation}");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to update game limits. Players: {playerCount}, Bots: {botsPerFormation}", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Logs current settings for debugging or informational purposes.
        /// </summary>
        public void LogCurrentSettings()
        {
            lock (_lock)
            {
                Logger.LogInformation($"Current Settings - Max Players: {_maxNumberOfPlayers}, Bots per Formation: {_numberOfBotsPerFormation}");
            }
        }
    }
}
