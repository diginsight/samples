# CosmosDB Console Application

## Overview

CosmosdbConsole is a .NET 8.0 command-line utility that demonstrates advanced interaction with Azure Cosmos DB. Built with the Diginsight observability framework, this console application provides a comprehensive set of tools for managing Cosmos DB documents, including bulk operations, querying, and data transformations.

## Features

- **JSON Document Management**: Load, upload, and delete JSON documents in bulk
- **Dynamic Querying**: Execute custom SQL queries against Cosmos DB collections
- **Document Transformation**: Transform documents during processing with custom logic
- **Observability Integration**: Full telemetry and logging using Diginsight framework
- **Command-line Interface**: Easy-to-use CLI built with Cocona framework

## Usage

The application provides four main commands:

### 1. Query Cosmos DB
Execute SQL queries against Cosmos DB containers:
dotnet run -- query -c "connection_string" -q "SELECT * FROM c" -d "database_name" -t "container_name" -f "output.json" --top 50
**Parameters:**
- `-c, --connectionString`: Cosmos DB connection string
- `-q, --query`: SQL query to execute
- `-d, --database`: Database name
- `-t, --collection`: Container/collection name
- `-f, --file`: Optional output file for results
- `--top`: Maximum items to return
- `--skip`: Number of items to skip

### 2. Upload JSON Documents
Bulk upload JSON documents to Cosmos DB:
dotnet run -- uploadjson -f "documents.json" -c "connection_string" -d "database_name" -t "container_name" -s "skipField1,skipField2"
**Parameters:**
- `-f, --filePath`: Path to JSON file containing documents
- `-c, --connectionString`: Cosmos DB connection string
- `-d, --database`: Target database name
- `-t, --collection`: Target container name
- `-s, --skipFields`: Fields to exclude from upload

### 3. Load JSON Documents (Stream Processing)
Streams and processes JSON documents without uploading to Cosmos DB:
dotnet run -- loadjson -f "path/to/documents.json" --top 100 --skip 0 -s "field1,field2"
**Parameters:**
- `-f, --file`: Path to JSON file containing documents
- `--top`: Maximum number of documents to process (default: -1 for all)
- `--skip`: Number of documents to skip (default: 0)
- `-s, --skipFields`: Comma-separated list of fields to exclude

### 4. Delete Documents from JSON
Delete documents listed in a JSON file:
dotnet run -- deletefromjson -f "documents.json" -c "connection_string" -d "database_name" -t "container_name"
## JSON File Format

The application expects JSON files with the following structure:
{
  "Documents": [
    {
      "id": "document1",
      "partitionKey": "partition1",
      "latitude": 40.7128,
      "longitude": -74.0060,
      "data": "sample data"
    },
    {
      "id": "document2",
      "partitionKey": "partition2",
      "latitude": 34.0522,
      "longitude": -118.2437,
      "data": "more data"
    }
  ]
}
## Prerequisites

- .NET 8.0 SDK or later
- Azure Cosmos DB account with connection string
- Valid Cosmos DB database and container

## Installation & Setup

1. Clone the repository and navigate to the project directory
2. Restore dependencies:dotnet restore3. Build the application:dotnet build
## Configuration

The application uses standard .NET configuration with `appsettings.json` and `appsettings.Development.json`. Key configuration sections include:

### Logging Configuration
- **Diginsight Activities**: Advanced activity tracking and telemetry
- **Console Logging**: Colorized console output with custom formatting
- **Log4Net Integration**: File-based logging with rolling file appenders

### Observability Configuration
- **OpenTelemetry**: Distributed tracing and metrics collection
- **Activity Sources**: Tracks Azure Cosmos DB, HTTP requests, and custom activities
- **Azure Monitor**: Optional integration with Azure Application Insights


## Internal Implementation

### Architecture Components

1. **Program.cs**: Entry point with dependency injection and command registration
2. **Executor.cs**: Core business logic for all Cosmos DB operations
3. **ObservabilityExtensions.cs**: Configures logging and telemetry
4. **Observability.cs**: Activity source registration for distributed tracing

### Key Technologies

- **Cocona Framework**: Modern command-line application framework
- **Microsoft.Azure.Cosmos**: Official Cosmos DB .NET SDK
- **Newtonsoft.Json**: JSON processing and manipulation
- **Diginsight Framework**: Advanced observability and diagnostics
- **System.Linq.Async**: Asynchronous LINQ operations

### Document Processing Pipeline

1. **JSON Streaming**: Uses `JsonTextReader` for memory-efficient processing of large files
2. **Document Normalization**: Removes system fields (`_*` properties) and generates IDs
3. **Partition Key Generation**: Automatically creates partition keys from latitude/longitude coordinates
4. **Transformation Logic**: Applies custom transformations during processing
5. **Bulk Operations**: Utilizes Cosmos DB bulk execution for optimal performance

### Observability Features

- **Activity Tracking**: Every operation is traced with structured activities
- **Performance Metrics**: Duration and throughput measurements
- **Error Handling**: Comprehensive exception logging and correlation
- **Configuration-Driven**: Adjustable logging levels and output destinations

### Document Transformation Logic

The application includes built-in transformation logic that:
- Generates partition keys from geographic coordinates
- Removes Cosmos DB system properties
- Creates missing document IDs
- Applies custom field filtering

## Error Handling

The application provides robust error handling:
- Connection string validation
- JSON format validation
- Cosmos DB operation error handling
- File I/O exception management
- Detailed error logging with correlation IDs

## Performance Considerations

- **Streaming Processing**: Handles large JSON files without loading entire content into memory
- **Bulk Operations**: Uses Cosmos DB bulk APIs for optimal throughput
- **Configurable Batch Sizes**: Supports pagination with `top` and `skip` parameters
- **Observable Operations**: All operations are instrumented for performance monitoring

## Dependencies

### Core Dependencies
- **Microsoft.Azure.Cosmos**: Azure Cosmos DB client library
- **Cocona**: Command-line application framework
- **Newtonsoft.Json**: JSON serialization and processing

### Diginsight Framework
- **Diginsight.Components**: Core components and utilities
- **Diginsight.Components.Azure**: Azure-specific extensions
- **Diginsight.Diagnostics**: Advanced diagnostics and telemetry
- **Diginsight.Diagnostics.Log4Net**: Log4Net integration

## Example Scenarios

### Migrating Data# Export data from source
dotnet run -- query -c "source_connection" -q "SELECT * FROM c" -d "sourcedb" -t "sourcecollection" -f "export.json"

# Import to destination
dotnet run -- uploadjson -f "export.json" -c "dest_connection" -d "destdb" -t "destcollection"
### Data Validation# Process and validate without uploading
dotnet run -- loadjson -f "data.json" --top 1000
### Selective Data Operations# Upload while excluding sensitive fields
dotnet run -- uploadjson -f "data.json" -c "connection" -d "db" -t "collection" -s "_ts,_etag,_self"
## Contributing

This application is part of the Diginsight samples collection, demonstrating best practices for:
- Azure Cosmos DB integration
- Command-line application development
- Observability implementation
- Bulk data processing

## License

This project is part of the Diginsight framework and follows the same licensing terms.
