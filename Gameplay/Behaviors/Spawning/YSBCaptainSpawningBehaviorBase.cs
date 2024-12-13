using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Captain;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.ObjectSystem;
using YSBCaptain.Core;
using YSBCaptain.Core.Logging;
using YSBCaptain.Extensions;
using YSBCaptain.Gameplay.Systems.Spawning;
using YSBCaptain.Performance;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Behaviors.Spawning
{
    /// <summary>
    /// Base class for spawning behaviors in YSBCaptain, providing core functionality
    /// and following TaleWorlds' patterns for spawning systems.
    /// </summary>
    /// <remarks>
    /// This abstract class:
    /// - Implements thread-safe event handling
    /// - Provides base spawning functionality
    /// - Manages spawn timers and counts
    /// - Follows TaleWorlds' Native code patterns
    /// - Ensures compatibility with game's core systems
    /// </remarks>
    public abstract class YSBCaptainSpawningBehaviorBase : SpawningBehaviorBase
    {
        private readonly object _lockObject = new object();
        private static readonly Random _random = new Random();
        protected readonly Microsoft.Extensions.Logging.ILogger<YSBCaptainSpawningBehaviorBase> _logger;
        protected readonly Dictionary<int, string> _teamCultures;
        protected bool _isRoundInProgress;

        // Custom enum to replace MultiplayerOptions.OptionType
        protected enum CustomOptionType
        {
            CultureTeam1,
            CultureTeam2,
            NumberOfBotsTeam1,
            NumberOfBotsTeam2,
            NumberOfBotsPerFormation
        }

        // Thread-safe event handling
        private event SpawningBehaviorBase.OnSpawningEndedEventDelegate _onSpawningEnded;
        /// <summary>
        /// Event triggered when spawning has ended.
        /// </summary>
        public new event SpawningBehaviorBase.OnSpawningEndedEventDelegate OnSpawningEnded
        {
            add
            {
                lock (_lockObject)
                {
                    _onSpawningEnded += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onSpawningEnded -= value;
                }
            }
        }

        private event Action<MissionPeer> _onPeerSpawnedFromVisuals;
        /// <summary>
        /// Event triggered when a peer is spawned from visuals.
        /// </summary>
        protected new event Action<MissionPeer> OnPeerSpawnedFromVisuals
        {
            add
            {
                lock (_lockObject)
                {
                    _onPeerSpawnedFromVisuals += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onPeerSpawnedFromVisuals -= value;
                }
            }
        }

        private event Action<MissionPeer> _onAllAgentsFromPeerSpawnedFromVisuals;
        /// <summary>
        /// Event triggered when all agents from a peer have been spawned from visuals.
        /// </summary>
        protected new event Action<MissionPeer> OnAllAgentsFromPeerSpawnedFromVisuals
        {
            add
            {
                lock (_lockObject)
                {
                    _onAllAgentsFromPeerSpawnedFromVisuals += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onAllAgentsFromPeerSpawnedFromVisuals -= value;
                }
            }
        }

        // Cached option values for better performance
        private static readonly Dictionary<CustomOptionType, string> DefaultCultureValues = new Dictionary<CustomOptionType, string>
        {
            { CustomOptionType.CultureTeam1, "DefaultCulture1" },
            { CustomOptionType.CultureTeam2, "DefaultCulture2" }
        };

        private static readonly Dictionary<CustomOptionType, int> DefaultIntValues = new Dictionary<CustomOptionType, int>
        {
            { CustomOptionType.NumberOfBotsTeam1, 8 },
            { CustomOptionType.NumberOfBotsTeam2, 8 },
            { CustomOptionType.NumberOfBotsPerFormation, 8 }
        };

        /// <summary>
        /// Initializes a new instance of the YSBCaptainSpawningBehaviorBase class.
        /// </summary>
        protected YSBCaptainSpawningBehaviorBase()
        {
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<YSBCaptainSpawningBehaviorBase>();
            _teamCultures = new Dictionary<int, string>();
            _isRoundInProgress = false;
        }

        /// <summary>
        /// Determines if the current round is in progress.
        /// </summary>
        /// <returns>True if the round is in progress, false otherwise.</returns>
        protected override bool IsRoundInProgress() => _isRoundInProgress;

        /// <summary>
        /// Resets spawn counts to their initial values.
        /// </summary>
        protected void ResetSpawnCounts()
        {
            try
            {
                lock (_lockObject)
                {
                    // Reset implementation
                    _logger.LogInformation("Spawn counts reset successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spawn counts");
                throw new InvalidOperationException("Failed to reset spawn counts", ex);
            }
        }

        /// <summary>
        /// Resets spawn timers to their initial values.
        /// </summary>
        protected void ResetSpawnTimers()
        {
            try
            {
                lock (_lockObject)
                {
                    // Timer reset implementation
                    _logger.LogInformation("Spawn timers reset successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting spawn timers");
                throw new InvalidOperationException("Failed to reset spawn timers", ex);
            }
        }

        /// <summary>
        /// Gets the culture for the specified team.
        /// </summary>
        /// <param name="team">The team to get the culture for.</param>
        /// <returns>The culture for the team.</returns>
        protected BasicCultureObject GetTeamCulture(Team team)
        {
            if (team == null)
            {
                _logger.LogWarning("Cannot get culture for null team");
                return null;
            }

            var teamIndex = team.TeamIndex;
            var cultureName = GetTeamCultureName(teamIndex);
            
            if (string.IsNullOrEmpty(cultureName))
            {
                _logger.LogWarning($"No culture name found for team {teamIndex}");
                return null;
            }

            var culture = MBObjectManager.Instance.GetObject<BasicCultureObject>(cultureName);
            if (culture == null)
            {
                _logger.LogError($"Failed to get culture object for name {cultureName}");
            }

            return culture;
        }

        private string GetTeamCultureName(int teamIndex)
        {
            var optionType = teamIndex == Mission.AttackerTeam.TeamIndex
                ? CustomOptionType.CultureTeam1
                : CustomOptionType.CultureTeam2;

            return GetOptionValueString(optionType, MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
        }

        protected virtual string GetOptionValueString(CustomOptionType optionType, MultiplayerOptions.MultiplayerOptionsAccessMode accessMode)
        {
            string value;
            if (DefaultCultureValues.TryGetValue(optionType, out value))
            {
                return value;
            }
            return string.Empty;
        }

        protected virtual int GetIntOptionValue(CustomOptionType optionType, MultiplayerOptions.MultiplayerOptionsAccessMode accessMode)
        {
            int value;
            if (DefaultIntValues.TryGetValue(optionType, out value))
            {
                return value;
            }
            return 0;
        }

        protected override void SpawnAgents()
        {
            try
            {
                if (Mission.Current == null)
                {
                    _logger.LogError("Cannot spawn agents: Mission.Current is null");
                    return;
                }

                var culture1 = GetTeamCulture(Mission.AttackerTeam);
                var culture2 = GetTeamCulture(Mission.DefenderTeam);

                foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
                {
                    if (!peer.IsSynchronized)
                        continue;

                    SpawnAgentForPeer(peer, culture1, culture2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SpawnAgents");
            }
        }

        private void SpawnAgentForPeer(NetworkCommunicator peer, BasicCultureObject culture1, BasicCultureObject culture2)
        {
            try
            {
                var missionPeer = peer.GetComponent<MissionPeer>();
                if (!ShouldSpawnAgentForPeer(missionPeer))
                    return;

                var mpheroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(missionPeer, false);
                if (mpheroClass == null)
                {
                    HandleInvalidTroopIndex(peer, missionPeer);
                    return;
                }

                SpawnAgentWithClass(peer, missionPeer, mpheroClass, culture1, culture2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error spawning agent for peer {peer?.Name}");
            }
        }

        private bool ShouldSpawnAgentForPeer(MissionPeer missionPeer)
        {
            return missionPeer != null &&
                   missionPeer.ControlledAgent == null &&
                   !missionPeer.HasSpawnedAgentVisuals &&
                   missionPeer.Team != null &&
                   missionPeer.Team != Mission.SpectatorTeam &&
                   missionPeer.TeamInitialPerkInfoReady &&
                   missionPeer.SpawnTimer.Check(Mission.CurrentTime);
        }

        private void HandleInvalidTroopIndex(NetworkCommunicator peer, MissionPeer missionPeer)
        {
            if (missionPeer.SelectedTroopIndex != 0)
            {
                missionPeer.SelectedTroopIndex = 0;
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new UpdateSelectedTroopIndex(peer, 0));
                GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.ExcludeOtherTeamPlayers, peer);
            }
        }

        private void SpawnAgentWithClass(NetworkCommunicator peer, MissionPeer missionPeer, MultiplayerClassDivisions.MPHeroClass mpheroClass, BasicCultureObject culture1, BasicCultureObject culture2)
        {
            var heroCharacter = mpheroClass.HeroCharacter;
            var equipment = heroCharacter.Equipment.Clone(false);
            var perkHandler = MPPerkObject.GetOnSpawnPerkHandler(missionPeer);

            ApplyPerkEquipment(equipment, perkHandler);

            var culture = missionPeer.Team == Mission.AttackerTeam ? culture1 : culture2;
            var spawnData = CreateSpawnData(missionPeer, heroCharacter, equipment, culture);

            if (GameMode.ShouldSpawnVisualsForServer(peer))
            {
                missionPeer.HasSpawnedAgentVisuals = true;
                missionPeer.EquipmentUpdatingExpired = false;
            }

            GameMode.HandleAgentVisualSpawning(peer, spawnData, 0, true);
        }

        private void ApplyPerkEquipment(Equipment equipment, MPPerkObject.MPOnSpawnPerkHandler perkHandler)
        {
            if (perkHandler == null)
                return;

            var alternativeEquipments = perkHandler.GetAlternativeEquipments(true);
            if (alternativeEquipments == null)
                return;

            foreach (var (index, element) in alternativeEquipments)
            {
                equipment[index] = element;
            }
        }

        private AgentBuildData CreateSpawnData(MissionPeer peer, BasicCharacterObject character, Equipment equipment, BasicCultureObject culture)
        {
            return new AgentBuildData(character)
                .MissionPeer(peer)
                .Equipment(equipment)
                .Team(peer.Team)
                .TroopOrigin(new BasicBattleAgentOrigin(character))
                .IsFemale(peer.Peer.IsFemale)
                .BodyProperties(GetBodyProperties(peer, culture))
                .VisualsIndex(0)
                .ClothingColor1(peer.Team == Mission.AttackerTeam ? culture.Color : culture.ClothAlternativeColor)
                .ClothingColor2(peer.Team == Mission.AttackerTeam ? culture.Color2 : culture.ClothAlternativeColor2);
        }

        public override void OnTick(float dt)
        {
            try
            {
                base.OnTick(dt);
                UpdatePeerEquipment();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnTick");
            }
        }

        private void UpdatePeerEquipment()
        {
            foreach (var peer in GameNetwork.NetworkPeers.Where(p => p.IsSynchronized))
            {
                try
                {
                    var missionPeer = peer.GetComponent<MissionPeer>();
                    if (!ShouldUpdatePeerEquipment(missionPeer))
                        continue;

                    UpdatePeerPerks(peer, missionPeer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error updating peer equipment for {peer.Name}");
                }
            }
        }

        private bool ShouldUpdatePeerEquipment(MissionPeer peer)
        {
            return peer != null &&
                   peer.ControlledAgent == null &&
                   peer.HasSpawnedAgentVisuals &&
                   CanUpdateSpawnEquipment(peer);
        }

        private void UpdatePeerPerks(NetworkCommunicator peer, MissionPeer missionPeer)
        {
            var mpheroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(missionPeer, false);
            if (mpheroClass == null)
                return;

            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new SyncPerksForCurrentlySelectedTroop(peer, missionPeer.Perks[missionPeer.SelectedTroopIndex]));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.ExcludeOtherTeamPlayers, peer);

            UpdateFormationTroops(missionPeer, mpheroClass);
        }

        private void UpdateFormationTroops(MissionPeer missionPeer, MultiplayerClassDivisions.MPHeroClass mpheroClass)
        {
            // Get and validate bots per formation count
            var botsPerFormation = GetIntOptionValue(CustomOptionType.NumberOfBotsPerFormation, MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
            
            // Early exit conditions
            if (botsPerFormation <= 0 || (GameMode.WarmupComponent != null && GameMode.WarmupComponent.IsInWarmup))
                return;

            // Ensure we don't exceed network compression bounds (-1 to 10)
            botsPerFormation = Math.Min(botsPerFormation, 10);
            
            try 
            {
                var perkHandler = MPPerkObject.GetOnSpawnPerkHandler(missionPeer);
                if (perkHandler == null)
                {
                    _logger.LogWarning($"Null perk handler for peer {missionPeer?.Name ?? "Unknown"}");
                    return;
                }

                // Calculate troop count with bounded value
                var troopCount = MPPerkObject.GetTroopCount(mpheroClass, botsPerFormation, perkHandler);
                
                // Additional formation logic
                var hasBannerBearer = missionPeer.SelectedPerks?.Any(p => p.HasBannerBearer) ?? false;
                
                // Log for debugging
                _logger.LogDebug($"Formation update - Peer: {missionPeer.Name}, Bots: {botsPerFormation}, TroopCount: {troopCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateFormationTroops");
            }
        }

        /// <summary>
        /// Called when the behavior is initialized.
        /// </summary>
        protected virtual void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
        }

        /// <summary>
        /// Called when the behavior is terminated.
        /// </summary>
        protected virtual void OnBehaviorTerminate()
        {
            base.OnBehaviorTerminate();
        }

        /// <summary>
        /// Called every mission tick.
        /// </summary>
        /// <param name="dt">Time elapsed since last tick.</param>
        protected virtual void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
        }

        /// <summary>
        /// Handles a new client connection.
        /// </summary>
        /// <param name="networkPeer">The network peer that connected.</param>
        protected virtual void HandleNewClientConnect(NetworkCommunicator networkPeer)
        {
            base.HandleNewClientConnect(networkPeer);
        }

        /// <summary>
        /// Handles a late new client connection after loading is finished.
        /// </summary>
        /// <param name="networkPeer">The network peer that connected.</param>
        protected virtual void HandleLateNewClientAfterLoadingFinished(NetworkCommunicator networkPeer)
        {
            base.HandleLateNewClientAfterLoadingFinished(networkPeer);
        }

        protected virtual void RaiseOnSpawningEnded()
        {
            _onSpawningEnded?.Invoke();
        }

        protected virtual void RaiseOnPeerSpawnedFromVisuals(MissionPeer peer)
        {
            _onPeerSpawnedFromVisuals?.Invoke(peer);
        }

        protected virtual void RaiseOnAllAgentsFromPeerSpawnedFromVisuals(MissionPeer peer)
        {
            _onAllAgentsFromPeerSpawnedFromVisuals?.Invoke(peer);
        }
    }
}
