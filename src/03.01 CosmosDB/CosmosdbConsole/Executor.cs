using Azure.Identity;
using Cocona;
using Diginsight.Diagnostics;
using Diginsight.Components.Azure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection.Metadata;

namespace CosmosdbConsole;

internal sealed class Executor : IDisposable
{
    private readonly ILogger logger;
    private readonly CosmosClient cosmosClient;
    private readonly Container container;
    private readonly string? file;
    private readonly bool whatIf;
    private readonly int? top;
    private readonly string? transformString = """

        """;

    /// <summary>
    /// Create a CosmosClient supporting both classic AccountKey connection strings
    /// and AAD-only endpoints. The <paramref name="connectionString"/> may be:
    ///   - a full "AccountEndpoint=...;AccountKey=...;" connection string, OR
    ///   - just a https:// endpoint URL (then DefaultAzureCredential is used), OR
    ///   - "AccountEndpoint=...;AuthType=AAD;" / similar (key absent -> AAD).
    /// </summary>
    private static CosmosClient CreateCosmosClient(string connectionString, ILogger logger)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(static x => x.Split('=', 2))
            .Where(static x => x.Length == 2)
            .ToDictionary(static x => x[0].Trim(), static x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);

        string? accountEndpoint = null;
        if (parts.TryGetValue("AccountEndpoint", out var ep))
        {
            accountEndpoint = ep;
        }
        else if (Uri.IsWellFormedUriString(connectionString.Trim(), UriKind.Absolute))
        {
            accountEndpoint = connectionString.Trim();
        }

        bool hasKey = parts.ContainsKey("AccountKey");

        if (!hasKey)
        {
            if (string.IsNullOrWhiteSpace(accountEndpoint))
            {
                throw new ArgumentException("Connection string has no AccountEndpoint and is not a bare endpoint URL.", nameof(connectionString));
            }
            logger.LogInformation("Using AAD auth (DefaultAzureCredential) for endpoint {endpoint}", accountEndpoint);
            return new CosmosClient(accountEndpoint, new DefaultAzureCredential());
        }

