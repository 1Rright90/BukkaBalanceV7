using System.Threading.Tasks;

namespace YSBCaptain.Core.Interfaces
{
    public interface IMessageProcessor
    {
        void Initialize();
        void Shutdown();
        Task ProcessMessageAsync(object message);
        int GetCurrentBatchSize();
        void UpdateBatchSize(int newSize);
        MessageStatistics GetMessageStatistics();
    }
}
