using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NetworkMessages.FromServer;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Patch
{
    /// <summary>
    /// Harmony patch for AgentSetFormation to ensure formation indices are within valid range
    /// Follows TaleWorlds' patterns for formation management
    /// </summary>
    [HarmonyPatch(typeof(AgentSetFormation))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[]
    {
        typeof(int),
        typeof(int)
    })]
    public static class Patch_AgentSetFormation
    {
        private const int MAX_FORMATION_INDEX = 9;  // Maximum valid formation index (0-9)

        /// <summary>
        /// Postfix patch to ensure formation indices are within valid range
        /// </summary>
        /// <param name="__instance">The AgentSetFormation instance being constructed</param>
        private static void Postfix(AgentSetFormation __instance)
        {
            try
            {
                // Ensure formation index is within valid range (0-9)
                int validFormationIndex = __instance.FormationIndex % (MAX_FORMATION_INDEX + 1);
                typeof(AgentSetFormation).GetProperty("FormationIndex").SetValue(__instance, validFormationIndex);

                Logger.LogDebug(
                    $"[Patch_AgentSetFormation] Postfix - AgentIndex: {__instance.AgentIndex}, " +
                    $"FormationIndex: {validFormationIndex}"
                );
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Patch_AgentSetFormation] Error in Postfix: {ex.Message}", ex);
            }
        }
    }
}
