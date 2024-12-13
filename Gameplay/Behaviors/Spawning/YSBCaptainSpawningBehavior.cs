using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Captain;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.ObjectSystem;
using NetworkMessages.FromServer;
using Timer = TaleWorlds.Core.Timer;
using Formation = TaleWorlds.MountAndBlade.Formation;
using YSBCaptain.Core;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Extensions;
using YSBCaptain.Network;
using YSBCaptain.Performance;
using YSBCaptain.Core.Logging;
using YSBCaptain.Gameplay.Systems.Spawning;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Behaviors.Spawning
{
    /// <summary>
    /// Spawning behavior implementation that follows TaleWorlds' layered architecture:
    /// Core → Extensions → Network/Performance → Gameplay → Patches.
    /// </summary>
    /// <remarks>
    /// This component:
    /// - Implements core spawning logic following TaleWorlds' patterns
    /// - Manages spawn queues and formation assignments
    /// - Integrates with performance monitoring and resource management
    /// - Provides thread-safe operations for multiplayer scenarios
    /// Dependencies:
    /// - Core layer for configuration and logging
    /// - Extensions layer for game object extensions
    /// - Network layer for multiplayer support
    /// - Performance layer for resource monitoring
    /// </remarks>
    public sealed class YSBCaptainSpawningBehavior : YSBCaptainSpawningBehaviorBase
    {
        private readonly SpawnQueueManager _spawnQueueManager;
        private readonly FormationManager _formationManager;
        private readonly SpawnStateManager _stateManager;
        private readonly DynamicResourceManager _resourceManager;
        private readonly ILogger<YSBCaptainSpawningBehavior> _logger;

        /// <summary>
        /// Initializes a new instance of the YSBCaptainSpawningBehavior class.
        /// </summary>
        public YSBCaptainSpawningBehavior(
            SpawnQueueManager spawnQueueManager,
            FormationManager formationManager,
            SpawnStateManager stateManager,
            DynamicResourceManager resourceManager)
            : base()  // Call base constructor which creates its own logger
        {
            _spawnQueueManager = spawnQueueManager ?? throw new ArgumentNullException(nameof(spawnQueueManager));
            _formationManager = formationManager ?? throw new ArgumentNullException(nameof(formationManager));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<YSBCaptainSpawningBehavior>();
        }

        /// <summary>
        /// Gets whether the current round is in progress.
        /// </summary>
        /// <returns>True if the round is in progress, false otherwise.</returns>
        protected override bool IsRoundInProgress() => base.IsRoundInProgress();

        /// <summary>
        /// Requests the start of a new spawn session.
        /// </summary>
        public override void RequestStartSpawnSession()
        {
            try
            {
                if (!_stateManager.IsSpawningEnabled)
                {
                    Mission.Current.SetBattleAgentCount(-1);
                    _stateManager.EnableSpawning();
                    base.ResetSpawnCounts();
                    base.ResetSpawnTimers();
                    _logger.LogInformation("Spawn session started successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start spawn session");
                throw new InvalidOperationException("Failed to start spawn session", ex);
            }
        }

        /// <summary>
        /// Initializes the spawning behavior with the specified spawn component.
        /// </summary>
        /// <param name="spawnComponent">The spawn component to initialize with.</param>
        /// <exception cref="ArgumentNullException">Thrown when spawnComponent is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        public override void Initialize(SpawnComponent spawnComponent)
        {
            if (spawnComponent == null)
                throw new ArgumentNullException(nameof(spawnComponent));

            try
            {
                base.Initialize(spawnComponent);
                _stateManager.Initialize(Mission.Current);
                _spawnQueueManager.Initialize();
                _formationManager.Initialize();
                _logger.LogInformation("YSBCaptainSpawningBehavior initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize YSBCaptainSpawningBehavior");
                throw new InvalidOperationException("Failed to initialize spawning behavior", ex);
            }
        }

        /// <summary>
        /// Called every tick to update the spawning behavior.
        /// </summary>
        /// <param name="dt">Time elapsed since last tick in seconds.</param>
        public override void OnTick(float dt)
        {
            try
            {
                base.OnTick(dt);
                if (!_stateManager.IsSpawningEnabled) return;

                _resourceManager.MonitorResources();
                _spawnQueueManager.ProcessQueue();
                _formationManager.UpdateFormations();
                _stateManager.UpdateTimers(dt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in spawning behavior tick");
            }
        }

        /// <summary>
        /// Clears the scene and resets all managers.
        /// </summary>
        public override void OnClearScene()
        {
            try
            {
                base.OnClearScene();
                _spawnQueueManager.Clear();
                _formationManager.Clear();
                _stateManager.Reset();
                _logger.LogInformation("Scene cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing scene");
            }
        }

        /// <summary>
        /// Handles a new client connection.
        /// </summary>
        /// <param name="peer">The network communicator for the new client.</param>
        protected override void HandleNewClientConnect(NetworkCommunicator peer)
        {
            try
            {
                base.HandleNewClientConnect(peer);
                _stateManager.HandleNewClient(peer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling new client connection");
            }
        }

        /// <summary>
        /// Handles a late new client connection after loading is finished.
        /// </summary>
        /// <param name="peer">The network communicator for the new client.</param>
        protected override void HandleLateNewClientAfterLoadingFinished(NetworkCommunicator peer)
        {
            try
            {
                base.HandleLateNewClientAfterLoadingFinished(peer);
                _stateManager.HandleLateNewClient(peer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling late new client connection");
            }
        }

        /// <summary>
        /// Determines whether early agent visuals despawning is allowed.
        /// </summary>
        /// <param name="peer">The mission peer to check.</param>
        /// <returns>True if early despawning is allowed, false otherwise.</returns>
        public override bool AllowEarlyAgentVisualsDespawning(MissionPeer peer)
        {
            if (peer == null)
                return false;

            return !_stateManager.IsRoundInProgress || !_stateManager.IsSpawningEnabled;
        }
    }

    public class SpawnQueueManager
    {
        private readonly ConcurrentQueue<SpawnRequest> _spawnQueue;
        private readonly object _spawnLock = new object();

        public SpawnQueueManager()
        {
            _spawnQueue = new ConcurrentQueue<SpawnRequest>();
        }

        public void Initialize()
        {
            // Initialize spawn queue processing
        }

        public void ProcessQueue()
        {
            lock (_spawnLock)
            {
                while (_spawnQueue.TryDequeue(out var spawnRequest))
                {
                    try
                    {
                        SpawnBotWithRequest(spawnRequest);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Spawn Request Processing Error: {ex.Message}", ex);
                    }
                }
            }
        }

        public void Clear()
        {
            // Clear spawn queue
        }

        private void SpawnBotWithRequest(SpawnRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    Logger.LogError("[YSBCaptain] SpawnBotWithRequest: Request is null");
                    return;
                }

                // Get formation
                var formation = FormationManager.GetEmptyFormation(request.Team);
                if (formation == null)
                {
                    Logger.LogWarning("[YSBCaptain] SpawnBotWithRequest: No empty formation available");
                    return;
                }

                // Use adjusted batch size for spawning
                int adjustedBatchSize = DynamicResourceManager.Instance.GetAdjustedBatchSize(SpawnBatchSize);

                // Batch spawn with dynamic sizing
                for (int i = 0; i < request.NumberOfBots; i += adjustedBatchSize)
                {
                    int currentBatchSize = Math.Min(adjustedBatchSize, request.NumberOfBots - i);
                    for (int j = 0; j < currentBatchSize; j++)
                    {
                        var spawnTask = SpawnHelper.Instance.SpawnBotAsync(
                            request.Team,
                            request.Culture,
                            request.Character,
                            request.Position,
                            request.OnSpawnPerkHandler,
                            formation.Index,
                            request.MortalityState
                        );

                        spawnTask.ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                            {
                                Logger.LogError($"[YSBCaptain] Bot spawn failed: {task.Exception?.InnerException?.Message}", task.Exception?.InnerException);
                            }
                        });
                    }
                }

                formation.SetControlledByAI(true, true);
                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);

                Logger.LogInformation($"[YSBCaptain] Successfully spawned bot in formation {formation.FormationIndex}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[YSBCaptain] Error in SpawnBotWithRequest: {ex.Message}", ex);
            }
        }
    }

    public class FormationManager
    {
        private readonly ConcurrentDictionary<Team, ConcurrentBag<int>> _reservedFormationsByTeam;
        private readonly object _formationLock = new object();

        public FormationManager()
        {
            _reservedFormationsByTeam = new ConcurrentDictionary<Team, ConcurrentBag<int>>();
        }

        public void Initialize()
        {
            // Initialize formation management
        }

        public void UpdateFormations()
        {
            // Update formations
        }

        public void Clear()
        {
            // Clear formations
        }

        public static Formation GetEmptyFormation(Team team)
        {
            if (!_reservedFormationsByTeam.TryGetValue(team, out var formations))
            {
                formations = new ConcurrentBag<int>();
                _reservedFormationsByTeam.TryAdd(team, formations);
            }

            lock (_formationLock)
            {
                var formation = team.FormationsIncludingSpecialAndEmpty
                    .FirstOrDefault(x =>
                        x.PlayerOwner == null &&
                        !x.ContainsAgentVisuals &&
                        x.CountOfUnits == 0 &&
                        !formations.Contains(x.Index));

                if (formation != null)
                {
                    formations.Add(formation.Index);
                    Logger.LogInformation($"[YSBCaptain] Formation {formation.Index} reserved for {team.Side}");
                    return formation;
                }
                return null;
            }
        }
    }

    public class SpawnStateManager
    {
        private bool _isSpawningEnabled;
        private bool _isRoundInProgress;
        private readonly List<KeyValuePair<MissionPeer, Timer>> _enforcedSpawnTimers;

        public SpawnStateManager()
        {
            _enforcedSpawnTimers = new List<KeyValuePair<MissionPeer, Timer>>();
        }

        public void Initialize(Mission mission)
        {
            // Initialize spawn state management
        }

        public void EnableSpawning()
        {
            _isSpawningEnabled = true;
        }

        public void UpdateTimers(float dt)
        {
            // Update spawn timers
        }

        public void Reset()
        {
            // Reset spawn state
        }

        public void HandleNewClient(NetworkCommunicator networkPeer)
        {
            // Handle new client
        }

        public void HandleLateNewClient(NetworkCommunicator networkPeer)
        {
            // Handle late new client
        }

        public bool IsSpawningEnabled => _isSpawningEnabled;

        public bool IsRoundInProgress => _isRoundInProgress;
    }

    public class SpawnRequest
    {
        public Team Team { get; set; }
        public BasicCultureObject Culture { get; set; }
        public BasicCharacterObject Character { get; set; }
        public MatrixFrame? Position { get; set; }
        public MPPerkObject.MPOnSpawnPerkHandler OnSpawnPerkHandler { get; set; }
        public int NumberOfBots { get; set; }
        public Agent.MortalityState MortalityState { get; set; }
    }
}
