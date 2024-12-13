# YSBCaptain

A performance monitoring and profiling library for .NET applications, specifically designed for Mount & Blade II: Bannerlord.

## Project Specifications

### Development Constraints
- **C# Language Version**: Strictly C# 7.3 features only
- **Runtime Environment**: Full compatibility with Mount & Blade II: Bannerlord's runtime
- **Code Quality**:
  - Enforced code style and analysis across the project
  - Warnings treated as errors
  - Full static code analysis enabled
  - Safe runtime behavior with overflow checking
- **Documentation**: XML documentation required for all public APIs
- **Native Code Reference**: All development must align with TaleWorlds' Native code patterns
  - Direct reference to Native_codes for consistent implementation
  - Follows TaleWorlds' coding conventions and patterns
  - Maintains compatibility with core game systems

### Technical Requirements

- **.NET Standard 2.0**: Required for Bannerlord compatibility
- **x64 Platform**: Targeting Windows x64 architecture
- **Development Environment**:
  - Visual Studio 2019 or later recommended
  - Windows build environment
  - Access to Native_codes reference implementation

## Requirements

- .NET SDK (with .NET Standard 2.0 Support)
Required to build assemblies compatible with Bannerlord’s modding APIs, which target .NET Standard 2.0.

- C# 7.3 Language Support
Ensures the project uses a language version fully compatible with Bannerlord’s existing codebase and modding guidelines.

- Mount & Blade II: Bannerlord Dedicated Server Files
Access to the official Bannerlord dedicated server binaries and server-side DLLs is essential for referencing necessary assemblies and testing server-only logic.

- Windows Environment (with PowerShell 5.1 or Later)
Recommended for build scripts, automated deployments, and integration with Bannerlord’s Windows-based dedicated server environment.

- Visual Studio 2019 or Compatible IDE
Provides a stable, well-integrated environment for C#, supports NuGet, and simplifies debugging and code navigation when working with Bannerlord assemblies.

## Features

- Memory profiling and monitoring
- Performance metrics collection
- Resource management
- Configuration management
- Health monitoring
- Telemetry support

## Installation

1. Clone the repository:
```powershell
git clone [repository-url]
```

2. Ensure you have the required dependencies:
   - Visual Studio 2019 or later
   - .NET SDK with .NET Standard 2.0 support
   - PowerShell 5.1 or later

## Project Structure

- `/Core` - Core interfaces and base implementations
- `/Performance` - Performance monitoring components
- `/Scripts` - PowerShell scripts for analysis and maintenance
- `/Tests` - Unit tests and integration tests

## Development Setup

1. Clone the repository
2. Open the solution in Visual Studio
3. Build the solution
4. Run the threading analysis:
```powershell
.\Scripts\check_threading.ps1 -ProjectDir $PWD
```

## Contributing

1. Fork the repository
2. Create your feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Compatibility Notes

- Specifically designed for Mount & Blade II: Bannerlord
- Uses C# 7.3 language features
- Targets .NET Standard 2.0
- Compatible with PowerShell 5.1 and later
