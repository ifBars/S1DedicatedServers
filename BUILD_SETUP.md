# Build Setup Guide

This guide helps contributors set up their local development environment for the DedicatedServerMod project.

## Prerequisites

- .NET SDK (for netstandard2.1)
- Visual Studio, Rider, or VS Code with C# support
- Schedule I game installed

## First-Time Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd DedicatedServerMod
```

### 2. Configure Local Build Paths

The project uses a `local.build.props` file for user-specific paths. This file is git-ignored so each contributor can have their own configuration.

1. Copy the example template:
   ```bash
   copy local.build.props.example local.build.props
   ```

2. Edit `local.build.props` and update the paths to match your local environment:
   ```xml
   <PropertyGroup>
       <MonoGamePath>YOUR_PATH_HERE</MonoGamePath>
       <Il2CppGamePath>YOUR_PATH_HERE</Il2CppGamePath>
   </PropertyGroup>
   ```

   **Example paths:**
   - `C:\Program Files (x86)\Steam\steamapps\common\Schedule I`
   - `D:\Games\Schedule I`

### 3. Restore NuGet Packages

The project uses Krafs.Publicizer to automatically publicize the Assembly-CSharp.dll at build time, eliminating the need for manually creating a publicized DLL.

```bash
dotnet restore
```

## Build Configurations

The project has four build configurations:

- **Mono_Client**: Mono build for client-side testing
- **Mono_Server**: Mono build for server-side
- **Il2cpp_Client**: IL2CPP build for client-side testing
- **Il2cpp_Server**: IL2CPP build for server-side

### Building

```bash
# Build a specific configuration
dotnet build -c Mono_Server

# Build all configurations
dotnet build
```

## How It Works

### Assembly Publicization

Previously, contributors needed to manually create `Assembly-CSharp-publicized.dll` using a separate tool. Now:

1. **Krafs.Publicizer** NuGet package is included in the project
2. During build, it automatically publicizes `Assembly-CSharp.dll`
3. References to Assembly-CSharp have `Publicize="true"` metadata
4. No manual steps required!

### Path Management

- **local.build.props**: Your personal game installation paths (git-ignored)
- **local.build.props.example**: Template with example paths (committed to git)
- **.csproj**: References `$(MonoGamePath)` and `$(Il2CppGamePath)` from your local.build.props

## Troubleshooting

### Build fails with "Could not find Assembly-CSharp.dll"

**Solution**: Make sure your `local.build.props` paths point to the correct game installation directory.

### Build fails with missing NuGet packages

**Solution**: Run `dotnet restore` to download all required packages.

### Changes to local.build.props aren't detected

**Solution**: Clean and rebuild the project:
```bash
dotnet clean
dotnet build -c <configuration>
```

## Contributing

When contributing:

1. **Never commit** your `local.build.props` file (it's git-ignored)
2. **Do commit** changes to `local.build.props.example` if you add new path properties
3. Test your changes with both Mono and IL2CPP configurations when possible

## Additional Resources

- [Krafs.Publicizer Documentation](https://github.com/krafs/Publicizer)
- [MSBuild Property Reference](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-properties)
