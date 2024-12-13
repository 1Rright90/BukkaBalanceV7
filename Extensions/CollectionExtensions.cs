using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Buffers;
using YSBCaptain.Core;

namespace YSBCaptain.Extensions
{
    /// <summary>
    /// Thread-safe extension methods for collections with optimized memory usage.
    /// Follows TaleWorlds' patterns for performance optimization and memory management.
    /// </summary>
    /// <remarks>
    /// This class provides optimized collection operations using:
    /// - Thread-safe random number generation
    /// - ArrayPool for memory efficiency
    /// - Minimal allocations for better performance
    /// All implementations align with Mount &amp; Blade II: Bannerlord's runtime requirements.
    /// </remarks>
    public static class CollectionExtensions
    {
        // Thread-local random with lazy initialization and proper cleanup
        private static readonly ThreadLocal<Random> _random = 
            new ThreadLocal<Random>(() => new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)), true);

        /// <summary>
        /// Returns a random element from the list, or default(T) if the list is empty.
        /// Uses a thread-safe approach with minimal allocations.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to get a random element from.</param>
        /// <returns>A random element from the list, or default(T) if the list is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when list is null.</exception>
        public static T GetRandomElement<T>(this IList<T> list)
        {
            ArgumentNullException.ThrowIfNull(list, nameof(list));
            return list.Count == 0 ? default : list[_random.Value.Next(list.Count)];
        }

        /// <summary>
        /// Returns a random element from the list that satisfies the predicate.
        /// Uses ArrayPool for better memory efficiency with large collections.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to get a random element from.</param>
        /// <param name="predicate">The condition that the element must satisfy.</param>
        /// <returns>A random element that satisfies the predicate, or default(T) if no elements satisfy the predicate.</returns>
        /// <exception cref="ArgumentNullException">Thrown when list or predicate is null.</exception>
        public static T GetRandomElement<T>(this IList<T> list, Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(list, nameof(list));
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            
            if (list.Count == 0) return default;

            // Rent array from pool for better memory efficiency
            int[] validIndices = ArrayPool<int>.Shared.Rent(list.Count);
            try
            {
                int validCount = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (predicate(list[i]))
                    {
                        validIndices[validCount++] = i;
                    }
                }

                return validCount == 0 ? default : list[validIndices[_random.Value.Next(validCount)]];
            }
            finally
            {
                ArrayPool<int>.Shared.Return(validIndices);
            }
        }

        /// <summary>
        /// Returns a random subset of elements from the list.
        /// Uses ArrayPool for better memory efficiency.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to get a random subset from.</param>
        /// <param name="count">The number of elements to include in the subset.</param>
        /// <returns>A list containing the random subset of elements.</returns>
        /// <exception cref="ArgumentNullException">Thrown when list is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when count is negative or greater than the list size.</exception>
        public static IList<T> GetRandomSubset<T>(this IList<T> list, int count)
        {
            ArgumentNullException.ThrowIfNull(list, nameof(list));
            if (count < 0 || count > list.Count)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (count == 0) return Array.Empty<T>();
            if (count == list.Count) return new List<T>(list);

            // Use ArrayPool for better memory efficiency
            int[] indices = ArrayPool<int>.Shared.Rent(list.Count);
            try
            {
                // Initialize index array
                for (int i = 0; i < list.Count; i++)
                    indices[i] = i;

                // Fisher-Yates shuffle for the first 'count' elements
                var rand = _random.Value;
                for (int i = 0; i < count; i++)
                {
                    int j = rand.Next(i, list.Count);
                    if (i != j)
                    {
                        (indices[i], indices[j]) = (indices[j], indices[i]);
                    }
                }

                // Create result list with exact capacity
                var result = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    result.Add(list[indices[i]]);
                }

                return result;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(indices);
            }
        }

        /// <summary>
        /// Shuffles the list in-place using the Fisher-Yates algorithm.
        /// Thread-safe and memory efficient.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list.</typeparam>
        /// <param name="list">The list to shuffle.</param>
        /// <exception cref="ArgumentNullException">Thrown when list is null.</exception>
        public static void Shuffle<T>(this IList<T> list)
        {
            ArgumentNullException.ThrowIfNull(list, nameof(list));
            if (list.Count <= 1) return;

            var rand = _random.Value;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rand.Next(i + 1);
                if (i != j)
                {
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }
        }

        /// <summary>
        /// Thread-safe batch processing for collections
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The collection to process.</param>
        /// <param name="batchAction">The action to perform on each batch.</param>
        /// <param name="batchSize">The size of each batch.</param>
        /// <exception cref="ArgumentNullException">Thrown when source or batchAction is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when batchSize is less than or equal to 0.</exception>
        public static void ProcessInBatches<T>(this IEnumerable<T> source, Action<IList<T>> batchAction, int batchSize = 100)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));
            ArgumentNullException.ThrowIfNull(batchAction, nameof(batchAction));
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

            var batch = new List<T>(batchSize);
            foreach (var item in source)
            {
                batch.Add(item);
                if (batch.Count >= batchSize)
                {
                    batchAction(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                batchAction(batch);
            }
        }

        /// <summary>
        /// Thread-safe parallel batch processing for collections
        /// </summary>
        /// <typeparam name="T">The type of elements in the collection.</typeparam>
        /// <param name="source">The collection to process.</param>
        /// <param name="batchAction">The action to perform on each batch.</param>
        /// <param name="batchSize">The size of each batch.</param>
        /// <param name="maxDegreeOfParallelism">The maximum number of concurrent batches.</param>
        /// <exception cref="ArgumentNullException">Thrown when source or batchAction is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when batchSize is less than or equal to 0.</exception>
        public static void ProcessInParallelBatches<T>(this IEnumerable<T> source, Action<IList<T>> batchAction, int batchSize = 100, int maxDegreeOfParallelism = -1)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));
            ArgumentNullException.ThrowIfNull(batchAction, nameof(batchAction));
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

            var batches = new ConcurrentQueue<List<T>>();
            var currentBatch = new List<T>(batchSize);

            foreach (var item in source)
            {
                currentBatch.Add(item);
                if (currentBatch.Count >= batchSize)
                {
                    batches.Enqueue(currentBatch);
                    currentBatch = new List<T>(batchSize);
                }
            }

            if (currentBatch.Count > 0)
            {
                batches.Enqueue(currentBatch);
            }

            var options = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = maxDegreeOfParallelism > 0 
                    ? maxDegreeOfParallelism 
                    : Environment.ProcessorCount 
            };

            Parallel.ForEach(batches, options, batch => batchAction(batch));
        }
    }
}
