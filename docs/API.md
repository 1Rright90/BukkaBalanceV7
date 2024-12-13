# YSBCaptain API Reference

## Core Systems

### Performance Monitoring

```csharp
/// <summary>
/// Interface for performance monitoring and metrics collection.
/// </summary>
public interface IPerformanceMonitor
{
    /// <summary>
    /// Initializes the performance monitor with specified options.
    /// </summary>
    /// <param name="options">Monitoring configuration options.</param>
    Task Initialize(MonitoringOptions options);

    /// <summary>
    /// Starts the performance monitoring.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the performance monitoring.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current performance metrics.
    /// </summary>
    /// <returns>Current performance metrics.</returns>
    Task<PerformanceMetrics> GetMetricsAsync();

    /// <summary>
    /// Records a custom metric.
    /// </summary>
    /// <param name="name">Name of the metric.</param>
    /// <param name="value">Value to record.</param>
    void RecordMetric(string name, double value);
}

### Formation Management

```csharp
/// <summary>
/// Provides methods for managing formations.
/// </summary>
public interface IFormationController
{
    /// <summary>
    /// Creates a new formation.
    /// </summary>
    /// <param name="team">The team that the formation belongs to.</param>
    /// <param name="formationType">The type of formation to create.</param>
    /// <returns>The created formation.</returns>
    Formation Create(Team team, FormationClass formationType);

    /// <summary>
    /// Updates the position of a formation.
    /// </summary>
    /// <param name="formation">The formation to update.</param>
    /// <param name="position">The new position of the formation.</param>
    void Update(Formation formation, Vec3 position);

    /// <summary>
    /// Removes a formation.
    /// </summary>
    /// <param name="formation">The formation to remove.</param>
    void Remove(Formation formation);

    /// <summary>
    /// Gets the number of units in a formation.
    /// </summary>
    /// <param name="formation">The formation to get the unit count for.</param>
    /// <returns>The number of units in the formation.</returns>
    int GetUnitCount(Formation formation);

    /// <summary>
    /// Gets the spacing between units in a formation.
    /// </summary>
    /// <param name="formation">The formation to get the unit spacing for.</param>
    /// <returns>The spacing between units in the formation.</returns>
    float GetUnitSpacing(Formation formation);

    /// <summary>
    /// Gets the center position of a formation.
    /// </summary>
    /// <param name="formation">The formation to get the center position for.</param>
    /// <returns>The center position of the formation.</returns>
    Vec3 GetCenter(Formation formation);

    /// <summary>
    /// Checks if a formation is valid.
    /// </summary>
    /// <param name="formation">The formation to check.</param>
    /// <returns>True if the formation is valid, false otherwise.</returns>
    bool IsValid(Formation formation);

    /// <summary>
    /// Gets the state of a formation.
    /// </summary>
    /// <param name="formation">The formation to get the state for.</param>
    /// <returns>The state of the formation.</returns>
    FormationState GetState(Formation formation);
}

### Unit Management

```csharp
/// <summary>
/// Provides methods for managing units.
/// </summary>
public interface IUnitController
{
    /// <summary>
    /// Creates a new unit.
    /// </summary>
    /// <param name="formation">The formation that the unit belongs to.</param>
    /// <param name="position">The position of the unit.</param>
    /// <returns>The created unit.</returns>
    Agent CreateUnit(Formation formation, Vec3 position);

    /// <summary>
    /// Removes a unit.
    /// </summary>
    /// <param name="unit">The unit to remove.</param>
    void RemoveUnit(Agent unit);

    /// <summary>
    /// Sets the behavior of a unit.
    /// </summary>
    /// <param name="unit">The unit to set the behavior for.</param>
    /// <param name="behavior">The behavior to set.</param>
    void SetBehavior(Agent unit, AgentBehavior behavior);

    /// <summary>
    /// Sets the target of a unit.
    /// </summary>
    /// <param name="unit">The unit to set the target for.</param>
    /// <param name="target">The target to set.</param>
    void SetTarget(Agent unit, Vec3 target);

    /// <summary>
    /// Gets the state of a unit.
    /// </summary>
    /// <param name="unit">The unit to get the state for.</param>
    /// <returns>The state of the unit.</returns>
    AgentState GetState(Agent unit);

    /// <summary>
    /// Gets the health of a unit.
    /// </summary>
    /// <param name="unit">The unit to get the health for.</param>
    /// <returns>The health of the unit.</returns>
    float GetHealth(Agent unit);
}

### Network Management

```csharp
/// <summary>
/// Provides methods for managing the network.
/// </summary>
public interface INetworkController
{
    /// <summary>
    /// Configures the batch size of the network.
    /// </summary>
    /// <param name="size">The batch size to set.</param>
    void ConfigureBatchSize(int size);

    /// <summary>
    /// Configures the update rate of the network.
    /// </summary>
    /// <param name="rate">The update rate to set.</param>
    void ConfigureUpdateRate(int rate);

