using System;
using System.Threading.Tasks;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Base interface for all system components
    /// </summary>
    public interface ISystemComponent : IDisposable
    {
        string Name { get; }
        Models.ExecutionState GetCurrentState();
        Task StartAsync();
        Task StopAsync();
    }
}
