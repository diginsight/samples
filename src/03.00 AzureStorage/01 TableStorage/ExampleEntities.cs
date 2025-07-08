using Azure;
using Azure.Data.Tables;

namespace TableStorageSampleAPI
{
    /// <summary>
    /// Example of a different entity type that can be used with the generic repository.
    /// This demonstrates how the repository pattern can work with any ITableEntity implementation.
    /// </summary>
    public class ProductRecord : ITableEntity
    {
        public string PartitionKey { get; set; } = "default";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        
        // Custom properties for a product entity
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Supplier { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Another example entity to demonstrate the flexibility of the generic repository.
    /// This represents a user profile entity.
    /// </summary>
    public class UserProfileRecord : ITableEntity
    {
        public string PartitionKey { get; set; } = "default";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        
        // Custom properties for a user profile entity
        public string? UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Department { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}