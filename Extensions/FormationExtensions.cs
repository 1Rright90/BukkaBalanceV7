using System;
using System.Collections.Concurrent;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using YSBCaptain.Core.Logging;
using YSBCaptain.Gameplay.Systems.Spawning;

namespace YSBCaptain.Extensions
{
    /// <summary>
    /// Extension methods for Formation-related functionality.
    /// Provides thread-safe operations for managing formations in Mount &amp; Blade II: Bannerlord.
    /// </summary>
    /// <remarks>
    /// This class follows TaleWorlds' patterns for formation management and provides:
    /// - Thread-safe formation timing
    /// - Culture-specific formation settings
    /// - Memory-efficient data structures
    /// All implementations align with Bannerlord's runtime requirements.
    /// </remarks>
    public static class FormationExtensions
    {
        private static readonly ConcurrentDictionary<Formation, SpawnTimer> _spawnTimers = new();
        private static readonly ConcurrentDictionary<Formation, float> _formationSpacing = new();
        private static readonly ConcurrentDictionary<Formation, float> _formationDensity = new();

        private static readonly ConcurrentDictionary<string, FormationDefaults> _cultureDefaults = new(
            new[]
            {
                new KeyValuePair<string, FormationDefaults>("empire", new FormationDefaults(1.0f, 1.0f)),
                new KeyValuePair<string, FormationDefaults>("vlandia", new FormationDefaults(1.2f, 0.9f)),
                new KeyValuePair<string, FormationDefaults>("sturgia", new FormationDefaults(1.1f, 1.1f)),
                new KeyValuePair<string, FormationDefaults>("aserai", new FormationDefaults(0.9f, 0.8f)),
                new KeyValuePair<string, FormationDefaults>("khuzait", new FormationDefaults(1.4f, 0.7f)),
                new KeyValuePair<string, FormationDefaults>("battania", new FormationDefaults(1.0f, 1.0f))
            });

        /// <summary>
        /// Represents default formation settings for each culture.
        /// </summary>
        private class FormationDefaults
        {
            /// <summary>
            /// Initializes a new instance of the FormationDefaults class.
            /// </summary>
            /// <param name="baseSpacing">The base spacing value for the formation.</param>
            /// <param name="baseDensity">The base density value for the formation.</param>
            public FormationDefaults(float baseSpacing, float baseDensity)
            {
                BaseSpacing = baseSpacing;
                BaseDensity = baseDensity;
            }

            /// <summary>
            /// Gets the base spacing value for the formation.
            /// </summary>
            public float BaseSpacing { get; }

            /// <summary>
            /// Gets the base density value for the formation.
            /// </summary>
            public float BaseDensity { get; }
        }

        /// <summary>
        /// Gets or creates a spawn timer for the specified formation.
        /// </summary>
        /// <param name="formation">The formation to get the timer for.</param>
        /// <param name="initialTime">Optional initial time for the timer.</param>
        /// <returns>A SpawnTimer instance for the formation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        public static SpawnTimer GetSpawnTimer(this Formation formation, float initialTime = 0f)
        {
            ArgumentNullException.ThrowIfNull(formation, nameof(formation));

            return _spawnTimers.GetOrAdd(formation, f =>
            {
                var timer = new SpawnTimer(f);
                if (initialTime > 0f)
                {
                    timer.Activate(initialTime);
                }
                Logger.LogDebug($"Created new spawn timer for formation {f.FormationIndex}");
                return timer;
            });
        }

        /// <summary>
        /// Removes the spawn timer for the specified formation.
        /// </summary>
        /// <param name="formation">The formation to remove the timer for.</param>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        public static void RemoveSpawnTimer(this Formation formation)
        {
            ArgumentNullException.ThrowIfNull(formation, nameof(formation));

            if (_spawnTimers.TryRemove(formation, out var timer))
            {
                timer.Deactivate();
                Logger.LogDebug($"Removed spawn timer for formation {formation.FormationIndex}");
            }
        }

        /// <summary>
        /// Activates the formation's spawn timer for the specified duration.
        /// </summary>
        /// <param name="formation">The formation to activate.</param>
        /// <param name="duration">The duration to activate for.</param>
        /// <param name="force">Whether to force activation even if already active.</param>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when duration is not positive.</exception>
        public static void Activate(this Formation formation, float duration, bool force = false)
        {
            ArgumentNullException.ThrowIfNull(formation, nameof(formation));
            if (duration <= 0f)
                throw new ArgumentException("Duration must be positive", nameof(duration));

            var timer = formation.GetSpawnTimer();
            if (force || !timer.IsActive)
            {
                timer.Activate(duration);
                Logger.LogDebug($"Activated formation {formation.FormationIndex} for {duration} seconds");
            }
        }

