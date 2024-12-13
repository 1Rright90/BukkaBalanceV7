using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Core.Interfaces
{
    public interface INetworkOptimizer
    {
        void Initialize();
        Task OptimizeAsync();
        void Shutdown();
    }

    public interface IDynamicResourceManager : IResourceManager
    {
        Task<bool> LoadDynamicResourceAsync(string resourceId);
        Task<bool> UnloadDynamicResourceAsync(string resourceId);
    }

    public interface IPerformanceMetrics
    {
        double CpuUsage { get; }
        double MemoryUsage { get; }
        double NetworkLatency { get; }
        double NetworkBandwidth { get; }
    }
}
