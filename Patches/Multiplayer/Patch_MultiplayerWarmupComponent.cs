using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Harmony patch for MultiplayerWarmupComponent to manage match start conditions
    /// Follows TaleWorlds' multiplayer patterns for consistent behavior
    /// </summary>
    [HarmonyPatch(typeof(MultiplayerWarmupComponent))]
    public class Patch_MultiplayerWarmupComponent
    {
        private static readonly string LogPrefix = "[YSBCaptain:WarmupComponent]";
        private const int ABSOLUTE_MIN_PLAYERS = 2;
        private const int ABSOLUTE_MAX_PLAYERS = 30;
        
        private static int _minPlayersForMatchStart = ABSOLUTE_MIN_PLAYERS;
        private static int _maxPlayersForMatchStart = ABSOLUTE_MAX_PLAYERS;

        /// <summary>
        /// Updates the match start player requirements within safe bounds
        /// </summary>
        /// <param name="minPlayers">Minimum number of players required</param>
        /// <param name="maxPlayers">Maximum number of players allowed</param>
        public static void UpdateMatchStartRequirements(int minPlayers, int maxPlayers)
        {
            try
            {
                var safeMinPlayers = Math.Max(ABSOLUTE_MIN_PLAYERS, minPlayers);
                var safeMaxPlayers = Math.Min(ABSOLUTE_MAX_PLAYERS, maxPlayers);

                Logger.LogInformation(
                    $"{LogPrefix} Updating Match Start Requirements: " +
                    $"Min Players: {safeMinPlayers}, " +
                    $"Max Players: {safeMaxPlayers}"
                );

                _minPlayersForMatchStart = safeMinPlayers;
                _maxPlayersForMatchStart = safeMaxPlayers;
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Failed to update match requirements: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Calculates the minimum players required based on current configuration
        /// </summary>
        private static int CalculateMinPlayersForMatchStart()
        {
            return _minPlayersForMatchStart;
        }

        /// <summary>
        /// Harmony prefix patch for CanMatchStartAfterWarmup
        /// Validates player counts and team balance before allowing match start
        /// </summary>
        [HarmonyPatch("CanMatchStartAfterWarmup")]
        [HarmonyPrefix]
        public static bool Prefix_CanMatchStartAfterWarmup(ref bool __result)
        {
            try
            {
                // Dynamic team player tracking
                int[] teamPlayerCounts = new int[2];
                
                // Collect synchronized players in valid teams
                foreach (NetworkCommunicator networkCommunicator in GameNetwork.NetworkPeers)
                {
                    MissionPeer component = networkCommunicator.GetComponent<MissionPeer>();
                    bool isValidPlayer = networkCommunicator.IsSynchronized && 
                                    ((component != null) ? component.Team : null) != null && 
                                    component.Team.Side != BattleSideEnum.None;
                    
                    if (isValidPlayer)
                    {
                        teamPlayerCounts[(int)component.Team.Side]++;
                    }
                }

                // Calculate total synchronized players
                int totalSynchronizedPlayers = teamPlayerCounts.Sum();
                
                // Get required player count
                int minPlayersRequired = CalculateMinPlayersForMatchStart();

                // Determine if match can start
                __result = totalSynchronizedPlayers >= minPlayersRequired &&
                          totalSynchronizedPlayers <= _maxPlayersForMatchStart;

                // Log result with appropriate level
                if (__result)
                {
                    Logger.LogInformation(
                        $"{LogPrefix} Match Start Approved: " +
                        $"Total Players: {totalSynchronizedPlayers}, " +
                        $"Min Required: {minPlayersRequired}"
                    );
                }
                else
                {
                    Logger.LogWarning(
                        $"{LogPrefix} Match Start Denied: " +
                        $"Total Players: {totalSynchronizedPlayers}, " +
                        $"Min Required: {minPlayersRequired}"
                    );
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"{LogPrefix} Error in CanMatchStartAfterWarmup: {ex.Message}", ex);
                __result = false;
                return false;
            }
        }
    }
}
