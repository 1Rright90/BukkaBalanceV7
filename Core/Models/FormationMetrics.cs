using System;

namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents metrics for a single formation. This class is immutable for thread safety.
    /// </summary>
    public sealed class FormationMetrics
    {
        /// <summary>
        /// Number of units in the formation
        /// </summary>
        public int UnitCount { get; }

        /// <summary>
        /// Formation cohesion value (0-1)
        /// </summary>
        public float Cohesion { get; }

        /// <summary>
        /// Formation spacing value
        /// </summary>
        public float Spacing { get; }

        /// <summary>
        /// Last time the metrics were updated
        /// </summary>
        public DateTime LastUpdate { get; }

        public FormationMetrics(int unitCount, float cohesion, float spacing)
        {
            UnitCount = unitCount;
            Cohesion = Math.Max(0, Math.Min(1, cohesion)); // Ensure cohesion is between 0 and 1
            Spacing = Math.Max(0, spacing);
            LastUpdate = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a copy of the metrics with updated values
        /// </summary>
        public FormationMetrics WithUpdates(int? unitCount = null, float? cohesion = null, float? spacing = null)
        {
            return new FormationMetrics(
                unitCount ?? this.UnitCount,
                cohesion ?? this.Cohesion,
                spacing ?? this.Spacing
            );
        }

        public override string ToString()
        {
            return $"Units: {UnitCount}, Cohesion: {Cohesion:F2}, Spacing: {Spacing:F2}";
        }
    }
}
