using System;
using System.Collections.Concurrent;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Gameplay.Systems.Spawning
{
    /// <summary>
    /// Manages formation assignments and tracking for spawned units.
    /// Follows TaleWorlds' patterns for formation management.
    /// </summary>
    /// <remarks>
    /// This class provides:
    /// - Thread-safe formation assignment
    /// - Formation capacity management
    /// - Formation validation and cleanup
    /// - Integration with TaleWorlds' formation system
    /// </remarks>
    public sealed class FormationManager
    {
        private readonly ConcurrentDictionary<Team, ConcurrentBag<int>> _reservedFormationsByTeam;
        private readonly object _formationLock = new object();
        private readonly ILogger _logger;
        private const int MaxBotsPerFormation = 200;

        /// <summary>
        /// Initializes a new instance of the FormationManager class.
        /// </summary>
        public FormationManager()
        {
            _reservedFormationsByTeam = new ConcurrentDictionary<Team, ConcurrentBag<int>>();
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<FormationManager>();
        }

        /// <summary>
        /// Initializes the formation manager.
        /// </summary>
        public void Initialize()
        {
            try
            {
                Clear();
                _logger.LogInformation("FormationManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize FormationManager");
                throw new InvalidOperationException("Failed to initialize FormationManager", ex);
            }
        }

        /// <summary>
        /// Gets an empty formation for the specified team.
        /// </summary>
        /// <param name="team">The team to get a formation for.</param>
        /// <returns>An empty formation if available, null otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when team is null.</exception>
        public Formation GetEmptyFormation(Team team)
        {
            if (team == null)
            {
                _logger.LogError("Attempted to get empty formation for null team");
                throw new ArgumentNullException(nameof(team));
            }

            try
            {
                if (!_reservedFormationsByTeam.TryGetValue(team, out var formations))
                {
                    formations = new ConcurrentBag<int>();
                    _reservedFormationsByTeam.TryAdd(team, formations);
                }

                lock (_formationLock)
                {
                    Formation emptyFormation = null;
                    foreach (var formation in team.FormationsIncludingSpecialAndEmpty)
                    {
                        if (formation.PlayerOwner == null &&
                            !formation.ContainsAgentVisuals &&
                            formation.CountOfUnits == 0 &&
                            !formations.Contains(formation.Index))
                        {
                            emptyFormation = formation;
                            break;
                        }
                    }

                    if (emptyFormation != null)
                    {
                        formations.Add(emptyFormation.Index);
                        _logger.LogDebug($"Reserved formation {emptyFormation.Index} for team {team.Side}");
                        return emptyFormation;
                    }

                    _logger.LogWarning($"No empty formation available for team {team.Side}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting empty formation for team {team.Side}");
                throw new InvalidOperationException("Failed to get empty formation", ex);
            }
        }

        /// <summary>
        /// Updates formation assignments and cleans up invalid formations.
        /// </summary>
        public void UpdateFormations()
        {
            try
            {
                foreach (var kvp in _reservedFormationsByTeam)
                {
                    var team = kvp.Key;
                    var formations = kvp.Value;

                    var invalidFormations = formations
                        .Where(index => !IsFormationValid(team, index))
                        .ToList();

                    foreach (var index in invalidFormations)
                    {
                        if (formations.TryTake(out _))
                        {
                            _logger.LogDebug($"Cleaned up invalid formation {index} for team {team.Side}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating formations");
                throw new InvalidOperationException("Failed to update formations", ex);
            }
        }

        /// <summary>
        /// Clears all formation assignments.
        /// </summary>
        public void Clear()
        {
            try
            {
                foreach (var team in _reservedFormationsByTeam.Keys)
                {
                    var formations = _reservedFormationsByTeam[team];
                    while (formations.TryTake(out _)) { }
                }
                _reservedFormationsByTeam.Clear();
                _logger.LogInformation("Formation assignments cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing formations");
                throw new InvalidOperationException("Failed to clear formations", ex);
            }
        }

        private bool IsFormationValid(Team team, int formationIndex)
        {
            try
            {
                Formation formation = team.FormationsIncludingSpecialAndEmpty
                    .FirstOrDefault(f => f.Index == formationIndex);

                return formation != null &&
                       formation.CountOfUnits <= MaxBotsPerFormation &&
                       !formation.ContainsAgentVisuals;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating formation {formationIndex} for team {team?.Side}");
                return false;
            }
        }
    }
}
