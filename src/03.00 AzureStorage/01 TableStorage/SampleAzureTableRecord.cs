using Azure;
using Azure.Data.Tables;

namespace TableStorageSampleAPI
{
    public class SampleAzureTableRecord : ITableEntity
    {
        public string PartitionKey { get; set; } = "default";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        
        // Custom properties
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Value { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}