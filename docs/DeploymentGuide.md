# YSBCaptain Module Deployment Guide

## System Requirements

- Mount & Blade II: Bannerlord (e1.2.0 or higher)
- Windows Server 2019/2022 or Windows 10/11 (x64 only)
- Dedicated Server Requirements:
  - 4+ CPU cores (8 recommended)
  - 16GB RAM (8GB minimum)
  - 2GB storage
  - 100Mbps network connection

## Development Requirements

### Language and Runtime
- C# 7.3 (strict compatibility mode)
  - No nullable reference types
  - No default interface methods
  - No pattern matching enhancements
  - No switch expressions
  - No range/index operators
- .NET Standard 2.0
- x64 platform only

### Build Environment
- Visual Studio 2019 or later
- Windows 10/11 (x64)
- PowerShell 5.1+
- .NET SDK with .NET Standard 2.0 support

### Code Quality Requirements
- XML documentation for all public APIs
- Warnings treated as errors
- Full static code analysis
- Overflow checking enabled
- Code style enforcement
- No nullable reference types (C# 7.3 compatibility)

## Dependencies

- .NET Standard 2.0 Runtime
- Required Packages:
  - Microsoft.Extensions.DependencyInjection (2.2.0)
  - Microsoft.Extensions.Logging (2.2.0)
  - Serilog (2.10.0)
  - Serilog.Extensions.Logging (3.1.0)
  - Serilog.Sinks.Console (4.1.0)
  - Serilog.Sinks.File (5.0.0)

## Installation

### Module Setup

1. Copy module to the Modules directory:
```
[Bannerlord Server]/Modules/YSBCaptain/
```

2. Verify module structure:
```
[Bannerlord Server]/
└── Modules/
    └── YSBCaptain/
        ├── bin/
        │   └── Win64_Shipping_Server/
        │       └── YSBCaptain.dll
        ├── ModuleData/
        │   ├── module_settings.xml
        │   └── performance_settings.xml
        └── SubModule.xml
```

3. Configure module loading in `DedicatedServerConfiguration.xml`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<ServerConfiguration>
    <Modules>
        <Module Id="Native" />
        <Module Id="SandBoxCore" />
        <Module Id="CustomBattle" />
        <Module Id="SandBox" />
        <Module Id="YSBCaptain" />
    </Modules>
</ServerConfiguration>
```

## Configuration

### Module Settings
Edit `ModuleData/module_settings.xml`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Settings>
    <Formation>
        <MaxUnits>100</MaxUnits>
        <UpdateInterval>1000</UpdateInterval>
    </Formation>
    <Network>
        <BatchSize>64</BatchSize>
        <UpdateRate>20</UpdateRate>
        <Compression>true</Compression>
    </Network>
    <Logging>
        <Level>1</Level>
        <RotationDays>7</RotationDays>
    </Logging>
</Settings>
```

### Performance Settings
Edit `ModuleData/performance_settings.xml`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Settings>
    <System>
        <WorkerThreads>4</WorkerThreads>
        <IOThreads>4</IOThreads>
    </System>
    <Memory>
        <PoolSize>1024</PoolSize>
        <CacheSize>1000</CacheSize>
    </Memory>
    <Network>
        <BufferSize>65536</BufferSize>
        <NoDelay>true</NoDelay>
    </Network>
    <Timer>
        <MaxExecutionTime>5000</MaxExecutionTime>
        <DefaultInterval>1000</DefaultInterval>
        <GracefulShutdownTimeout>5000</GracefulShutdownTimeout>
        <StatisticsRetentionDays>7</StatisticsRetentionDays>
    </Timer>
</Settings>
```

### Timer Configuration
The timer settings control the behavior of periodic tasks and background operations:

- `MaxExecutionTime`: Maximum allowed execution time for timer tasks in milliseconds
- `DefaultInterval`: Default interval between timer ticks in milliseconds
- `GracefulShutdownTimeout`: Time to wait for tasks to complete during shutdown
- `StatisticsRetentionDays`: Number of days to retain execution statistics

## Server Management

### Starting the Server

1. Launch the dedicated server:
```batch
[Bannerlord Server]/bin/Win64_Shipping_Server/TaleWorlds.MountAndBlade.DedicatedCustomServer.exe /dedicatedcustomserverconfigfile DedicatedServerConfiguration.xml
```

2. Verify module loading in server console output

### Monitoring

1. Check server logs in:
```
[Bannerlord Server]/logs/
```

2. Monitor performance metrics in:
```
[Bannerlord Server]/logs/performance/
```

### Maintenance

1. Log Rotation
- Logs are automatically rotated every 7 days
- Old logs are compressed and archived

2. Performance Optimization
- Monitor CPU and memory usage
- Adjust thread and memory settings as needed
- Clean old log files periodically

3. Updates
- Keep server and module versions synchronized
- Test updates in staging environment first
- Maintain backup of configuration files
