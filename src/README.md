# Diginsight Configuration Extensions

## Overview

This document explains the `HostBuilderExtensions` class and its `ConfigureAppConfiguration2` method, which provides sophisticated configuration management capabilities for .NET applications. The system supports advanced configuration loading with external folder support, Azure Key Vault integration, and intelligent file resolution.

## Key Features

- **Multi-Environment Configuration**: Supports different environments (Development, Production, etc.)
- **External Configuration Folder**: Allows configurations to be stored outside the application directory
- **Azure Key Vault Integration**: Automatically loads secrets from Azure Key Vault
- **Local Development Support**: Special handling for local development scenarios
- **Intelligent File Resolution**: Hierarchical search for configuration files
- **Tag-Based Filtering**: Supports filtering Key Vault secrets by tags

## Core Components

### 1. HostBuilderExtensions

The main entry point for configuration setup. Provides extension methods for `IHostBuilder` and `IWebHostBuilder`.

```csharp
public static IHostBuilder ConfigureAppConfiguration2(
    this IHostBuilder hostBuilder, 
    ILoggerFactory loggerFactory, 
    Func<IDictionary<string, string>, bool>? tagsMatch = null)
```

### 2. ConfigureAppConfiguration2 Method

The core configuration method that orchestrates the entire configuration loading process.

## Configuration Loading Process

### Phase 1: Environment Detection
1. Determines if running in Development environment (`isLocal`)
2. Checks if debugger is attached (`isDebuggerAttached`)
3. Gets environment name from `IHostEnvironment.EnvironmentName`

### Phase 2: Base Configuration Files
1. **appsettings.json**: Base configuration file
2. **appsettings.local.json**: Local override (Development only)
3. **appsettings.{Environment}.json**: Environment-specific configuration

### Phase 3: External Configuration Resolution

The system supports loading configurations from an external folder specified by the `ExternalConfigurationFolder` environment variable.

#### External Configuration Search Algorithm

When `ExternalConfigurationFolder` is set and exists, the system performs a hierarchical search:

1. **Repository Root Detection**: Uses `DirectoryHelper.GetRepositoryRoot()` to find the git repository root
2. **Path Decomposition**: Breaks down the current directory path relative to the repository root
3. **Hierarchical Search**: Searches for configuration files in the following order:

```
Example: If current directory is:
E:\dev.darioa.live\Diginsight\samples\src\03.00 AzureStorage\01 TableStorage\bin\Debug

And external folder is:
E:\dev.darioa.live\Diginsight\samples.internal

The search order is:
1. E:\dev.darioa.live\Diginsight\samples.internal\src\03.00 AzureStorage\01 TableStorage\bin\Debug
2. E:\dev.darioa.live\Diginsight\samples.internal\src\03.00 AzureStorage\01 TableStorage\bin
3. E:\dev.darioa.live\Diginsight\samples.internal\src\03.00 AzureStorage\01 TableStorage
4. E:\dev.darioa.live\Diginsight\samples.internal\src\03.00 AzureStorage
5. E:\dev.darioa.live\Diginsight\samples.internal\src
6. E:\dev.darioa.live\Diginsight\samples.internal
```

### Phase 4: Environment Variable Overrides

The system supports custom environment names through the `AppsettingsEnvironmentName` environment variable:

```csharp
string? appsettingsEnvironmentName = Environment.GetEnvironmentVariable("AppsettingsEnvironmentName") ?? environmentName;
```

This allows using different configuration files than the default environment name.

### Phase 5: Azure Key Vault Integration

If Azure Key Vault configuration is present, the system automatically integrates it:

#### Required Configuration Keys:
- `AzureKeyVault:Uri`: Key Vault URI
- `AzureKeyVault:ClientId`: Azure AD Client ID
- `AzureKeyVault:TenantId`: Azure AD Tenant ID
- `AzureKeyVault:ClientSecret`: Azure AD Client Secret

#### Authentication Methods:
The `ApplicationCredentialProvider` supports multiple authentication methods:

