using System;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace YSBCaptain.Utilities
{
    /// <summary>
    /// Represents a request to spawn a bot in the game
    /// Designed to work with SpawnRequestPool for efficient reuse
    /// </summary>
    public class SpawnRequest
    {
        // Unique identifier for tracking
        private readonly string _id;
        
        // Core properties
        public Team Team { get; set; }
        public BasicCultureObject Culture { get; set; }
        public BasicCharacterObject Character { get; set; }
        public MatrixFrame Position { get; set; }
        public MPPerkObject.MPOnSpawnPerkHandler OnSpawnPerkHandler { get; set; }
        public int SelectedFormation { get; set; }
        public Agent.MortalityState MortalityState { get; set; }
        public TaskCompletionSource<Agent> CompletionSource { get; set; }
        public int BotsToSpawn { get; set; }

        // Performance tracking
        public DateTime CreationTime { get; private set; }
        public DateTime CompletionTime { get; private set; }
        
        // Spawn status tracking
        public bool IsCompleted { get; private set; }
        public bool IsSuccessful { get; private set; }
        public Exception Error { get; private set; }

        public SpawnRequest()
        {
            _id = Guid.NewGuid().ToString();
            CreationTime = DateTime.UtcNow;
            BotsToSpawn = 1;
            SelectedFormation = -1;
            MortalityState = Agent.MortalityState.Mortal;
            CompletionTime = DateTime.MinValue;
        }

        public SpawnRequest(
            Team team, 
            BasicCultureObject culture, 
            BasicCharacterObject character, 
            MatrixFrame position, 
            MPPerkObject.MPOnSpawnPerkHandler onSpawnPerkHandler, 
            int selectedFormation, 
            Agent.MortalityState mortalityState, 
            TaskCompletionSource<Agent> completionSource, 
            int botsToSpawn) : this()
        {
            Team = team;
            Culture = culture;
            Character = character;
            Position = position;
            OnSpawnPerkHandler = onSpawnPerkHandler;
            SelectedFormation = selectedFormation;
            MortalityState = mortalityState;
            CompletionSource = completionSource;
            BotsToSpawn = botsToSpawn;
        }

        /// <summary>
        /// Mark the request as completed
        /// </summary>
        public void Complete(bool success, Exception error)
        {
            IsCompleted = true;
            IsSuccessful = success;
            Error = error;
            CompletionTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Get the unique identifier for this request
        /// </summary>
        public string GetId() => _id;

        /// <summary>
        /// Reset the request for reuse from pool
        /// </summary>
        public void Reset()
        {
            Team = null;
            Culture = null;
            Character = null;
            Position = default(MatrixFrame);
            OnSpawnPerkHandler = null;
            SelectedFormation = -1;
            MortalityState = Agent.MortalityState.Mortal;
            CompletionSource = null;
            BotsToSpawn = 1;
            
            IsCompleted = false;
            IsSuccessful = false;
            Error = null;
            CreationTime = DateTime.UtcNow;
            CompletionTime = DateTime.MinValue;
        }

        /// <summary>
        /// Get spawn duration in milliseconds
        /// </summary>
        public double GetDurationMs()
        {
            var endTime = CompletionTime == DateTime.MinValue ? DateTime.UtcNow : CompletionTime;
            return (endTime - CreationTime).TotalMilliseconds;
        }

        public override string ToString()
        {
            return $"SpawnRequest[{_id}]: Team={Team.Side}, Culture={Culture.Name}, " +
                   $"Character={Character.Name}, BotsToSpawn={BotsToSpawn}, " +
                   $"Duration={GetDurationMs():F2}ms, Success={IsSuccessful}";
        }
    }
}
