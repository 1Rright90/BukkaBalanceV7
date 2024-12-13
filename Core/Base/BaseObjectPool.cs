using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.Base
{
    /// <summary>
    /// Generic base class for thread-safe object pooling with performance monitoring
    /// </summary>
    /// <typeparam name="T">The type of objects to pool.</typeparam>
    public abstract class BaseObjectPool<T> : BaseInitializable where T : class
    {
        private readonly ConcurrentQueue<T> _pool;
        private readonly ConcurrentDictionary<string, T> _activeItems;
        private readonly IPerformanceMonitor _performanceMonitor;
        private readonly ILogger _logger;
        private int _currentPoolSize;
        private readonly int _maxPoolSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseObjectPool{T}"/> class.
        /// </summary>
        /// <param name="initialMaxSize">Initial maximum size of the pool.</param>
        /// <param name="performanceMonitor">The performance monitor.</param>
        /// <param name="logger">The logger.</param>
        protected BaseObjectPool(
            int initialMaxSize,
            IPerformanceMonitor performanceMonitor,
            ILogger logger)
        {
            if (initialMaxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialMaxSize));
            if (performanceMonitor == null)
                throw new ArgumentNullException(nameof(performanceMonitor));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _maxPoolSize = initialMaxSize;
            _performanceMonitor = performanceMonitor;
            _logger = logger;
            _pool = new ConcurrentQueue<T>();
            _activeItems = new ConcurrentDictionary<string, T>();
            _currentPoolSize = 0;
        }

        /// <summary>
        /// Rents an item from the pool or creates a new one if none are available.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public virtual async Task<T> RentAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed("RentAsync");
            ThrowIfNotInitialized("RentAsync");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _performanceMonitor.LogPerformanceMetricAsync("PoolRentAttempts", 1, cancellationToken).ConfigureAwait(false);

            T item = null;
            if (_pool.TryDequeue(out item))
            {
                Interlocked.Decrement(ref _currentPoolSize);
                await _performanceMonitor.LogPerformanceMetricAsync("PoolHits", 1, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _performanceMonitor.LogPerformanceMetricAsync("PoolMisses", 1, cancellationToken).ConfigureAwait(false);
                item = await CreateNewItemAsync(cancellationToken).ConfigureAwait(false);
            }

            var id = Guid.NewGuid().ToString();
            _activeItems.TryAdd(id, item);
            
            sw.Stop();
            await _performanceMonitor.LogPerformanceMetricAsync("PoolRentTime", sw.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
            
            return item;
        }

        /// <summary>
        /// Returns an item to the pool.
        /// </summary>
        /// <param name="item">The item to return.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public virtual async Task ReturnAsync(T item, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            ThrowIfDisposed("ReturnAsync");
            ThrowIfNotInitialized("ReturnAsync");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var kvp = _activeItems.FirstOrDefault(x => x.Value == item);
            if (!string.IsNullOrEmpty(kvp.Key) && _activeItems.TryRemove(kvp.Key, out _))
            {
                if (Interlocked.Increment(ref _currentPoolSize) <= _maxPoolSize)
                {
                    await ResetItemAsync(item, cancellationToken).ConfigureAwait(false);
                    _pool.Enqueue(item);
                    await _performanceMonitor.LogPerformanceMetricAsync("PoolReturns", 1, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    Interlocked.Decrement(ref _currentPoolSize);
                    await DisposeItemAsync(item, cancellationToken).ConfigureAwait(false);
                    await _performanceMonitor.LogPerformanceMetricAsync("PoolItemsDisposed", 1, cancellationToken).ConfigureAwait(false);
                }
            }

            sw.Stop();
            await _performanceMonitor.LogPerformanceMetricAsync("PoolReturnTime", sw.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new item for the pool.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task<T> CreateNewItemAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Resets an item before returning it to the pool.
        /// </summary>
        /// <param name="item">The item to reset.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task ResetItemAsync(T item, CancellationToken cancellationToken);

        /// <summary>
        /// Disposes of an item when it can't be returned to the pool.
        /// </summary>
        /// <param name="item">The item to dispose.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task DisposeItemAsync(T item, CancellationToken cancellationToken);

        /// <summary>
        /// Initializes the pool by creating the initial set of items.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            for (int i = 0; i < _maxPoolSize; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var item = await CreateNewItemAsync(cancellationToken).ConfigureAwait(false);
                _pool.Enqueue(item);
                Interlocked.Increment(ref _currentPoolSize);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected override async Task DisposeAsyncCore(CancellationToken cancellationToken)
        {
            while (_pool.TryDequeue(out var item))
            {
                await DisposeItemAsync(item, cancellationToken).ConfigureAwait(false);
                Interlocked.Decrement(ref _currentPoolSize);
            }

            foreach (var kvp in _activeItems.ToArray())
            {
                if (_activeItems.TryRemove(kvp.Key, out var item))
                {
                    await DisposeItemAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }

            await base.DisposeAsyncCore(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the current size of the pool.
        /// </summary>
        public int CurrentPoolSize => _currentPoolSize;

        /// <summary>
        /// Gets the maximum size of the pool.
        /// </summary>
        public int MaxPoolSize => _maxPoolSize;

        /// <summary>
        /// Gets the number of active items.
        /// </summary>
        public int ActiveItemCount => _activeItems.Count;
    }
}
