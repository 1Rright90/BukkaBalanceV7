using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using YSBCaptain.Core.Logging;
using YSBCaptain.Network.Compression;

namespace YSBCaptain.Network.Compression
{
    /// <summary>
    /// Provides compression configurations for matchmaker-specific data
    /// Follows TaleWorlds' networking patterns for multiplayer matchmaking
    /// </summary>
    public static class CompressionMatchmaker
    {
        /// <summary>
        /// Compression configuration for player KDA (Kills/Deaths/Assists) statistics
        /// Range: -1000 to 100000 to accommodate various game modes
        /// </summary>
        public static readonly CompressionInfo.Integer KillDeathAssist = new CompressionInfo.Integer(-1000, 100000, true);

        /// <summary>
        /// Compression configuration for mission time in seconds
        /// Range: -5 to 86400 (24 hours) with 20-bit precision
        /// Negative values allow for pre-match countdown
        /// </summary>
        public static readonly CompressionInfo.Float MissionTime = new CompressionInfo.Float(-5f, 86400f, 20);

        /// <summary>
        /// Compression configuration for matchmaker state
        /// Range: 0-6 representing different matchmaking states
        /// </summary>
        public static readonly CompressionInfo.Integer CurrentState = new CompressionInfo.Integer(0, 6, true);

        /// <summary>
        /// Compression configuration for player/team score
        /// Range: -1000000 to 21 to accommodate various scoring systems
        /// </summary>
        public static readonly CompressionInfo.Integer Score = new CompressionInfo.Integer(-1000000, 21, false);
    }
}
