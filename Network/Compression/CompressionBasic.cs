using System;
using YSBCaptain.Network.Compression;

namespace YSBCaptain.Network.Compression
{
    /// <summary>
    /// Provides basic compression constants and configuration for network communication
    /// following TaleWorlds' networking patterns
    /// </summary>
    public static class CompressionBasic
    {
        #region Position and Rotation Constants

        /// <summary>
        /// Maximum value for any component of a quaternion (1/√2)
        /// Used for rotation compression
        /// </summary>
        public const float MaxQuaternionComponent = 0.7071068f;

        /// <summary>
        /// Maximum allowed Z-coordinate value for position compression
        /// Represents the highest point in the game world in units
        /// </summary>
        public const float MaxPositionZ = 2521f;

        /// <summary>
        /// Maximum allowed value for X/Y position coordinates
        /// Represents the furthest point in the game world in units
        /// </summary>
        public const float MaxPosition = 10385f;

        /// <summary>
        /// Minimum allowed value for any position coordinate
        /// Represents the lowest/closest point in the game world in units
        /// </summary>
        public const float MinPosition = -100f;

        #endregion

        #region Network Compression Configuration

        /// <summary>
        /// Compression configuration for ping values (0-1023ms)
        /// </summary>
        public static readonly CompressionInfo.Integer PingValue = new CompressionInfo.Integer(0, 1023, true);

        /// <summary>
        /// Compression configuration for world position coordinates
        /// Uses 22 bits for precision within world bounds
        /// </summary>
        public static readonly CompressionInfo.Float Position = new CompressionInfo.Float(MinPosition, MaxPosition, 22);

        /// <summary>
        /// Compression configuration for local position coordinates
        /// Uses 16 bits for precision within local space (-32 to 32 units)
        /// </summary>
        public static readonly CompressionInfo.Float LocalPosition = new CompressionInfo.Float(-32f, 32f, 16);

        /// <summary>
        /// Compression configuration for player indices
        /// Supports up to 31 players (0-30 range)
        /// </summary>
        public static readonly CompressionInfo.Integer PlayerIndex = new CompressionInfo.Integer(0, 30, true);

        #endregion
    }
}
