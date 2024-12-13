using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using Microsoft.Extensions.Logging;

namespace YSBCaptain
{
    /// <summary>
    /// Main entry point for the YSBCaptain module. Handles initialization and Harmony patching.
    /// </summary>
    public class YSBCaptainSubModule : MBSubModuleBase
    {
        private static readonly ILogger _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<YSBCaptainSubModule>();
        private const string HarmonyId = "YSBCaptain";
        private Harmony _harmony;

        /// <summary>
        /// Called when the module is loaded. Initializes Harmony patches.
        /// </summary>
        protected override void OnSubModuleLoad()
        {
            try
            {
                base.OnSubModuleLoad();
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll();
                _logger.LogInformation($"YSBCaptain module loaded successfully. Harmony patches applied.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize YSBCaptain module");
                throw;
            }
        }

        /// <summary>
        /// Called when the module is unloaded. Cleans up Harmony patches.
        /// </summary>
        protected override void OnSubModuleUnloaded()
        {
            try
            {
                _harmony?.UnpatchAll(HarmonyId);
                _logger.LogInformation("YSBCaptain module unloaded successfully. Harmony patches removed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while unloading YSBCaptain module");
            }
            finally
            {
                base.OnSubModuleUnloaded();
            }
        }
    }
}
