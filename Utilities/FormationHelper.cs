using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Captain.Core;
using YSBCaptain.Core.Configuration;

namespace YSBCaptain.Utilities
{
    /// <summary>
    /// Provides helper methods for managing formation-related calculations and caching.
    /// </summary>
    public sealed class FormationHelper : IDisposable
    {
        private readonly ILogger<FormationHelper> _logger;
        private readonly Mission _mission;
        private readonly AppConfiguration _config;
        private readonly ConcurrentDictionary<int, float> _spacingCache;
        private readonly ConcurrentDictionary<int, Vec3> _centerCache;
        private readonly TaleWorlds.Core.Timer _cacheCleanupTimer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormationHelper"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <param name="mission">The mission instance.</param>
        /// <param name="config">The application configuration.</param>
        public FormationHelper(ILogger<FormationHelper> logger, Mission mission, AppConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mission = mission ?? throw new ArgumentNullException(nameof(mission));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _spacingCache = new ConcurrentDictionary<int, float>();
            _centerCache = new ConcurrentDictionary<int, Vec3>();
            
            var cleanupInterval = (int)_config.Resources.ResourceCheckInterval.TotalMilliseconds;
            _cacheCleanupTimer = new TaleWorlds.Core.Timer(
                CleanupCache, 
                null, 
                cleanupInterval, 
                cleanupInterval
            );
        }

        /// <summary>
        /// Gets the optimal spacing for a formation based on its composition and current conditions.
        /// </summary>
        /// <param name="formation">The formation to calculate spacing for.</param>
        /// <returns>The calculated optimal spacing value.</returns>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        public float GetFormationSpacing(Formation formation)
        {
            if (formation == null)
                throw new ArgumentNullException(nameof(formation));

            try
            {
                return _spacingCache.GetOrAdd(formation.Index, index =>
                {
                    var spacing = CalculateOptimalSpacing(formation);
                    
                    // Apply configuration-based adjustments
                    spacing *= _config.Game.FormationSpacing;
                    
                    // Log if detailed metrics are enabled
                    if (_config.Performance.EnableDetailedMetrics)
                    {
                        _logger.LogDebug($"Formation {index} spacing calculated: {spacing:F2}");
                    }
                    
                    return spacing;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calculating formation spacing", ex);
                return _config.Game.FormationSpacing;
            }
        }

        private float CalculateOptimalSpacing(Formation formation)
        {
            try
            {
                var unitCount = formation.CountOfUnits;
                if (unitCount == 0)
                    return _config.Game.FormationSpacing;

                // Calculate based on unit types
                var mountedCount = formation.GetCountOfUnitsWithCondition(agent => agent.HasMount);
                var infantryCount = unitCount - mountedCount;

                // Base spacing on unit composition
                var baseSpacing = mountedCount > infantryCount ? 2.5f : 1.0f;

                // Adjust for formation type
                baseSpacing = formation.FormationIndex switch
                {
                    (int)FormationClass.Infantry => baseSpacing * 1.0f,
                    (int)FormationClass.Ranged => baseSpacing * 1.2f,
                    (int)FormationClass.Cavalry => baseSpacing * 2.0f,
                    (int)FormationClass.HorseArcher => baseSpacing * 2.5f,
                    _ => baseSpacing
                };

                // Adjust based on map size if we have access to the scene
                if (Mission.Current?.Scene != null)
                {
                    var bounds = Mission.Current.Scene.GetBoundingBox();
                    var mapSize = Math.Max(bounds.Max.x - bounds.Min.x, bounds.Max.y - bounds.Min.y);
                    baseSpacing *= Math.Min(1.5f, mapSize / 1000f);
                }

                return baseSpacing;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating optimal spacing", ex);
                return _config.Game.FormationSpacing;
            }
        }

        /// <summary>
        /// Gets the center position of a formation, taking into account unit weights and positions.
        /// </summary>
        /// <param name="formation">The formation to calculate the center for.</param>
        /// <returns>The calculated center position as a Vec3.</returns>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        public Vec3 GetFormationCenter(Formation formation)
        {
            if (formation == null)
                throw new ArgumentNullException(nameof(formation));

            try
            {
                return _centerCache.GetOrAdd(formation.Index, _ =>
                {
                    var center = CalculateFormationCenter(formation);
                    
                    if (_config.Performance.EnableDetailedMetrics)
                    {
                        _logger.LogDebug($"Formation {formation.Index} center calculated: {center}");
                    }
                    
                    return center;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating formation center", ex);
                return formation.OrderPosition;
            }
        }

        private Vec3 CalculateFormationCenter(Formation formation)
        {
            try
            {
                if (formation.CountOfUnits == 0)
                    return formation.OrderPosition;

                // Calculate weighted center based on unit types
                var positions = formation.GetUnits()
                    .Select(agent => new
                    {
                        Position = agent.Position,
                        Weight = agent.HasMount ? 2f : 1f
                    })
                    .ToList();

                var totalWeight = positions.Sum(p => p.Weight);
                var weightedSum = positions.Aggregate(
                    Vec3.Zero,
                    (sum, p) => sum + p.Position * p.Weight
                );

                return weightedSum / totalWeight;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error calculating formation center", ex);
                return formation.OrderPosition;
            }
        }

        private void CleanupCache(object state)
        {
            if (_disposed)
                return;

            try
            {
                if (_mission == null || _mission.Teams == null)
                {
                    _logger.LogWarning("Mission or Teams is null during cache cleanup");
                    return;
                }

                // Clear caches for formations that no longer exist
                var activeFormationIds = _mission.Teams
                    .Where(team => team != null)
                    .SelectMany(team => team.Formations ?? Enumerable.Empty<Formation>())
                    .Where(formation => formation != null)
                    .Select(formation => formation.Index)
                    .ToHashSet();

                int removedSpacingEntries = 0;
                int removedCenterEntries = 0;

                foreach (var cachedId in _spacingCache.Keys.ToList())
                {
                    if (!activeFormationIds.Contains(cachedId))
                    {
                        if (_spacingCache.TryRemove(cachedId, out _))
                            removedSpacingEntries++;
                    }
                }

                foreach (var cachedId in _centerCache.Keys.ToList())
                {
                    if (!activeFormationIds.Contains(cachedId))
                    {
                        if (_centerCache.TryRemove(cachedId, out _))
                            removedCenterEntries++;
                    }
                }

                if (_config.Performance.EnableDetailedMetrics)
                {
                    _logger.LogDebug($"Formation cache cleanup completed. Active formations: {activeFormationIds.Count}, Removed spacing entries: {removedSpacingEntries}, Removed center entries: {removedCenterEntries}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during cache cleanup", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (this)
            {
                if (_disposed)
                    return;

                try
                {
                    _cacheCleanupTimer?.Dispose();
                    _spacingCache.Clear();
                    _centerCache.Clear();
                    _disposed = true;

                    _logger.LogInformation("FormationHelper disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error disposing FormationHelper", ex);
                }
            }
        }
    }
}
