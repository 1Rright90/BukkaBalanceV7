using Microsoft.Extensions.ObjectPool;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core;
using YSBCaptain.Core.Base;
using YSBCaptain.Utilities;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;

namespace YSBCaptain.Gameplay.Systems.Spawning
{
    /// <summary>
    /// Thread-safe object pool for SpawnRequest instances with performance tracking
    /// </summary>
    public class SpawnRequestPool
    {
        private readonly ConcurrentDictionary<string, SpawnRequestInfo> _requests = new ConcurrentDictionary<string, SpawnRequestInfo>();
        private readonly ILogger<SpawnRequestPool> _logger;

        public SpawnRequestPool(ILogger<SpawnRequestPool> logger)
        {
            _logger = logger;
        }

        public void AddRequest(SpawnRequest request)
        {
            var info = new SpawnRequestInfo(request);
            if (!_requests.TryAdd(request.Id, info))
            {
                _logger.LogWarning($"Request with ID {request.Id} already exists");
            }
        }

        public bool TryGetRequest(string id, out SpawnRequest request)
        {
            if (_requests.TryGetValue(id, out var info))
            {
                request = info.Request;
                return true;
            }
            request = null;
            return false;
        }

        public void RemoveRequest(string id)
        {
            if (_requests.TryRemove(id, out _))
            {
                _logger.LogInformation($"Removed request {id}");
            }
        }

        private class SpawnRequestInfo
        {
            public SpawnRequest Request { get; }
            public DateTime CreatedTime { get; }

            public SpawnRequestInfo(SpawnRequest request)
            {
                Request = request;
                CreatedTime = DateTime.UtcNow;
            }
        }
    }

    public class SpawnRequest
    {
        public string Id { get; set; }
        public int TeamIndex { get; set; }
        public int FormationIndex { get; set; }
        public int TroopCount { get; set; }
        public string Culture { get; set; }
        public bool IsValid { get; set; }
        public float SpawnDelay { get; set; }
        public float SpawnTimer { get; set; }

        public void Reset()
        {
            TeamIndex = 0;
            FormationIndex = 0;
            TroopCount = 0;
            Culture = null;
            IsValid = false;
            SpawnDelay = 0f;
            SpawnTimer = 0f;
        }
    }
}