        /// <summary>
        /// Gets the spacing value for the formation based on its culture.
        /// </summary>
        /// <param name="formation">The formation to get spacing for.</param>
        /// <returns>The spacing value for the formation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        public static float GetSpacing(this Formation formation)
        {
            ArgumentNullException.ThrowIfNull(formation, nameof(formation));

            var culture = formation.Team?.GetCulture()?.StringId ?? "empire";
            var baseSpacing = _cultureDefaults.TryGetValue(culture, out var defaults) ? defaults.BaseSpacing : 1.0f;

            return _formationSpacing.GetOrAdd(formation, _ => baseSpacing);
        }

        /// <summary>
        /// Sets the spacing value for the formation.
        /// </summary>
        /// <param name="formation">The formation to set spacing for.</param>
        /// <param name="spacing">The spacing value to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when spacing is negative.</exception>
        public static void SetSpacing(this Formation formation, float spacing)
        {
            ArgumentNullException.ThrowIfNull(formation, nameof(formation));
            if (spacing < 0f)
                throw new ArgumentException("Spacing must be non-negative", nameof(spacing));

            var culture = formation.Team?.GetCulture()?.StringId ?? "empire";
            var baseSpacing = _cultureDefaults.TryGetValue(culture, out var defaults) ? defaults.BaseSpacing : 1.0f;
            var adjustedSpacing = spacing * baseSpacing;

            _formationSpacing.AddOrUpdate(formation, adjustedSpacing, (_, __) => adjustedSpacing);
            formation.FormationSpacingMultiplier = adjustedSpacing;
            Logger.LogDebug($"Set spacing for formation {formation.FormationIndex} to {adjustedSpacing} (base: {baseSpacing})");
        }

        /// <summary>
        /// Gets the density value for the formation based on its culture.
        /// </summary>
        /// <param name="formation">The formation to get density for.</param>
        /// <returns>The density value for the formation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        public static float GetFormationDensity(this Formation formation)
        {
            ArgumentNullException.ThrowIfNull(formation, nameof(formation));

            var culture = formation.Team?.GetCulture()?.StringId ?? "empire";
            var baseDensity = _cultureDefaults.TryGetValue(culture, out var defaults) ? defaults.BaseDensity : 1.0f;

            return _formationDensity.GetOrAdd(formation, _ => baseDensity);
        }

        /// <summary>
        /// Sets the density value for the formation.
        /// </summary>
        /// <param name="formation">The formation to set density for.</param>
        /// <param name="density">The density value to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when formation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when density is negative.</exception>
        public static void SetFormationDensity(this Formation formation, float density)
        {
            ArgumentNullException.ThrowIfNull(formation, nameof(formation));
            if (density < 0f)
                throw new ArgumentException("Density must be non-negative", nameof(density));

            var culture = formation.Team?.GetCulture()?.StringId ?? "empire";
            var baseDensity = _cultureDefaults.TryGetValue(culture, out var defaults) ? defaults.BaseDensity : 1.0f;
            var adjustedDensity = density * baseDensity;

            _formationDensity.AddOrUpdate(formation, adjustedDensity, (_, __) => adjustedDensity);
            formation.UnitSpacing = adjustedDensity;
            Logger.LogDebug($"Set density for formation {formation.FormationIndex} to {adjustedDensity} (base: {baseDensity})");
        }

        /// <summary>
        /// Checks if the formation is valid.
        /// </summary>
        /// <param name="formation">The formation to check.</param>
        /// <returns>True if the formation is valid, false otherwise.</returns>
        public static bool IsValid(this Formation formation)
        {
            return formation != null && formation.Team != null && formation.Team.IsValid();
        }

        /// <summary>
        /// Gets the average position of the formation.
        /// </summary>
        /// <param name="formation">The formation to get the average position for.</param>
        /// <returns>The average position of the formation.</returns>
        public static Vec3 GetAveragePosition(this Formation formation)
        {
            if (!formation.IsValid())
            {
                Logger.LogWarning("Attempted to get average position for invalid formation");
                return Vec3.Zero;
            }

            return formation.OrderPosition.AsVec3;
        }

        /// <summary>
        /// Clears all formation caches.
        /// </summary>
        public static void ClearFormationCaches()
        {
            _spawnTimers.Clear();
            _formationSpacing.Clear();
            _formationDensity.Clear();
            Logger.LogDebug("Cleared all formation caches");
        }
    }
}
