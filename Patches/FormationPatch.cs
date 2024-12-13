using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Harmony patch for Formation constructor to ensure formation indices are within valid range
    /// Uses transpiler to modify IL code for optimal performance
    /// </summary>
    [HarmonyPatch(typeof(Formation))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(Team), typeof(int) })]
    public static class FormationPatch
    {
        private const int MAX_FORMATION_INDEX = 9;  // Maximum valid formation index (0-9)

        /// <summary>
        /// Transpiler to modify the IL code of the Formation constructor
        /// Ensures formation index is within valid range by applying modulo operation
        /// </summary>
        /// <param name="instructions">Original IL instructions</param>
        /// <returns>Modified IL instructions</returns>
        /// <exception cref="InvalidOperationException">Thrown when patch cannot be applied</exception>
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                var codes = new List<CodeInstruction>(instructions);
                bool patched = false;

                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Ldarg_2 && i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Stfld)
                    {
                        var operand = codes[i + 1].operand as FieldInfo;
                        if (operand?.Name == "FormationIndex")
                        {
                            // Replace formation index assignment with modulo operation
                            yield return new CodeInstruction(OpCodes.Ldarg_2);
                            yield return new CodeInstruction(OpCodes.Ldc_I4, MAX_FORMATION_INDEX + 1);
                            yield return new CodeInstruction(OpCodes.Rem);
                            yield return new CodeInstruction(OpCodes.Stfld, operand);
                            patched = true;
                            i++; // Skip the next instruction since we've handled it
                            continue;
                        }
                    }
                    yield return codes[i];
                }

                if (!patched)
                {
                    Logger.LogError("[FormationPatch] Failed to patch Formation constructor - target code not found");
                    throw new InvalidOperationException("Failed to patch Formation constructor");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[FormationPatch] Error in Transpiler", ex);
                throw;
            }
        }
    }
}
