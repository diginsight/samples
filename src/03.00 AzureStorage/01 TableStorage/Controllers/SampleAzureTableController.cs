using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using Microsoft.Graph;
using Diginsight.Diagnostics;
using Azure.Data.Tables;
using Azure;
using System.Text.Json;

namespace TableStorageSampleAPI.Controllers
{
    /// <summary>
    /// Controller for managing Azure Table Storage operations with support for strongly-typed and dynamic entities.
    /// Provides CRUD operations, JSON serialization, and flexible property naming conventions.
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    public class SampleAzureTableController : ControllerBase
    {
        private readonly GraphServiceClient graphServiceClient;
        private readonly TableServiceClient tableServiceClient;
        private readonly ILogger<SampleAzureTableController> logger;
        private const string TableName = "SampleTable";

        /// <summary>
        /// Initializes a new instance of the SampleAzureTableController class.
        /// </summary>
        /// <param name="logger">The logger instance for logging operations and errors.</param>
        /// <param name="graphServiceClient">The Microsoft Graph service client for identity operations.</param>
        /// <param name="tableServiceClient">The Azure Table Storage service client for data operations.</param>
        public SampleAzureTableController(ILogger<SampleAzureTableController> logger,
            GraphServiceClient graphServiceClient,
            TableServiceClient tableServiceClient)
        {
            this.logger = logger;
            this.graphServiceClient = graphServiceClient;
            this.tableServiceClient = tableServiceClient;
        }

        /// <summary>
        /// Retrieves a collection of records from Azure Table Storage using strongly-typed entities.
        /// </summary>
        /// <param name="filter">Optional OData filter expression to filter results.</param>
        /// <param name="top">Optional maximum number of records to return.</param>
        /// <param name="select">Optional comma-separated list of properties to select.</param>
        /// <returns>A collection of SampleAzureTableRecord entities.</returns>
        /// <response code="200">Returns the list of records.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SampleAzureTableRecord>>> GetRecords(
            [FromQuery] string? filter = null,
            [FromQuery] int? top = null,
            [FromQuery] string? select = null)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { filter, top, select });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                var records = new List<SampleAzureTableRecord>();

                var queryOptions = new QueryOptions();
                if (!string.IsNullOrEmpty(filter)) { queryOptions.Filter = filter; }
                if (top.HasValue && top.Value > 0) { queryOptions.Top = top.Value; }
                if (!string.IsNullOrEmpty(select)) { queryOptions.Select = select.Split(',').Select(s => s.Trim()).ToList(); }

                var asyncPageableRecords = tableClient.QueryAsync<SampleAzureTableRecord>(
                    filter: queryOptions.Filter,
                    maxPerPage: queryOptions.Top,
                    select: queryOptions.Select);

                await foreach (var entity in asyncPageableRecords)
                {
                    records.Add(entity);
                }

                activity?.SetOutput(records);
                return Ok(records);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting records from Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves a collection of records from Azure Table Storage as dynamic JSON objects with configurable property naming.
        /// </summary>
        /// <param name="filter">Optional OData filter expression to filter results.</param>
        /// <param name="top">Optional maximum number of records to return.</param>
        /// <param name="select">Optional comma-separated list of properties to select.</param>
        /// <param name="namingPolicy">Optional property naming policy for response formatting (CamelCase, KebabCase, etc.).</param>
        /// <returns>A collection of dynamic objects with property names formatted according to the specified naming policy.</returns>
        /// <response code="200">Returns the list of records as JSON objects.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpGet("json")]
        public async Task<ActionResult<IEnumerable<object>>> GetRecordsAsJson(
            [FromQuery] string? filter = null,
            [FromQuery] int? top = null,
            [FromQuery] string? select = null,
            [FromQuery] PropertyNamingPolicy? namingPolicy = null)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { filter, top, select, namingPolicy });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                var records = new List<object>();

                var queryOptions = new QueryOptions();
                if (!string.IsNullOrEmpty(filter)) { queryOptions.Filter = filter; }
                if (top.HasValue && top.Value > 0) { queryOptions.Top = top.Value; }
                if (!string.IsNullOrEmpty(select)) { queryOptions.Select = select.Split(',').Select(s => s.Trim()).ToList(); }

                var asyncPageableRecords = tableClient.QueryAsync<TableEntity>(
                    filter: queryOptions.Filter,
                    maxPerPage: queryOptions.Top,
                    select: queryOptions.Select);

                await foreach (var entity in asyncPageableRecords)
                {
                    // Convert TableEntity to a dictionary with controlled property naming
                    var record = new Dictionary<string, object?>();

                    foreach (var kvp in entity)
                    {
                        string propertyName = namingPolicy switch
                        {
                            PropertyNamingPolicy.CamelCase => ToCamelCase(kvp.Key),
                            PropertyNamingPolicy.KebabCaseLower => ToKebabCase(kvp.Key),
                            PropertyNamingPolicy.SnakeCaseLower => ToSnakeCase(kvp.Key),
                            PropertyNamingPolicy.KebabCaseUpper => ToKebabCase(kvp.Key).ToUpperInvariant(),
                            PropertyNamingPolicy.SnakeCaseUpper => ToSnakeCase(kvp.Key).ToUpperInvariant(),
                            _ => kvp.Key // Default to original casing
                        };
                        record[propertyName] = kvp.Value;
                    }

                    records.Add(record);
                }

                activity?.SetOutput(records);
                return Ok(records);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting JSON records from Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Retrieves a single record from Azure Table Storage by its partition key and row key.
        /// </summary>
        /// <param name="partitionKey">The partition key of the record to retrieve.</param>
        /// <param name="rowKey">The row key of the record to retrieve.</param>
        /// <returns>The requested SampleAzureTableRecord entity.</returns>
        /// <response code="200">Returns the requested record.</response>
        /// <response code="404">If the record is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpGet("{partitionKey}/{rowKey}")]
        public async Task<ActionResult<SampleAzureTableRecord>> GetRecord(string partitionKey, string rowKey)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                var response = await tableClient.GetEntityAsync<SampleAzureTableRecord>(partitionKey, rowKey);

                activity?.SetOutput(response.Value);
                return Ok(response.Value);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting record from Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates a new record in Azure Table Storage using a strongly-typed entity.
        /// Automatically sets CreatedAt timestamp and generates RowKey and PartitionKey if not provided.
        /// </summary>
        /// <param name="record">The SampleAzureTableRecord entity to create.</param>
        /// <returns>The created record with generated keys and timestamps.</returns>
        /// <response code="201">Returns the newly created record.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost]
        public async Task<ActionResult<SampleAzureTableRecord>> CreateRecord(SampleAzureTableRecord record)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { record });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                // Set timestamps
                record.CreatedAt = DateTime.UtcNow;
                record.UpdatedAt = null;

                // Generate RowKey if not provided
                if (string.IsNullOrEmpty(record.RowKey))
                {
                    record.RowKey = Guid.NewGuid().ToString();
                }

                // Set default partition key if not provided
                if (string.IsNullOrEmpty(record.PartitionKey))
                {
                    record.PartitionKey = "default";
                }

                await tableClient.AddEntityAsync(record);

                activity?.SetOutput(record);
                return CreatedAtAction(nameof(GetRecord), new { partitionKey = record.PartitionKey, rowKey = record.RowKey }, record);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating record in Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates a new dynamic record in Azure Table Storage from a dictionary of key-value pairs.
        /// Automatically handles partition key, row key generation, and timestamp management.
        /// </summary>
        /// <param name="entityData">Dictionary containing the properties and values for the new entity.</param>
        /// <returns>The created entity as a dynamic object with camelCase property names.</returns>
        /// <response code="201">Returns the newly created record.</response>
        /// <response code="409">If an entity with the same PartitionKey and RowKey already exists.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("dynamic")]
        public async Task<ActionResult<object>> CreateDynamicRecord([FromBody] Dictionary<string, object> entityData)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { entityData });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                // Create a new TableEntity
                var entity = new TableEntity();

                // Set default values for required properties
                string partitionKey = "default";
                string rowKey = Guid.NewGuid().ToString();

                // Process the input data
                foreach (var kvp in entityData)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;

                    // Handle special properties
                    switch (key.ToLowerInvariant())
                    {
                        case "partitionkey":
                            partitionKey = value?.ToString() ?? "default";
                            break;
                        case "rowkey":
                            rowKey = value?.ToString() ?? Guid.NewGuid().ToString();
                            break;
                        case "timestamp":
                        case "etag":
                            // Skip these as they're managed by Azure Table Storage
                            continue;
                        default:
                            // Add all other properties to the entity
                            entity[key] = value;
                            break;
                    }
                }

                // Set the partition key and row key
                entity.PartitionKey = partitionKey;
                entity.RowKey = rowKey;

                // Add standard tracking properties if not already present
                if (!entity.ContainsKey("CreatedAt"))
                {
                    entity["CreatedAt"] = DateTime.UtcNow;
                }
                if (!entity.ContainsKey("UpdatedAt"))
                {
                    entity["UpdatedAt"] = null;
                }

                // Insert the entity
                await tableClient.AddEntityAsync(entity);

                // Create response object with proper naming policy
                var responseEntity = new Dictionary<string, object?>();
                foreach (var kvp in entity)
                {
                    string propertyName = ToCamelCase(kvp.Key);
                    responseEntity[propertyName] = kvp.Value;
                }

                activity?.SetOutput(responseEntity);
                return Created($"/{entity.PartitionKey}/{entity.RowKey}", responseEntity);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                return Conflict("An entity with the same PartitionKey and RowKey already exists");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating dynamic record in Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates a new record in Azure Table Storage from a raw JSON string with configurable property naming.
        /// Provides advanced JSON parsing, validation, and flexible response formatting.
        /// </summary>
        /// <param name="jsonString">The JSON string containing the entity data to create.</param>
        /// <param name="namingPolicy">Optional property naming policy for response formatting (default: CamelCase).</param>
        /// <returns>The created entity as a dynamic object with property names formatted according to the specified naming policy.</returns>
        /// <response code="201">Returns the newly created record.</response>
        /// <response code="400">If the JSON string is invalid or not an object.</response>
        /// <response code="409">If an entity with the same PartitionKey and RowKey already exists.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("json")]
        public async Task<ActionResult<object>> CreateRecordAsJson(
            [FromBody] string jsonString,
            [FromQuery] PropertyNamingPolicy? namingPolicy = PropertyNamingPolicy.CamelCase)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { jsonString, namingPolicy });

            try
            {
                // Validate and parse the JSON string
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return BadRequest("JSON string cannot be null or empty");
                }

                JsonDocument jsonDocument;
                try
                {
                    jsonDocument = JsonDocument.Parse(jsonString);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Invalid JSON format provided");
                    return BadRequest($"Invalid JSON format: {ex.Message}");
                }

                using (jsonDocument)
                {
                    if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return BadRequest("JSON must be an object");
                    }

                    var tableClient = tableServiceClient.GetTableClient(TableName);
                    await tableClient.CreateIfNotExistsAsync();

                    // Create a new TableEntity
                    var entity = new TableEntity();

                    // Set default values for required properties
                    string partitionKey = "default";
                    string rowKey = Guid.NewGuid().ToString();

                    // Process the JSON properties
                    foreach (var property in jsonDocument.RootElement.EnumerateObject())
                    {
                        var key = property.Name;
                        var value = ExtractJsonValue(property.Value);

                        // Normalize key for comparison (handle both camelCase and PascalCase)
                        var normalizedKey = key.ToLowerInvariant();

                        // Handle special properties
                        switch (normalizedKey)
                        {
                            case "partitionkey":
                                partitionKey = value?.ToString() ?? "default";
                                break;
                            case "rowkey":
                                rowKey = value?.ToString() ?? Guid.NewGuid().ToString();
                                break;
                            case "timestamp":
                            case "etag":
                                // Skip these as they're managed by Azure Table Storage
                                continue;
                            default:
                                // Convert key to PascalCase for storage (Azure Table Storage standard)
                                string storageKey = ToPascalCase(key);
                                entity[storageKey] = value;
                                break;
                        }
                    }

                    // Set the partition key and row key
                    entity.PartitionKey = partitionKey;
                    entity.RowKey = rowKey;

                    // Add standard tracking properties if not already present
                    if (!entity.ContainsKey("CreatedAt"))
                    {
                        entity["CreatedAt"] = DateTime.UtcNow;
                    }
                    if (!entity.ContainsKey("UpdatedAt"))
                    {
                        entity["UpdatedAt"] = null;
                    }

                    // Insert the entity
                    await tableClient.AddEntityAsync(entity);

                    // Create response object with controlled property naming
                    var responseEntity = new Dictionary<string, object?>();
                    foreach (var kvp in entity)
                    {
                        string propertyName = namingPolicy switch
                        {
                            PropertyNamingPolicy.CamelCase => ToCamelCase(kvp.Key),
                            PropertyNamingPolicy.KebabCaseLower => ToKebabCase(kvp.Key),
                            PropertyNamingPolicy.SnakeCaseLower => ToSnakeCase(kvp.Key),
                            PropertyNamingPolicy.KebabCaseUpper => ToKebabCase(kvp.Key).ToUpperInvariant(),
                            PropertyNamingPolicy.SnakeCaseUpper => ToSnakeCase(kvp.Key).ToUpperInvariant(),
                            _ => kvp.Key // Default to original casing
                        };
                        responseEntity[propertyName] = kvp.Value;
                    }

                    activity?.SetOutput(responseEntity);
                    return Created($"/{entity.PartitionKey}/{entity.RowKey}", responseEntity);
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                return Conflict("An entity with the same PartitionKey and RowKey already exists");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating JSON record in Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates an existing record in Azure Table Storage using a strongly-typed entity.
        /// Automatically updates the UpdatedAt timestamp and ensures partition/row keys match the URL.
        /// </summary>
        /// <param name="partitionKey">The partition key of the record to update.</param>
        /// <param name="rowKey">The row key of the record to update.</param>
        /// <param name="record">The updated SampleAzureTableRecord entity.</param>
        /// <returns>No content on successful update.</returns>
        /// <response code="204">If the record was updated successfully.</response>
        /// <response code="404">If the record to update is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPut("{partitionKey}/{rowKey}")]
        public async Task<IActionResult> UpdateRecord(string partitionKey, string rowKey, SampleAzureTableRecord record)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey, record });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                // Ensure the partition key and row key match the URL
                record.PartitionKey = partitionKey;
                record.RowKey = rowKey;
                record.UpdatedAt = DateTime.UtcNow;

                await tableClient.UpdateEntityAsync(record, ETag.All);

                return NoContent();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating record in Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates an existing record in Azure Table Storage using dynamic data from a dictionary.
        /// Implements optimistic concurrency control and preserves existing properties not included in the update.
        /// </summary>
        /// <param name="partitionKey">The partition key of the record to update.</param>
        /// <param name="rowKey">The row key of the record to update.</param>
        /// <param name="entityData">Dictionary containing the properties and values to update.</param>
        /// <returns>No content on successful update.</returns>
        /// <response code="204">If the record was updated successfully.</response>
        /// <response code="404">If the record to update is not found.</response>
        /// <response code="412">If the record has been modified by another process (concurrency conflict).</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPut("{partitionKey}/{rowKey}/dynamic")]
        public async Task<IActionResult> UpdateDynamicRecord(string partitionKey, string rowKey, [FromBody] Dictionary<string, object> entityData)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey, entityData });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                // First, try to get the existing entity to preserve its ETag for optimistic concurrency
                TableEntity existingEntity;
                try
                {
                    var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
                    existingEntity = response.Value;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    return NotFound();
                }

                // Create updated entity based on existing entity
                var updatedEntity = new TableEntity(partitionKey, rowKey)
                {
                    ETag = existingEntity.ETag // Preserve ETag for optimistic concurrency
                };

                // Copy all existing properties first
                foreach (var kvp in existingEntity)
                {
                    if (kvp.Key != "PartitionKey" && kvp.Key != "RowKey" && kvp.Key != "Timestamp" && kvp.Key != "ETag")
                    {
                        updatedEntity[kvp.Key] = kvp.Value;
                    }
                }

                // Update with new data from request
                foreach (var kvp in entityData)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;

                    // Handle special properties
                    switch (key.ToLowerInvariant())
                    {
                        case "partitionkey":
                        case "rowkey":
                            // Ignore these - they come from the URL
                            continue;
                        case "timestamp":
                        case "etag":
                            // Skip these as they're managed by Azure Table Storage
                            continue;
                        default:
                            // Update the property
                            updatedEntity[key] = value;
                            break;
                    }
                }

                // Update the UpdatedAt timestamp
                updatedEntity["UpdatedAt"] = DateTime.UtcNow;

                // Update the entity with optimistic concurrency
                await tableClient.UpdateEntityAsync(updatedEntity, existingEntity.ETag);

                return NoContent();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return NotFound();
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                return StatusCode(412, "The entity has been modified by another process. Please retry with the latest version.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating dynamic record in Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates an existing record in Azure Table Storage using data from a raw JSON string.
        /// Implements optimistic concurrency control, JSON validation, and preserves existing properties not included in the update.
        /// </summary>
        /// <param name="partitionKey">The partition key of the record to update.</param>
        /// <param name="rowKey">The row key of the record to update.</param>
        /// <param name="jsonString">The JSON string containing the properties and values to update.</param>
        /// <param name="namingPolicy">Optional property naming policy for processing (default: CamelCase).</param>
        /// <returns>No content on successful update.</returns>
        /// <response code="204">If the record was updated successfully.</response>
        /// <response code="400">If the JSON string is invalid or not an object.</response>
        /// <response code="404">If the record to update is not found.</response>
        /// <response code="412">If the record has been modified by another process (concurrency conflict).</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPut("{partitionKey}/{rowKey}/json")]
        public async Task<IActionResult> UpdateRecordAsJson(
            string partitionKey, 
            string rowKey, 
            [FromBody] string jsonString,
            [FromQuery] PropertyNamingPolicy? namingPolicy = PropertyNamingPolicy.CamelCase)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey, jsonString, namingPolicy });

            try
            {
                // Validate and parse the JSON string
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return BadRequest("JSON string cannot be null or empty");
                }

                JsonDocument jsonDocument;
                try
                {
                    jsonDocument = JsonDocument.Parse(jsonString);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Invalid JSON format provided");
                    return BadRequest($"Invalid JSON format: {ex.Message}");
                }

                using (jsonDocument)
                {
                    if (jsonDocument.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        return BadRequest("JSON must be an object");
                    }

                    var tableClient = tableServiceClient.GetTableClient(TableName);
                    await tableClient.CreateIfNotExistsAsync();

                    // First, try to get the existing entity to preserve its ETag for optimistic concurrency
                    TableEntity existingEntity;
                    try
                    {
                        var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
                        existingEntity = response.Value;
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404)
                    {
                        return NotFound();
                    }

                    // Create updated entity based on existing entity
                    var updatedEntity = new TableEntity(partitionKey, rowKey)
                    {
                        ETag = existingEntity.ETag // Preserve ETag for optimistic concurrency
                    };

                    // Copy all existing properties first
                    foreach (var kvp in existingEntity)
                    {
                        if (kvp.Key != "PartitionKey" && kvp.Key != "RowKey" && kvp.Key != "Timestamp" && kvp.Key != "ETag")
                        {
                            updatedEntity[kvp.Key] = kvp.Value;
                        }
                    }

                    // Process the JSON properties and update the entity
                    foreach (var property in jsonDocument.RootElement.EnumerateObject())
                    {
                        var key = property.Name;
                        var value = ExtractJsonValue(property.Value);

                        // Normalize key for comparison (handle both camelCase and PascalCase)
                        var normalizedKey = key.ToLowerInvariant();

                        // Handle special properties
                        switch (normalizedKey)
                        {
                            case "partitionkey":
                            case "rowkey":
                                // Ignore these - they come from the URL
                                continue;
                            case "timestamp":
                            case "etag":
                                // Skip these as they're managed by Azure Table Storage
                                continue;
                            default:
                                // Convert key to PascalCase for storage (Azure Table Storage standard)
                                string storageKey = ToPascalCase(key);
                                updatedEntity[storageKey] = value;
                                break;
                        }
                    }

                    // Update the UpdatedAt timestamp
                    updatedEntity["UpdatedAt"] = DateTime.UtcNow;

                    // Update the entity with optimistic concurrency
                    await tableClient.UpdateEntityAsync(updatedEntity, existingEntity.ETag);

                    return NoContent();
                }
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return NotFound();
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                return StatusCode(412, "The entity has been modified by another process. Please retry with the latest version.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating JSON record in Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes a record from Azure Table Storage by its partition key and row key.
        /// </summary>
        /// <param name="partitionKey">The partition key of the record to delete.</param>
        /// <param name="rowKey">The row key of the record to delete.</param>
        /// <returns>No content on successful deletion.</returns>
        /// <response code="204">If the record was deleted successfully.</response>
        /// <response code="404">If the record to delete is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpDelete("{partitionKey}/{rowKey}")]
        public async Task<IActionResult> DeleteRecord(string partitionKey, string rowKey)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey });

            try
            {
                var tableClient = tableServiceClient.GetTableClient(TableName);
                await tableClient.CreateIfNotExistsAsync();

                await tableClient.DeleteEntityAsync(partitionKey, rowKey);

                return NoContent();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting record from Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Converts a string from PascalCase to camelCase format.
        /// </summary>
        /// <param name="input">The input string to convert.</param>
        /// <returns>The string converted to camelCase format.</returns>
        /// <example>
        /// ToCamelCase("UserName") returns "userName"
        /// ToCamelCase("firstName") returns "firstName" (unchanged)
        /// </example>
        private static string ToCamelCase(string input)
        {
            if (string.IsNullOrEmpty(input) || char.IsLower(input[0]))
                return input;

            return char.ToLowerInvariant(input[0]) + input[1..];
        }

        /// <summary>
        /// Converts a string from camelCase to PascalCase format.
        /// </summary>
        /// <param name="input">The input string to convert.</param>
        /// <returns>The string converted to PascalCase format.</returns>
        /// <example>
        /// ToPascalCase("userName") returns "UserName"
        /// ToPascalCase("FirstName") returns "FirstName" (unchanged)
        /// </example>
        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            if (char.IsUpper(input[0]))
                return input;

            return char.ToUpperInvariant(input[0]) + input[1..];
        }

        /// <summary>
        /// Converts a string from PascalCase/camelCase to kebab-case format (lowercase with hyphens).
        /// </summary>
        /// <param name="input">The input string to convert.</param>
        /// <returns>The string converted to kebab-case format.</returns>
        /// <example>
        /// ToKebabCase("UserName") returns "user-name"
        /// ToKebabCase("firstName") returns "first-name"
        /// </example>
        private static string ToKebabCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "-" + char.ToLowerInvariant(x) : char.ToLowerInvariant(x).ToString()));
        }

        /// <summary>
        /// Converts a string from PascalCase/camelCase to snake_case format (lowercase with underscores).
        /// </summary>
        /// <param name="input">The input string to convert.</param>
        /// <returns>The string converted to snake_case format.</returns>
        /// <example>
        /// ToSnakeCase("UserName") returns "user_name"
        /// ToSnakeCase("firstName") returns "first_name"
        /// </example>
        private static string ToSnakeCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + char.ToLowerInvariant(x) : char.ToLowerInvariant(x).ToString()));
        }

        /// <summary>
        /// Recursively extracts and converts JSON element values to appropriate .NET types.
        /// Handles all JSON value types including nested objects and arrays.
        /// </summary>
        /// <param name="element">The JsonElement to extract the value from.</param>
        /// <returns>The extracted value as the appropriate .NET type (string, number, bool, null, array, or dictionary).</returns>
        /// <remarks>
        /// Numbers are intelligently parsed as int, long, double, or decimal based on their content.
        /// Objects are converted to Dictionary&lt;string, object?&gt; and arrays to object[].
        /// </remarks>
        private static object? ExtractJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt32(out int intValue) ? intValue :
                                       element.TryGetInt64(out long longValue) ? longValue :
                                       element.TryGetDouble(out double doubleValue) ? doubleValue :
                                       (object)element.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.EnumerateArray().Select(ExtractJsonValue).ToArray(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ExtractJsonValue(p.Value)),
                _ => element.ToString()
            };
        }

        /// <summary>
        /// Converts a PropertyNamingPolicy enum value to the corresponding System.Text.Json JsonNamingPolicy.
        /// </summary>
        /// <param name="policy">The PropertyNamingPolicy enum value to convert.</param>
        /// <returns>The corresponding JsonNamingPolicy, or null if no mapping exists.</returns>
        /// <remarks>
        /// This method is currently unused but provides a mapping for potential future JSON serialization needs.
        /// </remarks>
        private static JsonNamingPolicy? GetJsonNamingPolicy(PropertyNamingPolicy policy)
        {
            return policy switch
            {
                PropertyNamingPolicy.CamelCase => JsonNamingPolicy.CamelCase,
                PropertyNamingPolicy.KebabCaseLower => JsonNamingPolicy.KebabCaseLower,
                PropertyNamingPolicy.KebabCaseUpper => JsonNamingPolicy.KebabCaseUpper,
                PropertyNamingPolicy.SnakeCaseLower => JsonNamingPolicy.SnakeCaseLower,
                PropertyNamingPolicy.SnakeCaseUpper => JsonNamingPolicy.SnakeCaseUpper,
                _ => null
            };
        }

        /// <summary>
        /// Helper class for encapsulating query options used in Azure Table Storage operations.
        /// </summary>
        private class QueryOptions
        {
            /// <summary>
            /// Gets or sets the OData filter expression for filtering query results.
            /// </summary>
            public string? Filter { get; set; }
            
            /// <summary>
            /// Gets or sets the maximum number of entities to return in the query.
            /// </summary>
            public int? Top { get; set; }
            
            /// <summary>
            /// Gets or sets the collection of property names to select in the query projection.
            /// </summary>
            public IEnumerable<string>? Select { get; set; }
        }

        /// <summary>
        /// Enumeration defining the available property naming policies for response formatting.
        /// Used to control how property names are formatted in API responses.
        /// </summary>
        public enum PropertyNamingPolicy
        {
            /// <summary>
            /// Uses the original property names without any transformation.
            /// </summary>
            Original,
            
            /// <summary>
            /// Converts property names to camelCase format (e.g., "firstName").
            /// </summary>
            CamelCase,
            
            /// <summary>
            /// Converts property names to lowercase kebab-case format (e.g., "first-name").
            /// </summary>
            KebabCaseLower,
            
            /// <summary>
            /// Converts property names to uppercase kebab-case format (e.g., "FIRST-NAME").
            /// </summary>
            KebabCaseUpper,
            
            /// <summary>
            /// Converts property names to lowercase snake_case format (e.g., "first_name").
            /// </summary>
            SnakeCaseLower,
            
            /// <summary>
            /// Converts property names to uppercase snake_case format (e.g., "FIRST_NAME").
            /// </summary>
            SnakeCaseUpper
        }
    }
}