    /// <summary>
    /// Enables or disables compression on the network.
    /// </summary>
    /// <param name="enable">True to enable compression, false to disable.</param>
    void SetCompression(bool enable);

    /// <summary>
    /// Gets the network statistics.
    /// </summary>
    /// <returns>The network statistics.</returns>
    NetworkStats GetStats();

    /// <summary>
    /// Gets the latency of a network peer.
    /// </summary>
    /// <param name="peer">The peer to get the latency for.</param>
    /// <returns>The latency of the peer.</returns>
    float GetPeerLatency(NetworkPeer peer);

    /// <summary>
    /// Registers an event handler for network events.
    /// </summary>
    /// <param name="handler">The event handler to register.</param>
    void RegisterEventHandler(NetworkEventHandler handler);
}

## Core Components

### Configuration

```csharp
/// <summary>
/// Provides methods for managing configuration.
/// </summary>
public interface IConfiguration
{
    /// <summary>
    /// Gets a configuration value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key of the value.</param>
    /// <returns>The value.</returns>
    T Get<T>(string key);

    /// <summary>
    /// Gets a configuration value with a default value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key of the value.</param>
    /// <param name="defaultValue">The default value to return if the key is not found.</param>
    /// <returns>The value.</returns>
    T Get<T>(string key, T defaultValue);

    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key of the value.</param>
    /// <param name="value">The value to set.</param>
    void Set<T>(string key, T value);

    /// <summary>
    /// Tries to get a configuration value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key of the value.</param>
    /// <param name="value">The value to return if the key is found.</param>
    /// <returns>True if the key is found, false otherwise.</returns>
    bool TryGet<T>(string key, out T value);

    /// <summary>
    /// Saves the configuration changes.
    /// </summary>
    void SaveChanges();
}

### Resource Management

```csharp
/// <summary>
/// Provides methods for managing resources.
/// </summary>
public interface IResourceController
{
    /// <summary>
    /// Registers a resource.
    /// </summary>
    /// <param name="id">The ID of the resource.</param>
    /// <param name="resource">The resource to register.</param>
    void Register(string id, IDisposable resource);

    /// <summary>
    /// Unregisters a resource.
    /// </summary>
    /// <param name="id">The ID of the resource.</param>
    void Unregister(string id);

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    void Cleanup();

    /// <summary>
    /// Gets a resource.
    /// </summary>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <param name="id">The ID of the resource.</param>
    /// <returns>The resource.</returns>
    T Get<T>(string id) where T : class;

    /// <summary>
    /// Checks if a resource is registered.
    /// </summary>
    /// <param name="id">The ID of the resource.</param>
    /// <returns>True if the resource is registered, false otherwise.</returns>
    bool Contains(string id);
}

### Performance Monitoring

```csharp
/// <summary>
/// Provides methods for monitoring performance.
/// </summary>
public interface IPerformanceController
{
    /// <summary>
    /// Tracks a metric.
    /// </summary>
    /// <param name="metric">The metric to track.</param>
    /// <param name="value">The value of the metric.</param>
    void Track(string metric, float value);

    /// <summary>
    /// Increments a counter.
    /// </summary>
    /// <param name="counter">The counter to increment.</param>
    void Increment(string counter);

    /// <summary>
    /// Gets the average value of a metric.
    /// </summary>
    /// <param name="metric">The metric to get the average value for.</param>
    /// <returns>The average value of the metric.</returns>
    float GetAverageValue(string metric);

    /// <summary>
    /// Resets counters.
    /// </summary>
    void ResetCounters();

    /// <summary>
    /// Gets the performance statistics.
    /// </summary>
    /// <returns>The performance statistics.</returns>
    PerformanceStats GetStats();
}

## Code Style Guidelines

### C# 7.3 Compatibility
All code must be compatible with C# 7.3. This means:
- Use explicit null checks instead of null-conditional operators
- Use method overloads instead of optional parameters where possible
- Use traditional foreach loops instead of LINQ where performance is critical
- Use explicit type declarations instead of var where clarity is needed
- Use manual null checks instead of null-coalescing operators

### Example Patterns

```csharp
// Correct - C# 7.3 compatible null checking
public void ProcessFormation(Formation formation)
{
    if (formation == null)
    {
        throw new ArgumentNullException(nameof(formation));
    }
    // Process formation
}

// Correct - C# 7.3 compatible method overloads
public void UpdateFormation(Formation formation)
{
    UpdateFormation(formation, Vector3.Zero);
}

public void UpdateFormation(Formation formation, Vector3 position)
{
    if (formation == null)
    {
        throw new ArgumentNullException(nameof(formation));
    }
    // Update formation
}

// Correct - Traditional foreach for performance
public int CountUnits(Formation formation)
{
    int count = 0;
    foreach (var unit in formation.Units)
    {
        if (unit.IsAlive)
        {
            count++;
        }
    }
    return count;
}
