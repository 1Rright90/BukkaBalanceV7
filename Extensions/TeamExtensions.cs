using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Library;
using YSBCaptain.Core;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Extensions
{
    /// <summary>
    /// Extension methods for Team-related functionality.
    /// Follows the layered architecture and TaleWorlds' patterns for team management.
    /// </summary>
    /// <remarks>
    /// This class follows the layered architecture:
    /// Core → Extensions → Network/Performance → Gameplay → Patches
    /// This component only depends on Core layer as it's part of the Extensions layer.
    /// All implementations align with Mount &amp; Blade II: Bannerlord's runtime requirements.
    /// </remarks>
    public static class TeamExtensions
    {
        private static readonly object _lockObject = new object();
        private static readonly Dictionary<Team, BasicCultureObject> _cultureCache = new();
        
        // All available cultures in the game, following TaleWorlds' naming conventions
        private static readonly string[] AvailableCultures = new[]
        {
            "empire",    // The Empire
            "vlandia",   // The Vlandians
            "sturgia",   // The Sturgians
            "aserai",    // The Aserai
            "khuzait",   // The Khuzaits
            "battania"   // The Battanians
        };

        private static readonly Random _random = new Random();

        /// <summary>
        /// Gets the culture object for the specified team with caching for better performance.
        /// </summary>
        /// <param name="team">The team to get the culture for.</param>
        /// <returns>The BasicCultureObject for the team, or null if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when team is null.</exception>
        /// <remarks>
        /// This method follows TaleWorlds' pattern for culture management:
        /// 1. Check cache first
        /// 2. Try to get from team settings
        /// 3. Fall back to random culture if needed
        /// </remarks>
        public static BasicCultureObject GetCulture(this Team team)
        {
            ArgumentNullException.ThrowIfNull(team, nameof(team));

            lock (_lockObject)
            {
                if (_cultureCache.TryGetValue(team, out var culture))
                {
                    return culture;
                }

                try
                {
                    // Get the culture from game settings or player choice
                    var objectManager = Game.Current?.ObjectManager;
                    if (objectManager == null)
                    {
                        Logger.LogError("[TeamExtensions] ObjectManager is null");
                        return null!;
                    }

                    // Try to get culture from team settings first
                    var selectedCulture = team.Side == BattleSideEnum.Defender
                        ? Mission.Current?.DefenderTeamCulture
                        : Mission.Current?.AttackerTeamCulture;

                    // If no culture is set, use a random one
                    if (selectedCulture == null)
                    {
                        var randomIndex = _random.Next(AvailableCultures.Length);
                        selectedCulture = objectManager.GetObject<BasicCultureObject>(AvailableCultures[randomIndex]);
                    }

                    if (selectedCulture != null)
                    {
                        _cultureCache[team] = selectedCulture;
                        return selectedCulture;
                    }

                    Logger.LogWarning($"[TeamExtensions] Could not find culture for team {team.Side}");
                    return null!;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[TeamExtensions] Error getting culture for team: {ex.Message}");
                    return null!;
                }
            }
        }

        /// <summary>
        /// Gets the average position of all active agents in the team.
        /// </summary>
        /// <param name="team">The team to get the average position for.</param>
        /// <returns>The average position as a Vec3, or null if no active agents.</returns>
        /// <exception cref="ArgumentNullException">Thrown when team is null.</exception>
        public static Vec3? GetTeamAveragePosition(this Team team)
        {
            ArgumentNullException.ThrowIfNull(team, nameof(team));

            try
            {
                var activeAgents = team.ActiveAgents;
                if (activeAgents == null || !activeAgents.Any())
                {
                    return null;
                }

                var sum = activeAgents.Aggregate(
                    Vec3.Zero,
                    (current, agent) => current + agent.Position.AsVec3);

                return sum / activeAgents.Count;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TeamExtensions] Error calculating team average position: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets team statistics including active agents, total health, etc.
        /// </summary>
        public static TeamStats GetTeamStats(this Team team)
        {
            try
            {
                if (team?.ActiveAgents == null)
                    return new TeamStats();

                var stats = new TeamStats
                {
                    ActiveAgentCount = 0,
                    TotalHealth = 0,
                    AverageHealth = 0,
                    MaxHealth = float.MinValue,
                    MinHealth = float.MaxValue,
                    Culture = GetCulture(team)?.StringId ?? "unknown"
                };

                foreach (Agent agent in team.ActiveAgents)
                {
                    if (agent?.IsActive() == true)
                    {
                        stats.ActiveAgentCount++;
                        stats.TotalHealth += agent.Health;
                        stats.MaxHealth = Math.Max(stats.MaxHealth, agent.Health);
                        stats.MinHealth = Math.Min(stats.MinHealth, agent.Health);
                    }
                }

                if (stats.ActiveAgentCount > 0)
                {
                    stats.AverageHealth = stats.TotalHealth / stats.ActiveAgentCount;
                }

                return stats;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[TeamExtensions] Error in GetTeamStats: {ex.Message}");
                return new TeamStats();
            }
        }

        public class TeamStats
        {
            public int ActiveAgentCount { get; set; }
            public float TotalHealth { get; set; }
            public float AverageHealth { get; set; }
            public float MaxHealth { get; set; }
            public float MinHealth { get; set; }
            public string Culture { get; set; } = "unknown";
        }

        /// <summary>
        /// Clears the culture cache when teams are reset or changed
        /// </summary>
        public static void ClearCultureCache()
        {
            lock (_lockObject)
            {
                _cultureCache.Clear();
            }
        }

        /// <summary>
        /// Checks if the team is valid and can be used.
        /// </summary>
        /// <param name="team">The team to check.</param>
        /// <returns>True if the team is valid, false otherwise.</returns>
        public static bool IsValid(this Team team)
        {
            return team != null && Mission.Current != null && Mission.Current.Teams.Contains(team);
        }

        public static bool IsValidBattleSide(this BattleSideEnum side)
        {
            return side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender;
        }

        public static bool IsValidTeam(this Team team)
        {
            return team != null && team.Side.IsValidBattleSide();
        }

        public static bool IsAttacker(this Team team)
        {
            return team != null && team.Side == BattleSideEnum.Attacker;
        }

        public static bool IsDefender(this Team team)
        {
            return team != null && team.Side == BattleSideEnum.Defender;
        }
    }
}
