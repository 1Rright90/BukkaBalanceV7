using System;
using System.Threading;
using System.Threading.Tasks;

namespace YSBCaptain.Core.Telemetry
{
    public interface ITelemetryClient : IDisposable
    {
        Task TrackMetricAsync(string name, double value, CancellationToken cancellationToken = default);
        Task TrackEventAsync(string eventName, string properties = null, CancellationToken cancellationToken = default);
        Task TrackExceptionAsync(Exception exception, string properties = null, CancellationToken cancellationToken = default);
        Task FlushAsync(CancellationToken cancellationToken = default);
    }
}
