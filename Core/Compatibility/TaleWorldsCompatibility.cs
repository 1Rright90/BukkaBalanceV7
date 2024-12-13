using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.MountAndBlade.Multiplayer;

namespace YSBCaptain.Core.Compatibility
{
    /// <summary>
    /// Provides a thread-safe compatibility layer between YSBCaptain and TaleWorlds core libraries.
    /// This class manages object creation, caching, and cleanup for TaleWorlds components.
    /// </summary>
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public static class TaleWorldsCompatibility
    {
        private static readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<Type, WeakReference<object>> _objectCache = new ConcurrentDictionary<Type, WeakReference<object>>();
        private static readonly ConcurrentDictionary<GameEntity, byte> _managedEntities = new ConcurrentDictionary<GameEntity, byte>();
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMs = 100;

        /// <summary>
        /// Ensures thread-safe access to TaleWorlds objects with retry logic and proper error handling.
        /// </summary>
        /// <typeparam name="T">Type of TaleWorlds object to retrieve</typeparam>
        /// <param name="allowNull">If true, allows returning null when object cannot be created</param>
        /// <returns>Instance of requested TaleWorlds object or null if allowNull is true</returns>
        /// <exception cref="TaleWorldsApiException">Thrown when object creation fails and allowNull is false</exception>
        public static async Task<T> GetTaleWorldsObjectAsync<T>(bool allowNull = false) where T : class
        {
            T instance = null;
            Exception lastException = null;

            for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                try
                {
                    using (await _asyncLock.LockAsync().ConfigureAwait(false))
                    {
                        if (_objectCache.TryGetValue(typeof(T), out var weakRef))
                        {
                            if (weakRef.TryGetTarget(out var cachedInstance))
                            {
                                instance = cachedInstance as T;
                                if (instance != null)
                                {
                                    return instance;
                                }
                            }
                            // Remove dead reference
                            _objectCache.TryRemove(typeof(T), out _);
                        }

                        instance = await CreateTaleWorldsInstanceAsync<T>().ConfigureAwait(false);
                        if (instance != null)
                        {
                            _objectCache.TryAdd(typeof(T), new WeakReference<object>(instance));
                            return instance;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < MaxRetryAttempts - 1)
                    {
                        await Task.Delay(RetryDelayMs * (attempt + 1)).ConfigureAwait(false); // Exponential backoff
                        continue;
                    }
                }
            }

            if (!allowNull && instance == null)
            {
                throw new TaleWorldsApiException(
                    $"Failed to create or retrieve TaleWorlds instance of type {typeof(T).Name} after {MaxRetryAttempts} attempts",
                    lastException);
            }

            return instance;
        }

        /// <summary>
        /// Creates a new instance of a TaleWorlds object with proper initialization and validation.
        /// </summary>
        /// <typeparam name="T">Type of TaleWorlds object to create</typeparam>
        /// <returns>New instance of the requested type</returns>
        /// <exception cref="TaleWorldsApiException">Thrown when object creation fails</exception>
        private static async Task<T> CreateTaleWorldsInstanceAsync<T>() where T : class
        {
            try
            {
                if (typeof(T) == typeof(GameEntity))
                {
                    var scene = Scene.Current;
                    if (scene == null)
                    {
                        throw new TaleWorldsApiException("Cannot create GameEntity: No active scene");
                    }

                    var entity = GameEntity.CreateEmpty(scene);
                    if (entity != null)
                    {
                        _managedEntities.TryAdd(entity, 0);
                    }
                    return entity as T;
                }

                if (typeof(T).IsSubclassOf(typeof(GameManager)))
                {
                    var manager = GameManager.Current;
                    if (manager == null)
                    {
                        throw new TaleWorldsApiException("Cannot retrieve GameManager: No active manager");
                    }
                    return manager as T;
                }

                var instance = Activator.CreateInstance<T>();
                if (instance is IAsyncInitializable asyncInit)
                {
                    await asyncInit.InitializeAsync().ConfigureAwait(false);
                }
                return instance;
            }
            catch (Exception ex)
            {
                throw new TaleWorldsApiException($"Failed to create instance of type {typeof(T).Name}", ex);
            }
        }

        /// <summary>
        /// Ensures proper cleanup of TaleWorlds resources with enhanced entity management.
        /// </summary>
        public static async Task CleanupTaleWorldsResourcesAsync()
        {
            try
            {
                // Clean up managed entities
                foreach (var entity in _managedEntities.Keys)
                {
                    try
                    {
                        if (entity != null && !entity.IsRemoved)
                        {
                            entity.Remove();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to remove entity: {ex.Message}");
                    }
                }
                _managedEntities.Clear();

                // Clean up cached references
                foreach (var kvp in _objectCache)
                {
                    try
                    {
                        if (kvp.Value.TryGetTarget(out var target))
                        {
                            if (target is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                            else if (target is GameEntity entity && !entity.IsRemoved)
                            {
                                entity.Remove();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to cleanup reference: {ex.Message}");
                    }
                }
                _objectCache.Clear();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to cleanup TaleWorlds resources: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the current context has a valid Mission instance.
        /// </summary>
        /// <returns>True if in a valid mission context, false otherwise.</returns>
        public static bool IsInValidMissionContext()
        {
            try
            {
                return Mission.Current != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the current context has a valid Scene instance.
        /// </summary>
        /// <returns>True if in a valid scene context, false otherwise.</returns>
        public static bool IsInValidSceneContext()
        {
            try
            {
                return Scene.Current != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Custom exception for TaleWorlds API-related errors with enhanced details.
    /// </summary>
    public sealed class TaleWorldsApiException : Exception
    {
        public TaleWorldsApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Simple logging utility for TaleWorlds compatibility layer.
    /// </summary>
    internal static class Logger
    {
        internal static void Error(string message)
        {
            Console.Error.WriteLine($"[ERROR] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}: {message}");
        }
    }
}