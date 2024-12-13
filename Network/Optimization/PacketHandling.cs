using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network;
using TaleWorlds.MountAndBlade.Network.Messages;
using YSBCaptain.Network.Compression;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Network.Optimization
{
    /// <summary>
    /// Processes network messages asynchronously with batching and compression
    /// Follows TaleWorlds' networking patterns for efficient message handling
    /// </summary>
    public class MessageProcessor : IAsyncDisposable, IDisposable
    {
        private readonly ConcurrentQueue<GameNetworkMessage> _messageQueue;
        private readonly CancellationTokenSource _processingCts;
        private readonly Task _processingTask;
        private readonly int _batchSize;
        private readonly CustomCompression _compression;
        private bool _isDisposed;
        private readonly ILogger _logger;

        private const int DefaultBatchSize = 100;
        private const int MaxQueueSize = 10000;

        /// <summary>
        /// Initializes a new message processor with specified batch size
        /// </summary>
        /// <param name="batchSize">Number of messages to process in each batch</param>
        public MessageProcessor(int batchSize = DefaultBatchSize)
        {
            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive");

            _messageQueue = new ConcurrentQueue<GameNetworkMessage>();
            _processingCts = new CancellationTokenSource();
            _batchSize = batchSize;
            _compression = new CustomCompression();
            _processingTask = Task.Run(ProcessQueueAsync);
            _logger = new Logger();
        }

        /// <summary>
        /// Enqueues a network message for processing
        /// </summary>
        /// <param name="message">The message to process</param>
        /// <exception cref="ObjectDisposedException">Thrown when the processor is disposed</exception>
        public void EnqueueMessage(GameNetworkMessage message)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MessageProcessor));
            }

            if (message == null)
            {
                _logger.LogWarning("Attempted to enqueue null message");
                return;
            }

            try
            {
                if (_messageQueue.Count >= MaxQueueSize)
                {
                    _logger.LogWarning($"Message queue is full ({MaxQueueSize} messages). Message dropped.");
                    return;
                }

                _messageQueue.Enqueue(message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to enqueue message: {ex.Message}");
            }
        }

        private async Task ProcessQueueAsync()
        {
            while (!_processingCts.Token.IsCancellationRequested)
            {
                try
                {
                    var processedCount = 0;
                    var startTime = DateTime.UtcNow;

                    while (processedCount < _batchSize && _messageQueue.TryDequeue(out var message))
                    {
                        await ProcessMessageAsync(message).ConfigureAwait(false);
                        processedCount++;
                    }

                    if (processedCount > 0)
                    {
                        var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                        if (processingTime > 100)
                        {
                            _logger.LogWarning($"Batch processing took {processingTime}ms for {processedCount} messages");
                        }
                    }

                    await Task.Delay(1).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Message processing failed: {ex.Message}");
                    await Task.Delay(1000).ConfigureAwait(false); // Back off on error
                }
            }
        }

        private async Task ProcessMessageAsync(GameNetworkMessage message)
        {
            try
            {
                var compressedMessage = await _compression.CompressMessageAsync(message).ConfigureAwait(false);
                GameNetwork.BeginModuleEventAsServer(compressedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to process message: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            try
            {
                // Signal cancellation and trigger cleanup
                _processingCts?.Cancel();
        
                // Clean up synchronous resources immediately
                _messageQueue?.Dispose();
                _processingCts?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MessageProcessor disposal");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            try
            {
                // Signal cancellation
                _processingCts?.Cancel();

                // Wait for the processing task to complete with a timeout
                if (_processingTask != null)
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        var timeoutTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
                        var completedTask = await Task.WhenAny(_processingTask, timeoutTask).ConfigureAwait(false);
                        
                        if (completedTask == timeoutTask)
                        {
                            _logger.LogWarning("Processing task disposal timed out after 5 seconds");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during disposal
                    }
                }

                // Clean up resources
                _messageQueue?.Dispose();
                _processingCts?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MessageProcessor async disposal");
            }
        }
    }
}