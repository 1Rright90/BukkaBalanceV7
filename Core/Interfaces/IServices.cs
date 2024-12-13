using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YSBCaptain.Core.Models;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Core.Interfaces
{
    // Other interfaces are defined in their own files:
    // IHealthCheck.cs
    // IMemoryProfiler.cs
    // IPerformanceMonitor.cs
    // IResourceManager.cs
    // ITelemetry.cs
    // ISystemMonitor.cs
    
    public interface IConfigurationProvider
    {
        T GetConfiguration<T>() where T : class, new();
        void SaveConfiguration<T>(T configuration) where T : class;
        void ReloadConfiguration();
        bool ValidateConfiguration<T>(T configuration) where T : class;
    }
}
