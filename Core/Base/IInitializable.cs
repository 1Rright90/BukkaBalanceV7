using System.Threading;
using System.Threading.Tasks;

namespace YSBCaptain.Core.Base
{
    /// <summary>
    /// Defines a standard initialization pattern for components
    /// </summary>
    public interface IInitializable
    {
        /// <summary>
        /// Initialize the component synchronously
        /// </summary>
        void Initialize();

        /// <summary>
        /// Initialize the component asynchronously
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets whether the component is initialized
        /// </summary>
        bool IsInitialized { get; }
    }
}
