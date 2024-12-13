using System;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Error;
using YSBCaptain.Network.Validation;
using TaleWorlds.MountAndBlade.Network.Messages;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Network.Base
{
    /// <summary>
    /// Base class for network message processing with rate limiting and batching
    /// </summary>
    public abstract class BaseMessageProcessor : BaseComponent, IMessageProcessor
    {
        private readonly MessageProcessorErrorManager _errorManager;
        private readonly NetworkMessageValidator _messageValidator;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly int _batchSize;
        private int _currentBatchSize;
        private readonly object _batchLock = new object();
        private long _processedMessages;
        private long _failedMessages;
        private readonly object _statsLock = new object();
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        protected BaseMessageProcessor(
            string componentName,
            Microsoft.Extensions.Logging.ILogger<BaseMessageProcessor> logger,
            NetworkMessageValidator validator,
            int batchSize,
            IPerformanceMonitor performanceMonitor) 
            : base(componentName, logger, performanceMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messageValidator = validator ?? throw new ArgumentNullException(nameof(validator));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _batchSize = batchSize;
            _currentBatchSize = batchSize;

            _errorManager = new MessageProcessorErrorManager(
                TimeSpan.FromMinutes(5),
                TimeSpan.FromSeconds(30),
                10,
                logger,
                performanceMonitor
            );
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            try
            {
                OnInitialize();
                _logger.LogInformation($"Initializing {ComponentName}");
                _currentBatchSize = _batchSize;
                _processedMessages = 0;
                _failedMessages = 0;
                _isInitialized = true;
                _logger.LogInformation($"Message processor {ComponentName} initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize message processor {ComponentName}");
                throw;
            }
        }

        public void Shutdown()
        {
            if (!_isInitialized)
                return;

            try
            {
                OnShutdown();
                _logger.LogInformation($"Shutting down {ComponentName}");
                _currentBatchSize = 0;
                _isInitialized = false;
                _logger.LogInformation($"Message processor {ComponentName} shutdown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error shutting down message processor {ComponentName}");
                throw;
            }
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnShutdown() { }

        public MessageStatistics GetMessageStatistics()
        {
            lock (_statsLock)
            {
                return new MessageStatistics
                {
                    TotalMessages = _processedMessages + _failedMessages,
                    ProcessedMessages = _processedMessages,
                    FailedMessages = _failedMessages,
                    AverageProcessingTime = TimeSpan.FromMilliseconds(_performanceMonitor.GetMetricValue("AverageProcessingTime")),
                    LastProcessedTime = DateTime.UtcNow,
                    MessagesPerSecond = _performanceMonitor.GetMetricValue("MessagesPerSecond")
                };
            }
        }

        protected virtual async Task ProcessMessageAsync(IGameNetworkMessage message, CancellationToken cancellationToken)
        {
            if (message == null)
            {
                _logger.LogWarning("Attempted to process null message");
                return;
            }

            try
            {
                if (!await _messageValidator.ValidateMessageAsync(message, cancellationToken))
                {
                    _logger.LogWarning($"Message validation failed for {message.GetType().Name}");
                    _errorManager.HandleMessageWarning(message);
                    return;
                }

                using (_performanceMonitor.MeasureScope($"ProcessMessage_{message.GetType().Name}"))
                {
                    await ProcessMessageInternalAsync(message, cancellationToken);
                    Interlocked.Increment(ref _processedMessages);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message of type {message.GetType().Name}");
                _errorManager.HandleMessageError(message, ex);
                Interlocked.Increment(ref _failedMessages);
            }
        }

        public async Task ProcessMessageAsync(object message)
        {
            if (message == null)
            {
                _logger.LogWarning("Attempted to process null message");
                return;
            }

            try
            {
                var networkMessage = message as IGameNetworkMessage;
                if (networkMessage == null)
                {
                    _logger.LogWarning($"Message is not of type IGameNetworkMessage: {message.GetType().Name}");
                    return;
                }

                await ProcessMessageAsync(networkMessage, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing message of type {message.GetType().Name}");
                throw;
            }
        }

        protected abstract Task ProcessMessageInternalAsync(IGameNetworkMessage message, CancellationToken cancellationToken);

        public int GetCurrentBatchSize()
        {
            lock (_batchLock)
            {
                return _currentBatchSize;
            }
        }

        public void UpdateBatchSize(int newSize)
        {
            lock (_batchLock)
            {
                if (newSize <= 0)
                {
                    _logger.LogWarning($"Attempted to set invalid batch size: {newSize}. Using default: {_batchSize}");
                    _currentBatchSize = _batchSize;
                }
                else
                {
                    _currentBatchSize = newSize;
                    _performanceMonitor.RecordMetric($"{ComponentName}_BatchSize", newSize);
                }
            }
        }

        public override async Task StartAsync()
        {
            if (_isDisposed)
            {
                _logger.LogWarning($"Component {ComponentName} is disposed");
                return;
            }

            try
            {
                Initialize();
                await base.StartAsync();
                _logger.LogInformation($"Message processor {ComponentName} started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start message processor {ComponentName}");
                throw;
            }
        }

        public override async Task StopAsync()
        {
            if (!_isInitialized)
                return;

            try
            {
                await base.StopAsync();
                Shutdown();
                _logger.LogInformation($"Message processor {ComponentName} stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping message processor {ComponentName}");
                throw;
            }
        }

        protected override void OnDispose()
        {
            _errorManager?.Dispose();
            base.OnDispose();
        }
    }

    /// <summary>
    /// Specialized error manager for message processing
    /// </summary>
    public class MessageProcessorErrorManager
    {
        private readonly TimeSpan _errorWindowDuration;
        private readonly TimeSpan _warningWindowDuration;
        private readonly int _maxErrorsBeforeBlock;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly IPerformanceMonitor _performanceMonitor;

        public MessageProcessorErrorManager(
            TimeSpan errorWindowDuration,
            TimeSpan warningWindowDuration,
            int maxErrorsBeforeBlock,
            Microsoft.Extensions.Logging.ILogger logger,
            IPerformanceMonitor performanceMonitor)
        {
            _errorWindowDuration = errorWindowDuration;
            _warningWindowDuration = warningWindowDuration;
            _maxErrorsBeforeBlock = maxErrorsBeforeBlock;
            _logger = logger;
            _performanceMonitor = performanceMonitor;
        }

        public void HandleMessageError(IGameNetworkMessage message, Exception error)
        {
            _performanceMonitor.RecordError($"MessageError_{message.GetType().Name}", error);
            _logger.LogError(error, $"Message processing error: {message.GetType().Name}");
        }

        public void HandleMessageWarning(IGameNetworkMessage message)
        {
            _performanceMonitor.RecordMetric($"MessageWarning_{message.GetType().Name}", 1);
            _logger.LogWarning($"Message processing warning: {message.GetType().Name}");
        }
    }

    public class MessageStatistics
    {
        public long TotalMessages { get; set; }
        public long ProcessedMessages { get; set; }
        public long FailedMessages { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public DateTime LastProcessedTime { get; set; }
        public double MessagesPerSecond { get; set; }
    }
}
