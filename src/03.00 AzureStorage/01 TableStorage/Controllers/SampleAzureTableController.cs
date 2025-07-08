using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using Microsoft.Graph;
using Diginsight.Diagnostics;
using Azure.Data.Tables;
using Azure;
using System.Text.Json;
using Diginsight.Components.Azure.Abstractions;

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
        private readonly IAzureTableRepository<SampleAzureTableRecord> repository;
        private readonly ILogger<SampleAzureTableController> logger;

        /// <summary>
        /// Initializes a new instance of the SampleAzureTableController class.
        /// </summary>
        /// <param name="logger">The logger instance for logging operations and errors.</param>
        /// <param name="graphServiceClient">The Microsoft Graph service client for identity operations.</param>
        /// <param name="repository">The strongly-typed Azure Table repository for SampleAzureTableRecord operations.</param>
        public SampleAzureTableController(
            ILogger<SampleAzureTableController> logger,
            GraphServiceClient graphServiceClient,
            IAzureTableRepository<SampleAzureTableRecord> repository)
        {
            this.logger = logger;
            this.graphServiceClient = graphServiceClient;
            this.repository = repository;
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
        public async Task<ActionResult<IEnumerable<SampleAzureTableRecord>>> QueryAsync(
            [FromQuery] string? filter = null,
            [FromQuery] int? top = null,
            [FromQuery] string? select = null)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { filter, top, select });

            try
            {
                var selectList = string.IsNullOrEmpty(select) ? null : select.Split(',').Select(s => s.Trim());
                var records = await repository.QueryAsync(filter, top, selectList);

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
        public async Task<ActionResult<IEnumerable<object>>> QueryAsJsonAsync(
            [FromQuery] string? filter = null,
            [FromQuery] int? top = null,
            [FromQuery] string? select = null,
            [FromQuery] PropertyNamingPolicy? namingPolicy = null)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { filter, top, select, namingPolicy });

            try
            {
                var selectList = string.IsNullOrEmpty(select) ? null : select.Split(',').Select(s => s.Trim());
                var records = await repository.QueryAsJsonAsync(filter, top, selectList, namingPolicy);

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
        public async Task<ActionResult<SampleAzureTableRecord>> GetAsync(string partitionKey, string rowKey)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey });

            try
            {
                var record = await repository.GetAsync(partitionKey, rowKey);
                
                if (record == null)
                {
                    return NotFound();
                }

                activity?.SetOutput(record);
                return Ok(record);
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
        public async Task<ActionResult<SampleAzureTableRecord>> CreateAsync(SampleAzureTableRecord record)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { record });

            try
            {
                var createdRecord = await repository.CreateAsync(record);

                activity?.SetOutput(createdRecord);
                return CreatedAtAction(nameof(GetAsync), new { partitionKey = createdRecord.PartitionKey, rowKey = createdRecord.RowKey }, createdRecord);
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
        public async Task<ActionResult<object>> CreateDynamicAsync([FromBody] Dictionary<string, object> entityData)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { entityData });

            try
            {
                var responseEntity = await repository.CreateDynamicAsync(entityData);

                activity?.SetOutput(responseEntity);
                return Created($"/{responseEntity.GetValueOrDefault("partitionKey")}/{responseEntity.GetValueOrDefault("rowKey")}", responseEntity);
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
        public async Task<ActionResult<object>> CreateAsJsonAsync(
            [FromBody] string jsonString,
            [FromQuery] PropertyNamingPolicy? namingPolicy = PropertyNamingPolicy.CamelCase)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { jsonString, namingPolicy });

            try
            {
                var responseEntity = await repository.CreateAsJsonAsync(jsonString, namingPolicy);

                activity?.SetOutput(responseEntity);
                return Created($"/{responseEntity.GetValueOrDefault("partitionKey")}/{responseEntity.GetValueOrDefault("rowKey")}", responseEntity);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid request data provided");
                return BadRequest(ex.Message);
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
        /// Creates multiple records in Azure Table Storage using strongly-typed entities in a single transaction.
        /// All entities must belong to the same partition for the batch operation to succeed.
        /// </summary>
        /// <param name="records">The collection of SampleAzureTableRecord entities to create.</param>
        /// <returns>The created records with generated keys and timestamps.</returns>
        /// <response code="201">Returns the newly created records.</response>
        /// <response code="400">If the entities belong to different partitions or are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("batch")]
        public async Task<ActionResult<IEnumerable<SampleAzureTableRecord>>> CreateBatchAsync([FromBody] IEnumerable<SampleAzureTableRecord> records)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { records });

            try
            {
                var createdRecords = await repository.CreateBatchAsync(records);

                activity?.SetOutput(createdRecords);
                return Created("batch", createdRecords);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid batch request data provided");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating batch records in Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates multiple records in Azure Table Storage from a JSON array string in a single transaction.
        /// All entities must belong to the same partition for the batch operation to succeed.
        /// Provides advanced JSON parsing, validation, and flexible response formatting.
        /// </summary>
        /// <param name="jsonString">The JSON array string containing the entity data to create.</param>
        /// <param name="namingPolicy">Optional property naming policy for response formatting (default: CamelCase).</param>
        /// <returns>The created entities as dynamic objects with property names formatted according to the specified naming policy.</returns>
        /// <response code="201">Returns the newly created records.</response>
        /// <response code="400">If the JSON string is invalid, not an array, or entities belong to different partitions.</response>
        /// <response code="500">If an internal server error occurs.</response>
        [HttpPost("batch/json")]
        public async Task<ActionResult<IEnumerable<object>>> CreateAsJsonBatchAsync(
            [FromBody] string jsonString,
            [FromQuery] PropertyNamingPolicy? namingPolicy = PropertyNamingPolicy.CamelCase)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { jsonString, namingPolicy });

            try
            {
                var responseEntities = await repository.CreateAsJsonBatchAsync(jsonString, namingPolicy);

                activity?.SetOutput(responseEntities);
                return Created("batch/json", responseEntities);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid batch JSON request data provided");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating JSON batch records in Azure Table Storage");
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
        public async Task<IActionResult> UpdateAsync(string partitionKey, string rowKey, SampleAzureTableRecord record)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey, record });

            try
            {
                await repository.UpdateAsync(partitionKey, rowKey, record);
                return NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
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
        public async Task<IActionResult> UpdateDynamicAsync(string partitionKey, string rowKey, [FromBody] Dictionary<string, object> entityData)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey, entityData });

            try
            {
                await repository.UpdateDynamicAsync(partitionKey, rowKey, entityData);
                return NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
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
        public async Task<IActionResult> UpdateAsJsonAsync(
            string partitionKey, 
            string rowKey, 
            [FromBody] string jsonString,
            [FromQuery] PropertyNamingPolicy? namingPolicy = PropertyNamingPolicy.CamelCase)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey, jsonString, namingPolicy });

            try
            {
                await repository.UpdateAsJsonAsync(partitionKey, rowKey, jsonString, namingPolicy);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Invalid request data provided");
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
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
        public async Task<IActionResult> DeleteAsync(string partitionKey, string rowKey)
        {
            using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { partitionKey, rowKey });

            try
            {
                await repository.DeleteAsync(partitionKey, rowKey);
                return NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting record from Azure Table Storage");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
