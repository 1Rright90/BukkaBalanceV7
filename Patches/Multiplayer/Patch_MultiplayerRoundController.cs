using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;

namespace YSBCaptain.Patches.Multiplayer
{
    /// <summary>
    /// Harmony patch for MultiplayerRoundController to handle round state transitions and player management
    /// Implements performance monitoring and thread-safe player counting
    /// </summary>
    [HarmonyPatch(typeof(MultiplayerRoundController))]
    public static class Patch_MultiplayerRoundController
    {
        private static readonly DateTime[] LastChecks = new DateTime[16];
        private static readonly object[] TeamCountsLocks = new object[16];
        private static readonly int[] TeamCounts = new int[16];
        private static DateTime? startWaitTime;
        private static readonly PerformanceMonitor _performanceMonitor = new PerformanceMonitor("RoundController");

        // Cached reflection properties
        private static readonly PropertyInfo IsGameModeUsingRoundSystemProperty = 
            typeof(MissionMultiplayerGameModeBase).GetProperty(
                "IsGameModeUsingRoundSystem",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

        private static readonly PropertyInfo ShouldCheckForNewRoundProperty = 
            typeof(MissionMultiplayerGameModeBase).GetProperty(
                "ShouldCheckForNewRound",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

        private static readonly MethodInfo GetRoundEndReasonMethod = 
            typeof(MultiplayerRoundController).GetMethod(
                "GetRoundEndReason",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        private static readonly PropertyInfo IsMatchEndingProperty = 
            typeof(MultiplayerRoundController).GetProperty(
                "IsMatchEnding",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

        static Patch_MultiplayerRoundController()
        {
            for (int i = 0; i < TeamCountsLocks.Length; i++)
            {
                TeamCountsLocks[i] = new object();
            }
        }

        /// <summary>
        /// Prefix patch for CheckForNewRound method to handle round transitions and player counting
        /// Implements thread-safe operations and performance monitoring
        /// </summary>
        /// <param name="__instance">Instance of MultiplayerRoundController</param>
        /// <param name="____gameModeServer">Game mode server instance</param>
        /// <param name="__result">Result of the check</param>
        /// <returns>False to override original method, true to run original method</returns>
        [HarmonyPatch("CheckForNewRound")]
        [HarmonyPrefix]
        public static bool Prefix_CheckForNewRound(
            MultiplayerRoundController __instance,
            MissionMultiplayerGameModeBase ____gameModeServer,
            ref bool __result)
        {
            using (_performanceMonitor.MeasureScope())
            {
                int instanceId = __instance.GetHashCode() % TeamCountsLocks.Length;
                DateTime currentTime = DateTime.UtcNow;

                var lastCheck = LastChecks[instanceId];
                if (currentTime <= lastCheck.AddSeconds(1.0))
                {
                    __result = false;
                    return false;
                }

                if (Interlocked.CompareExchange(
                    ref LastChecks[instanceId],
                    currentTime,
                    lastCheck) != lastCheck)
                {
                    __result = false;
                    return false;
                }

                try
                {
                    if (____gameModeServer == null)
                    {
                        Logger.LogError("Game mode server is null during round check");
                        return false;
                    }

                    bool isUsingRoundSystem = (bool)(IsGameModeUsingRoundSystemProperty?.GetValue(____gameModeServer) ?? false);
                    bool shouldCheckForNewRound = (bool)(ShouldCheckForNewRoundProperty?.GetValue(____gameModeServer) ?? false);

                    if (!isUsingRoundSystem || !shouldCheckForNewRound)
                    {
                        __result = false;
                        return false;
                    }

                    if (GetRoundEndReasonMethod != null)
                    {
                        object roundEndReason = GetRoundEndReasonMethod.Invoke(__instance, null);
                        if (roundEndReason != null)
                        {
                            __result = true;
                            return false;
                        }
                    }

                    var localTeamCounts = new int[TeamCounts.Length];

                    foreach (NetworkCommunicator networkCommunicator in GameNetwork.NetworkPeers)
                    {
                        if (networkCommunicator?.ControlledAgent?.Team != null)
                        {
                            int teamIndex = networkCommunicator.ControlledAgent.Team.TeamIndex;
                            if (teamIndex >= 0 && teamIndex < localTeamCounts.Length)
                            {
                                Interlocked.Increment(ref localTeamCounts[teamIndex]);
                            }
                        }
                    }

                    lock (TeamCountsLocks[instanceId])
                    {
                        Array.Copy(localTeamCounts, TeamCounts, TeamCounts.Length);
                    }

                    if (__instance.CurrentRoundState == MultiplayerRoundState.WaitingForPlayers || 
                        ____gameModeServer.TimerComponent.CheckIfTimerPassed())
                    {
                        if (TeamCounts.Any(count => count > 0))
                        {
                            if (startWaitTime == null)
                            {
                                startWaitTime = DateTime.Now;
                                Logger.LogInformation("Starting wait time for player readiness");
                            }
                        }
                    }

                    if (startWaitTime.HasValue && TeamCounts.All(count => count == 0))
                    {
                        Logger.LogWarning("No players ready. Waiting for players to join teams.");
                        Logger.SendMessageToAll("Waiting for players to join teams...");
                        startWaitTime = null;
                        __result = false;
                        return false;
                    }

                    if (__instance.CurrentRoundState == MultiplayerRoundState.WaitingForPlayers || 
                        ____gameModeServer.TimerComponent.CheckIfTimerPassed())
                    {
                        int minPlayers = MultiplayerOptions.OptionType.MinNumberOfPlayersForMatchStart
                            .GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);

                        if (TeamCounts.Sum() < minPlayers && __instance.RoundCount == 0)
                        {
                            IsMatchEndingProperty?.SetValue(__instance, true);
                            Logger.LogWarning($"Not enough players to start match. Required: {minPlayers}, Current: {TeamCounts.Sum()}");
                            __result = false;
                            return false;
                        }

                        TeamCounts[1] += MultiplayerOptions.OptionType.NumberOfBotsTeam2
                            .GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
                        TeamCounts[0] += MultiplayerOptions.OptionType.NumberOfBotsTeam1
                            .GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);

                        if (TeamCounts[0] >= 1 && TeamCounts[1] >= 1)
                        {
                            startWaitTime = null;
                            Logger.LogInformation($"Starting new round. Team1: {TeamCounts[0]}, Team2: {TeamCounts[1]}");
                            __result = true;
                            return false;
                        }
                    }

                    __result = false;
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Critical error in CheckForNewRound: {ex.Message}", ex);
                    _performanceMonitor.RecordError("CheckForNewRound", ex);
                    __result = false;
                    return false;
                }
            }
        }
    }
}