        logger.LogDebug("Using AccountKey auth for endpoint {endpoint}", accountEndpoint);
        return new CosmosClient(connectionString);
    }

    private string? transform(string? recordJson) { 
        if (recordJson is null) { return null; }

        var document = JObject.Parse(recordJson);
        var latitudeProp = document.Properties().Where(p => p.Name.StartsWith("Latitude")).FirstOrDefault();
        var longitudeProp = document.Properties().Where(p => p.Name.StartsWith("Longitude")).FirstOrDefault();
        var latitude = latitudeProp?.Value is not null ? (int)Double.Parse(latitudeProp.Value.ToString()): 0;
        var longitude = longitudeProp?.Value is not null ? (int)Double.Parse(longitudeProp.Value.ToString()): 0;
        var coodKey = $"{latitude},{longitude}";

        var partitionKeyProp = document.Properties().Where(p => p.Name.StartsWith("partitionKey")).FirstOrDefault();
        if (partitionKeyProp == null)
        {
            document.Add("partitionKey", coodKey);
        }
        return document.ToString();
    }

    public Executor(ILogger<Executor> logger)
    {
        this.logger = logger;

        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger);

    }

    public void Dispose()
    {
        cosmosClient?.Dispose();
    }

    public async Task QueryAsync(
        [FromService] CoconaAppContext appContext,
        [Option('c')] string connectionString,
        [Option('q')] string query,
        [Option('d')] string database,
        [Option('t')] string collection,
        [Option('f')] string? file,
        [Option("x")] string? transformFile,
        [Option] int top = -1,
        [Option] int skip = 0
    )
    {
        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { query, database, collection, file, transformFile, top, skip });

        try
        {
            var cosmosClient = CreateCosmosClient(connectionString, logger);
            var container = cosmosClient.GetContainer(database, collection); logger.LogDebug($"container = cosmosClient.GetContainer({database}, {collection});");

            var topClause = top > 0 ? $" OFFSET {skip} LIMIT {top}" : string.Empty;
            string modifiedQuery = $"{query}{topClause}";

            var requestOptions = new QueryRequestOptions { MaxItemCount = top, QueryTextMode = QueryTextMode.None };
            var iterator = container.GetItemQueryStreamIteratorObservable(modifiedQuery, requestOptions: requestOptions);

            StreamWriter? streamWriter = null;
            if (file is not null)
            {
                streamWriter = new StreamWriter(file); logger.LogInformation($"streamWriter = new StreamWriter({file});");
            }

            using (streamWriter)
            {
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    if (!response.IsSuccessStatusCode) { throw new Exception(response.ErrorMessage); }

                    response.Content.Position = 0;
                    var content = await new System.IO.StreamReader(response.Content).ReadToEndAsync();
                    logger.LogDebug("content: {content}", content);
                    if (streamWriter is not null)
                    {
                        await streamWriter.WriteAsync(content);
                    }
                }
            }
        }
        catch (Exception ex) { logger.LogError(ex, $"'{ex.GetType().Name}': {ex.Message}", ex); }
    }

    public async Task<int> StreamDocumentsJsonAsync(
        [FromService] CoconaAppContext appContext,
        [Option('f')] string? filePath,
        [Option('x')] string? transformFile,
        [Option('s')] string? skipFields,
        [Option] int top = -1,
        [Option] int skip = 0
    )
    {
        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { filePath, transformFile, skipFields, top, skip });

        int documentCount = 0;
        
        try
        {
            using (var streamReader = new StreamReader(filePath))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                logger.LogDebug($"Read until the 'Documents' property is found");
                while (await jsonReader.ReadAsync())
                {
                    if (jsonReader.TokenType == JsonToken.PropertyName && (string)jsonReader.Value == "Documents")
                    {
                        await jsonReader.ReadAsync(); // Move to the start of the array
                        if (jsonReader.TokenType == JsonToken.StartArray)
                        {
                            logger.LogDebug($"Start of the array is found");
                            break;
                        }
                    }
                }

                var skipFieldsArray = skipFields?.Split(',')?.Select(static x => x.Trim())?.ToArray();
                logger.LogDebug($"Read documents within the 'Documents' array");
                while (await jsonReader.ReadAsync() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        var document = await JObject.LoadAsync(jsonReader);
                        NormalizeDocument(document, skipFieldsArray);

                        //var documentString = document.ToString();
                        //documentString = transform(documentString);

                        documentCount++;
                    }
                }
            }
        }
        catch (Exception ex) { logger.LogError(ex, $"'{ex.GetType().Name}': {ex.Message}", ex); }

        activity?.SetOutput(documentCount);
        return documentCount;
    }

    public async Task<int> UploadDocumentsJsonAsync(
        [FromService] CoconaAppContext appContext,
        [Option('f')] string filePath,
        [Option('c')] string connectionString,
        [Option('d')] string database,
        [Option('t')] string collection,
        [Option('s')] string skipFields,
        [Option] int top = -1,
        [Option] int skip = 0
    )
    {
        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { filePath, database, collection, skipFields, top, skip });

        int documentCount = 0;

        try
        {
            var cosmosClient = CreateCosmosClient(connectionString, logger);
            var container = cosmosClient.GetContainer(database, collection); logger.LogDebug($"container = cosmosClient.GetContainer({database}, {collection});");

            using (var streamReader = new StreamReader(filePath))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                logger.LogDebug($"Read until the 'Documents' property is found");
                while (await jsonReader.ReadAsync())
                {
                    if (jsonReader.TokenType == JsonToken.PropertyName && (string)jsonReader.Value == "Documents")
                    {
                        await jsonReader.ReadAsync(); // Move to the start of the array
                        if (jsonReader.TokenType == JsonToken.StartArray)
                        {
                            logger.LogDebug($"Start of the array is found");
                            break;
                        }
                    }
                }

                var skipFieldsArray = skipFields?.Split(',')?.Select(static x => x.Trim())?.ToArray();
                logger.LogDebug($"Read documents within the 'Documents' array");
                while (await jsonReader.ReadAsync() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        var document = await JObject.LoadAsync(jsonReader);
                        var id = NormalizeDocument(document, skipFieldsArray);

                        var response = await container.UpsertItemObservableAsync(document); 
                        id = GetDocumentId(document); logger.LogDebug($"container.UpsertItemObservableAsync(document {{{id}}});");

                        documentCount++;
                    }
                }
            }

        }
        catch (Exception ex) { logger.LogError(ex, $"'{ex.GetType().Name}': {ex.Message}", ex); }

        activity?.SetOutput(documentCount);
        return documentCount;
    }

    public async Task<int> DeleteDocumentsFromJsonAsync(
       [FromService] CoconaAppContext appContext,
       [Option('f')] string filePath,
       [Option('c')] string connectionString,
       [Option('d')] string database,
       [Option('t')] string collection,
       [Option] int top = -1,
       [Option] int skip = 0
   )
    {
        using Activity? activity = Observability.ActivitySource.StartMethodActivity(logger, () => new { filePath, database, collection, top, skip });

        int documentCount = 0;

        try
        {
            var cosmosClient = CreateCosmosClient(connectionString, logger);
            var container = cosmosClient.GetContainer(database, collection); logger.LogDebug($"container = cosmosClient.GetContainer({database}, {collection});");

            using (var streamReader = new StreamReader(filePath))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                logger.LogDebug($"Read until the 'Documents' property is found");
                while (await jsonReader.ReadAsync())
                {
                    if (jsonReader.TokenType == JsonToken.PropertyName && (string)jsonReader.Value == "Documents")
                    {
                        await jsonReader.ReadAsync(); // Move to the start of the array
                        if (jsonReader.TokenType == JsonToken.StartArray)
                        {
                            logger.LogDebug($"Start of the array is found");
                            break;
                        }
                    }
                }

                logger.LogDebug($"Read documents within the 'Documents' array");
                while (await jsonReader.ReadAsync() && jsonReader.TokenType != JsonToken.EndArray)
                {
                    if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        var document = await JObject.LoadAsync(jsonReader);
                        var id = GetDocumentId(document);

                        await container.DeleteItemStreamObservableAsync(id, PartitionKey.None); logger.LogDebug($"container.DeleteItemStreamObservableAsync({id}, PartitionKey.None);");
                        //await container.ReadItemStreamAsync(id); logger.LogDebug($"container.UpsertItemAsync(document);");
                        documentCount++;
                    }
                }
            }

        }
        catch (Exception ex) { logger.LogError(ex, $"'{ex.GetType().Name}': {ex.Message}", ex); }

        activity?.SetOutput(documentCount);
        return documentCount;
    }

    private string NormalizeDocument(JObject document, string[] skipFields)
    {
        var skipProperties = document.Properties().Where(p => p.Name.StartsWith("_") || skipFields is not null && skipFields.Contains(p.Name, StringComparer.InvariantCultureIgnoreCase))?.ToList();
        foreach (var property in skipProperties)
        {
            property.Remove();
        }
        var idProp = document.Properties().Where(p => p.Name.StartsWith("id")).FirstOrDefault();
        if (idProp == null) {
            var id = Guid.NewGuid();
            document.Add("id", id.ToString());
        }
        return idProp?.Value?.ToString();
    }
    private string GetDocumentId(JObject document)
    {
        var idProp = document.Properties().Where(p => p.Name.StartsWith("id")).FirstOrDefault();
        return idProp.Value.ToString();
    }

}
