using System;
using System.Collections.Concurrent;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.Logging;
using YSBCaptain.Extensions;
using YSBCaptain.Network;
using YSBCaptain.Performance;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Handles Formation leash and movement restrictions.
    /// Instance-based implementation to avoid static mutable state.
    /// </summary>
    public class FormationLeashPatch : IDisposable
    {
        private readonly IDynamicResourceManager _resourceManager;
        private readonly Microsoft.Extensions.Logging.ILogger<FormationLeashPatch> _logger;
        private readonly ConcurrentDictionary<Formation, float> _formationDistances;

        // Configuration
        private double _baseLeashDistance;
        private double _baseCheckInterval;
        private double _timeSinceLastCheck;
        private DateTime _lastPerformanceCheck;

        private const int PERFORMANCE_CHECK_INTERVAL_MS = 5000;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormationLeashPatch"/> class.
        /// </summary>
        public FormationLeashPatch(IDynamicResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _formationDistances = new ConcurrentDictionary<Formation, float>();
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<FormationLeashPatch>();

            _baseLeashDistance = 100.0f;
            _baseCheckInterval = 0.1f;
            _timeSinceLastCheck = 0.0f;
            _lastPerformanceCheck = DateTime.UtcNow;
        }

        [HarmonyPatch(typeof(Formation), "UpdateMovement")]
        [HarmonyPrefix]
        public bool Prefix_UpdateMovement(Formation __instance, float dt)
        {
            try
            {
                _timeSinceLastCheck += dt;

                // Dynamic interval based on performance
                var checkInterval = _resourceManager.GetAdjustedProcessingDelay(_baseCheckInterval);
                if (_timeSinceLastCheck < checkInterval)
                {
                    return true;
                }

                _timeSinceLastCheck = 0.0f;

                // Skip if formation is not AI controlled or has no player owner
                if (!__instance.IsAIControlled || __instance.PlayerOwner == null)
                {
                    return true;
                }

                var currentTime = DateTime.UtcNow;
                if ((currentTime - _lastPerformanceCheck).TotalMilliseconds >= PERFORMANCE_CHECK_INTERVAL_MS)
                {
                    _lastPerformanceCheck = currentTime;
                    AdjustLeashDistanceBasedOnPerformance();
                }

                // Check and update formation distance
                UpdateFormationDistance(__instance);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("[FormationLeashPatch] Error in UpdateMovement prefix", ex);
                return true;
            }
        }

        private void AdjustLeashDistanceBasedOnPerformance()
        {
            try
            {
                var performanceMultiplier = _resourceManager.GetPerformanceMultiplier();
                var newDistance = Math.Max(100.0f, 9999.0f * performanceMultiplier);
                _baseLeashDistance = newDistance;
                _logger.LogDebug($"[FormationLeashPatch] Adjusted leash distance to {newDistance:F1} based on performance");
            }
            catch (Exception ex)
            {
                _logger.LogError("[FormationLeashPatch] Error adjusting leash distance", ex);
            }
        }

        private void UpdateFormationDistance(Formation formation)
        {
            try
            {
                if (formation?.PlayerOwner?.Team == null || formation.PlayerOwner.Character == null)
                {
                    return;
                }

                var playerPosition = formation.PlayerOwner.Position;
                var formationPosition = formation.OrderPosition.AsVec2;
                var distance = formationPosition.Distance(playerPosition.AsVec2);

                _formationDistances.AddOrUpdate(formation, distance, (_, __) => distance);

                if (distance > _baseLeashDistance)
                {
                    var newPosition = playerPosition + (formation.OrderPosition - playerPosition).NormalizedCopy() * (float)_baseLeashDistance;
                    formation.SetMovementOrder(newPosition);
                    _logger.LogDebug($"[FormationLeashPatch] Formation {formation.Index} pulled back. Distance: {distance:F1}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[FormationLeashPatch] Error updating formation distance", ex);
            }
        }

        /// <summary>
        /// Disposes the resources used by the patch.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _formationDistances.Clear();
            _disposed = true;
            _logger.LogInformation("[FormationLeashPatch] Disposed resources.");
        }
    }

    /// <summary>
    /// Factory for managing FormationLeashPatch instances.
    /// </summary>
    public class FormationLeashPatchFactory
    {
        private readonly IDynamicResourceManager _resourceManager;
        private readonly Microsoft.Extensions.Logging.ILogger<FormationLeashPatchFactory> _logger;

        public FormationLeashPatchFactory(IDynamicResourceManager resourceManager)
        {
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<FormationLeashPatchFactory>();
        }

        public FormationLeashPatch Create()
        {
            return new FormationLeashPatch(_resourceManager);
        }
    }
}