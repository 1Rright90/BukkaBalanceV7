using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Core
{
    /// <summary>
    /// A simple dependency injection container that manages service registration and resolution.
    /// Thread-safe and optimized for high-performance scenarios.
    /// </summary>
    public sealed class DependencyContainer : IDisposable
    {
        private readonly ConcurrentDictionary<Type, Lazy<object>> _singletons;
        private readonly ConcurrentDictionary<Type, Func<object>> _factories;
        private readonly ConcurrentDictionary<Type, Type> _typeMap;
        private readonly ILogger _logger;
        private readonly object _disposeLock = new object();
        private volatile bool _isDisposed;
        private volatile bool _isInitialized;

        private DependencyContainer()
        {
            _singletons = new ConcurrentDictionary<Type, Lazy<object>>();
            _factories = new ConcurrentDictionary<Type, Func<object>>();
            _typeMap = new ConcurrentDictionary<Type, Type>();
            _logger = new ConsoleLogger(); // Default logger
            RegisterDefaults();
        }

        private static readonly Lazy<DependencyContainer> _instance =
            new Lazy<DependencyContainer>(() => new DependencyContainer(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Gets the singleton instance of the <see cref="DependencyContainer"/>.
        /// </summary>
        public static DependencyContainer Instance => _instance.Value;

        private void RegisterDefaults()
        {
            try
            {
                // Register core services
                RegisterSingleton<ILogger>(() => _logger);
                RegisterSingleton<ITelemetry>(CreateTelemetry);
                RegisterSingleton<IHealthCheck>(CreateHealthCheck);
                RegisterSingleton<IPerformanceMonitor>(CreatePerformanceMonitor);
                RegisterSingleton<IMemoryProfiler>(CreateMemoryProfiler);

                _isInitialized = true;
                _logger.LogDebug("Default dependencies registered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to register default dependencies", ex);
                throw;
            }
        }

        public void RegisterSingleton<TInterface>(Func<object> factory) where TInterface : class
        {
            ThrowIfDisposed();

            var interfaceType = typeof(TInterface);
            _typeMap.TryAdd(interfaceType, interfaceType);

            var lazy = new Lazy<object>(() =>
            {
                try
                {
                    return factory() ?? throw new InvalidOperationException($"Factory for {interfaceType.Name} returned null");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating instance of {interfaceType.Name}", ex);
                    throw;
                }
            }, LazyThreadSafetyMode.ExecutionAndPublication);

            if (!_singletons.TryAdd(interfaceType, lazy))
            {
                _logger.LogWarning($"Singleton registration failed for {interfaceType.Name}");
                throw new InvalidOperationException($"Type {interfaceType.Name} is already registered");
            }

            _logger.LogDebug($"Registered singleton for {interfaceType.Name}");
        }

        public void RegisterTransient<TInterface, TImplementation>()
            where TInterface : class
            where TImplementation : class, TInterface
        {
            ThrowIfDisposed();

            var interfaceType = typeof(TInterface);
            var implementationType = typeof(TImplementation);

            _typeMap.TryAdd(interfaceType, implementationType);
            _factories.TryAdd(interfaceType, () => CreateInstance(implementationType));

            _logger.LogDebug($"Registered transient mapping from {interfaceType.Name} to {implementationType.Name}");
        }

        public T Resolve<T>() where T : class
        {
            ThrowIfDisposed();

            var type = typeof(T);

            if (_singletons.TryGetValue(type, out var singleton))
            {
                return (T)singleton.Value;
            }

            if (_factories.TryGetValue(type, out var factory))
            {
                return (T)factory();
            }

            if (_typeMap.TryGetValue(type, out var implementationType))
            {
                return (T)CreateInstance(implementationType);
            }

            _logger.LogWarning($"No registration found for type {type.Name}");
            throw new InvalidOperationException($"No registration found for type {type.Name}");
        }

        private object CreateInstance(Type type)
        {
            try
            {
                var constructor = type.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (constructor == null)
                {
                    throw new InvalidOperationException($"No accessible constructors found for type {type.Name}");
                }

                var parameters = constructor.GetParameters()
                    .Select(p => ResolveParameter(p))
                    .ToArray();

                return constructor.Invoke(parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating instance of {type.Name}", ex);
                throw;
            }
        }

        private object ResolveParameter(ParameterInfo parameter)
        {
            try
            {
                var parameterType = parameter.ParameterType;

                if (parameter.HasDefaultValue)
                {
                    return parameter.DefaultValue;
                }

                return typeof(DependencyContainer).GetMethod(nameof(Resolve))
                    ?.MakeGenericMethod(parameterType)
                    .Invoke(this, null) ?? throw new InvalidOperationException($"Cannot resolve parameter {parameter.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resolving parameter {parameter.Name}", ex);
                throw;
            }
        }

        private ITelemetry CreateTelemetry()
        {
            return new Telemetry(_logger);
        }

        private IHealthCheck CreateHealthCheck()
        {
            return new HealthCheck(Resolve<ITelemetry>(), _logger);
        }

        private IPerformanceMonitor CreatePerformanceMonitor()
        {
            return new PerformanceMonitor(
                Resolve<IHealthCheck>(),
                Resolve<ITelemetry>(),
                _logger);
        }

        private IMemoryProfiler CreateMemoryProfiler()
        {
            return new MemoryProfiler(_logger);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_disposeLock)
            {
                if (!_isDisposed)
                {
                    try
                    {
                        foreach (var singleton in _singletons.Values)
                        {
                            if (singleton.IsValueCreated && singleton.Value is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }
                        }

                        _singletons.Clear();
                        _factories.Clear();
                        _typeMap.Clear();

                        _logger.LogInformation("Dependency container disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Error disposing dependency container", ex);
                        throw;
                    }
                    finally
                    {
                        _isDisposed = true;
                        _isInitialized = false;
                    }
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(DependencyContainer));
            }
        }
    }
}