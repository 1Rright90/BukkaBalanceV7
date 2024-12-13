using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Captain;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.ObjectSystem;
using YSBCaptain.Core;
using YSBCaptain.Core.Logging;
using YSBCaptain.Extensions;
using YSBCaptain.Utilities;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Systems.Spawning
{
    /// <summary>
    /// Provides helper methods for spawning agents and managing spawn-related operations.
    /// </summary>
    /// <remarks>
    /// This class follows TaleWorlds' patterns for agent spawning and handles:
    /// - Bot spawning with proper team and formation assignment
    /// - Spawn position and frame calculations
    /// - Agent build data preparation
    /// - Integration with TaleWorlds' agent system
    /// </remarks>
    public class SpawnHelper
    {
        private readonly Mission _mission;
        private readonly FormationHelper _formationHelper;
        private readonly CoreManager _coreManager;
        private readonly ILogger<SpawnHelper> _logger;

        /// <summary>
        /// Initializes a new instance of the SpawnHelper class.
        /// </summary>
        /// <param name="mission">The current mission instance.</param>
        /// <param name="formationHelper">Helper for formation-related operations.</param>
        /// <param name="coreManager">Core game manager instance.</param>
        /// <param name="logger">Logger instance for logging operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is null.</exception>
        public SpawnHelper(
            Mission mission, 
            FormationHelper formationHelper, 
            CoreManager coreManager,
            ILogger<SpawnHelper> logger)
        {
            _mission = mission ?? throw new ArgumentNullException(nameof(mission));
            _formationHelper = formationHelper ?? throw new ArgumentNullException(nameof(formationHelper));
            _coreManager = coreManager ?? throw new ArgumentNullException(nameof(coreManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the SpawnHelper and its dependencies.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        public void Initialize()
        {
            try
            {
                _logger.LogInformation("SpawnHelper initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SpawnHelper");
                throw new InvalidOperationException("Failed to initialize SpawnHelper", ex);
            }
        }

        /// <summary>
        /// Spawns a bot with the specified parameters.
        /// </summary>
        /// <param name="team">The team the bot will belong to.</param>
        /// <param name="culture">The culture of the bot.</param>
        /// <param name="character">The character template for the bot.</param>
        /// <param name="formation">The formation to assign the bot to (optional).</param>
        /// <param name="position">The spawn position for the bot.</param>
        /// <param name="onSpawnPerkHandler">Handler for applying perks on spawn (optional).</param>
        /// <returns>A tuple containing success status and the spawned agent (if successful).</returns>
        /// <exception cref="InvalidOperationException">Thrown when spawning fails due to invalid state.</exception>
        public (bool Success, Agent Agent) SpawnBot(
            Team team,
            BasicCultureObject culture,
            BasicCharacterObject character,
            Formation formation,
            Vec3 position,
            MPPerkObject.MPOnSpawnPerkHandler onSpawnPerkHandler)
        {
            try
            {
                if (team == null)
                {
                    _logger.LogWarning("Cannot spawn bot: Team is null");
                    return (false, null);
                }

                if (culture == null)
                {
                    _logger.LogWarning("Cannot spawn bot: Culture is null");
                    return (false, null);
                }

                if (character == null)
                {
                    _logger.LogWarning("Cannot spawn bot: Character is null");
                    return (false, null);
                }

                var spawnFrame = DetermineSpawnFrame(formation, position);
                if (!spawnFrame.HasValue)
                {
                    _logger.LogWarning("Cannot spawn bot: Failed to determine spawn frame");
                    return (false, null);
                }

                var buildData = PrepareAgentBuildData(team, culture, character, spawnFrame.Value);
                if (buildData == null)
                {
                    _logger.LogWarning("Cannot spawn bot: Failed to prepare agent build data");
                    return (false, null);
                }

                var agent = _mission.SpawnAgent(buildData);
                if (agent == null)
                {
                    _logger.LogWarning("Cannot spawn bot: Failed to spawn agent");
                    return (false, null);
                }

                if (formation != null)
                {
                    agent.Formation = formation;
                }

                if (onSpawnPerkHandler != null)
                {
                    onSpawnPerkHandler.Invoke(agent);
                }

                _logger.LogInformation($"Successfully spawned bot for team {team.Side}");
                return (true, agent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to spawn bot");
                throw new InvalidOperationException("Failed to spawn bot", ex);
            }
        }

        /// <summary>
        /// Spawns a bot with the specified parameters.
        /// </summary>
        /// <param name="team">The team the bot will belong to.</param>
        /// <param name="culture">The culture of the bot.</param>
        /// <param name="character">The character template for the bot.</param>
        /// <returns>A tuple containing success status and the spawned agent (if successful).</returns>
        public (bool Success, Agent Agent) SpawnBot(
            Team team,
            BasicCultureObject culture,
            BasicCharacterObject character)
        {
            return SpawnBot(team, culture, character, null, Vec3.Zero, null);
        }

        /// <summary>
        /// Spawns a bot with the specified parameters.
        /// </summary>
        /// <param name="team">The team the bot will belong to.</param>
        /// <param name="culture">The culture of the bot.</param>
        /// <param name="character">The character template for the bot.</param>
        /// <param name="formation">The formation to assign the bot to.</param>
        /// <returns>A tuple containing success status and the spawned agent (if successful).</returns>
        public (bool Success, Agent Agent) SpawnBot(
            Team team,
            BasicCultureObject culture,
            BasicCharacterObject character,
            Formation formation)
        {
            return SpawnBot(team, culture, character, formation, Vec3.Zero, null);
        }

        /// <summary>
        /// Spawns a bot with the specified parameters.
        /// </summary>
        /// <param name="team">The team the bot will belong to.</param>
        /// <param name="culture">The culture of the bot.</param>
        /// <param name="character">The character template for the bot.</param>
        /// <param name="position">The spawn position for the bot.</param>
        /// <returns>A tuple containing success status and the spawned agent (if successful).</returns>
        public (bool Success, Agent Agent) SpawnBot(
            Team team,
            BasicCultureObject culture,
            BasicCharacterObject character,
            Vec3 position)
        {
            return SpawnBot(team, culture, character, null, position, null);
        }

        /// <summary>
        /// Determines the formation for the specified team and formation index.
        /// </summary>
        /// <param name="team">The team to determine the formation for.</param>
        /// <param name="formationIndex">The index of the formation.</param>
        /// <returns>The determined formation, or null if failed.</returns>
        private Formation DetermineFormation(Team team, int formationIndex)
        {
            try
            {
                if (team == null)
                {
                    _logger.LogWarning("Cannot determine formation: Team is null");
                    return null;
                }

                if (formationIndex < 0 || formationIndex >= team.FormationCount)
                {
                    _logger.LogWarning($"Invalid formation index {formationIndex} for team {team.Side}");
                    return null;
                }

                var formation = team.GetFormation((FormationClass)formationIndex);
                if (formation == null)
                {
                    _logger.LogWarning($"Formation not found for index {formationIndex} in team {team.Side}");
                    return null;
                }

                formation.SetUnitSpacing(_formationHelper.GetFormationSpacing(formation));
                return formation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining formation");
                return null;
            }
        }

        /// <summary>
        /// Determines the spawn frame for the specified formation and position.
        /// </summary>
        /// <param name="formation">The formation to determine the spawn frame for.</param>
        /// <param name="position">The position to determine the spawn frame for.</param>
        /// <returns>The determined spawn frame, or null if failed.</returns>
        private MatrixFrame DetermineSpawnFrame(Formation formation, Vec3 position)
        {
            try
            {
                if (position != Vec3.Zero)
                {
                    var frame = MatrixFrame.Identity;
                    frame.origin = position;
                    return frame;
                }

                if (formation != null)
                {
                    return _mission.GetSpawnFrame(formation);
                }

                _logger.LogWarning("No position or formation provided for spawn frame");
                return MatrixFrame.Invalid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining spawn frame");
                return MatrixFrame.Invalid;
            }
        }

        /// <summary>
        /// Prepares the agent build data for the specified team, culture, character, and spawn frame.
        /// </summary>
        /// <param name="team">The team the agent will belong to.</param>
        /// <param name="culture">The culture of the agent.</param>
        /// <param name="character">The character template for the agent.</param>
        /// <param name="spawnFrame">The spawn frame for the agent.</param>
        /// <returns>The prepared agent build data, or null if failed.</returns>
        private AgentBuildData PrepareAgentBuildData(
            Team team,
            BasicCultureObject culture,
            BasicCharacterObject character,
            MatrixFrame spawnFrame)
        {
            try
            {
                var buildData = new AgentBuildData(character);
                buildData.Team = team;
                buildData.Banner = team.Banner;
                buildData.IsFemale = character.IsFemale;
                buildData.BodyProperties = GetRandomBodyProperties(character);
                buildData.Equipment = GetRandomEquipment(character, culture);
                buildData.SpawnFrame = spawnFrame;

                return buildData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing agent build data");
                return null;
            }
        }

        /// <summary>
        /// Gets random body properties for the specified character.
        /// </summary>
        /// <param name="character">The character to get random body properties for.</param>
        /// <returns>The random body properties.</returns>
        private BodyProperties GetRandomBodyProperties(BasicCharacterObject character)
        {
            try
            {
                return BodyProperties.GetRandomBodyProperties(
                    character.Race,
                    character.IsFemale,
                    character.Age);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating random body properties");
                throw;
            }
        }

        /// <summary>
        /// Gets random equipment for the specified character and culture.
        /// </summary>
        /// <param name="character">The character to get random equipment for.</param>
        /// <param name="culture">The culture to get random equipment for.</param>
        /// <returns>The random equipment.</returns>
        private Equipment GetRandomEquipment(BasicCharacterObject character, BasicCultureObject culture)
        {
            try
            {
                var equipment = new Equipment();
                foreach (var (index, element) in character.Equipment)
                {
                    equipment[index] = element;
                }
                return equipment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating random equipment");
                throw;
            }
        }

        /// <summary>
        /// Cleans up the SpawnHelper and its dependencies.
        /// </summary>
        public void Cleanup()
        {
            try
            {
                _logger.LogInformation("SpawnHelper cleaned up");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up SpawnHelper");
            }
        }
    }
}
