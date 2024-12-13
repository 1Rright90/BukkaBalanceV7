using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.MountAndBlade.Multiplayer.Models;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Network.Compression
{
    /// <summary>
    /// Provides compression utilities for mission data in multiplayer environments.
    /// </summary>
    public class MissionCompression
    {
        private const int DefaultMaxRoundTime = 3600; // 1 hour
        private const int MinRoundTime = 30; // Minimum round time
        private const float MinDebugScale = 0.5f;
        private const float MaxDebugScale = 1.5f;
        private const int MinAgentHealth = -1;
        private const int MaxAgentHealth = 15;

        private readonly Mission _mission;

        public readonly CompressionInfo.Float DebugScale;
        public readonly CompressionInfo.Integer AgentHealth;
        public readonly CompressionInfo.Integer RoundTime;
        public readonly CompressionInfo.Float CapturePointProgress;

        public MissionCompression(Mission mission)
        {
            _mission = mission ?? throw new ArgumentNullException(nameof(mission));

            DebugScale = new CompressionInfo.Float(MinDebugScale, MaxDebugScale, 8);
            AgentHealth = new CompressionInfo.Integer(MinAgentHealth, MaxAgentHealth, true);
            CapturePointProgress = new CompressionInfo.Float(0f, 1f, 10);

            try
            {
                int maxRoundTime = GetMaxRoundTimeAsync(CancellationToken.None).GetAwaiter().GetResult();
                if (!IsValidRoundTime(maxRoundTime))
                {
                    Debug.Print($"Invalid round time limit ({maxRoundTime}), using default");
                    maxRoundTime = DefaultMaxRoundTime;
                }

                RoundTime = new CompressionInfo.Integer(0, maxRoundTime, true);
            }
            catch (Exception ex)
            {
                Debug.Print($"Failed to initialize round time: {ex.Message}. Using default.");
                RoundTime = new CompressionInfo.Integer(0, DefaultMaxRoundTime, true);
            }
        }

        private async Task<int> GetMaxRoundTimeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var multiplayerGameMode = _mission.GetMissionBehavior<MissionMultiplayerGameModeBase>();
                var timerComponent = _mission.GetMissionBehavior<MultiplayerTimerComponent>();

                if (multiplayerGameMode != null && timerComponent != null && timerComponent.IsTimerRunning)
                {
                    int remainingTime = (int)timerComponent.GetRemainingTime(true);
                    if (remainingTime > 0)
                    {
                        return Math.Max(remainingTime, MinRoundTime);
                    }
                }

                var optionsComponent = _mission.GetMissionBehavior<MissionOptionsComponent>();
                if (optionsComponent != null)
                {
                    int roundTimeLimit = optionsComponent.GetOptionValueFromOptionType(
                        MultiplayerOptions.OptionType.RoundTimeLimit
                    );

                    if (roundTimeLimit > 0)
                    {
                        return Math.Max(roundTimeLimit, MinRoundTime);
                    }
                }

                return DefaultMaxRoundTime;
            }
            catch (Exception ex)
            {
                Debug.Print($"Error getting maximum round time: {ex.Message}");
                return DefaultMaxRoundTime;
            }
        }

        public bool IsValidDebugScale(float scale) =>
            scale >= MinDebugScale && scale <= MaxDebugScale;

        public bool IsValidAgentHealth(int health) =>
            health >= MinAgentHealth && health <= MaxAgentHealth;

        public bool IsValidCaptureProgress(float progress) =>
            progress >= 0f && progress <= 1f;

        public bool IsValidRoundTime(int time) =>
            time >= MinRoundTime && time <= DefaultMaxRoundTime;

        public async Task CompressDataAsync(Stream dataStream, CancellationToken cancellationToken)
        {
            if (dataStream == null) throw new ArgumentNullException(nameof(dataStream));

            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var compressedStream = new MemoryStream();
                    await dataStream.CopyToAsync(compressedStream, cancellationToken).ConfigureAwait(false);

                    Debug.Print("Compression successful.");
                    return;
                }
                catch (OperationCanceledException)
                {
                    Debug.Print("Compression canceled.");
                    throw;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Debug.Print($"Compression failed (attempt {retryCount}): {ex.Message}");
                    if (retryCount >= maxRetries)
                    {
                        throw;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}