**Development Environment:**
- `AzureCliCredential`: Uses Azure CLI authentication
- `VisualStudioCodeCredential`: Uses VS Code authentication
- `VisualStudioCredential`: Uses Visual Studio authentication
- `ClientSecretCredential`: Uses client secret if provided

**Production Environment:**
- `ManagedIdentityCredential`: Uses managed identity
- `ClientSecretCredential`: Uses client secret if provided

#### Authority Host Support:
The system automatically detects China regions and sets the appropriate authority host:
```csharp
if (appsettingsEnvName.EndsWith("cn", StringComparison.OrdinalIgnoreCase))
{
    credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureChina;
}
```

### Phase 6: Configuration Source Ordering

The system ensures proper precedence by reordering configuration sources:
1. JSON files (in order of importance)
2. Azure Key Vault secrets
3. Environment variables (highest priority)

## Helper Components

### DirectoryHelper
Provides utility methods for finding repository roots:
```csharp
public static string? GetRepositoryRoot(string currentDirectory)
```

### ApplicationCredentialProvider
Manages Azure authentication credentials with fallback chains for different environments.

### KeyVaultSecretManager2
Custom Key Vault secret manager that:
- Converts secret names to configuration keys
- Supports tag-based filtering
- Handles secret expiration and activation dates
- Converts Key Vault naming conventions to .NET configuration format

#### Key Name Conversion:
- `--` ? `:`
- `-x{hex}` ? Unicode character
- `-u{hex}` ? Unicode character

Example: `MyApp--Database--ConnectionString` becomes `MyApp:Database:ConnectionString`

## Environment Variables

### Core Variables:
- `DOTNET_ENVIRONMENT`: Sets the application environment
- `ExternalConfigurationFolder`: Path to external configuration folder
- `AppsettingsEnvironmentName`: Override for environment-specific configuration file names

### Azure Key Vault Variables:
- `AzureKeyVault:Uri`: Key Vault URI
- `AzureKeyVault:ClientId`: Azure AD Client ID
- `AzureKeyVault:TenantId`: Azure AD Tenant ID
- `AzureKeyVault:ClientSecret`: Azure AD Client Secret

## Usage Examples

### Basic Usage:
```csharp
Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration2(loggerFactory)
    .Build();
```

### With Tag Filtering:
```csharp
Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration2(loggerFactory, tags => tags.ContainsKey("AppSettings"))
    .Build();
```

### For Web Applications:
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureAppConfiguration2(loggerFactory);
```

## Configuration File Examples

### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AzureKeyVault": {
    "Uri": "https://myvault.vault.azure.net/"
  }
}
```

### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "AzureKeyVault": {
    "ClientId": "your-client-id",
    "TenantId": "your-tenant-id",
    "ClientSecret": "your-client-secret"
  }
}
```

### appsettings.local.json (Development Only)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyApp;Trusted_Connection=true;"
  }
}
```

## Best Practices

1. **Environment Variables**: Use environment variables for sensitive configuration in production
2. **External Folders**: Keep sensitive configurations in external folders for security
3. **Tag Filtering**: Use Key Vault tags to organize and filter secrets
4. **Local Development**: Use `.local.json` files for local development overrides
5. **Logging**: Enable detailed logging during development for configuration troubleshooting

## Security Considerations

1. **Secret Management**: Never commit secrets to source control
2. **External Folders**: Ensure external configuration folders have proper access controls
3. **Key Vault**: Use managed identities in production environments
4. **Local Files**: Add `*.local.json` to `.gitignore`

## Troubleshooting

### Common Issues:
1. **Configuration Not Found**: Check external folder path and permissions
2. **Key Vault Access**: Verify authentication credentials and permissions
3. **File Resolution**: Enable debug logging to trace file search paths
4. **Environment Variables**: Ensure all required environment variables are set

### Debug Logging:
The system provides extensive debug logging. Enable it in your configuration:
```json
{
  "Logging": {
    "LogLevel": {
      "Diginsight.Components.Configuration": "Debug"
    }
  }
}
```

This comprehensive configuration system provides flexibility, security, and maintainability for complex application deployment scenarios.