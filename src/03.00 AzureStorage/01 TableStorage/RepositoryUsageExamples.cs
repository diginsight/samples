using Microsoft.Extensions.DependencyInjection;
using Azure.Data.Tables;
using Diginsight.Components.Azure.Abstractions;
using Diginsight.Components.Azure.Repositories;

namespace TableStorageSampleAPI
{
    /// <summary>
    /// Example usage of the generic Azure Table Repository.
    /// This demonstrates how to use the repository with different entity types.
    /// </summary>
    public static class RepositoryUsageExamples
    {
        /// <summary>
        /// Example of how to register different entity types in the DI container.
        /// Add this to your Program.cs or startup configuration.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        public static void ConfigureRepositories(IServiceCollection services)
        {
            // Register repositories for different entity types
            services.AddScoped<IAzureTableRepository<SampleAzureTableRecord>, AzureTableRepository<SampleAzureTableRecord>>(sp =>
                new AzureTableRepository<SampleAzureTableRecord>(
                    sp.GetRequiredService<TableServiceClient>(),
                    sp.GetRequiredService<ILogger<AzureTableRepository<SampleAzureTableRecord>>>(),
                    "SampleTable"));

            services.AddScoped<IAzureTableRepository<ProductRecord>, AzureTableRepository<ProductRecord>>(sp =>
                new AzureTableRepository<ProductRecord>(
                    sp.GetRequiredService<TableServiceClient>(),
                    sp.GetRequiredService<ILogger<AzureTableRepository<ProductRecord>>>(),
                    "ProductsTable"));

            services.AddScoped<IAzureTableRepository<UserProfileRecord>, AzureTableRepository<UserProfileRecord>>(sp =>
                new AzureTableRepository<UserProfileRecord>(
                    sp.GetRequiredService<TableServiceClient>(),
                    sp.GetRequiredService<ILogger<AzureTableRepository<UserProfileRecord>>>(),
                    "UserProfilesTable"));
        }

        /// <summary>
        /// Example of how to use the generic repository in a service or controller.
        /// </summary>
        public class ExampleService
        {
            private readonly IAzureTableRepository<ProductRecord> _productRepository;
            private readonly IAzureTableRepository<UserProfileRecord> _userRepository;
            private readonly IAzureTableRepository<SampleAzureTableRecord> _sampleRepository;

            public ExampleService(
                IAzureTableRepository<ProductRecord> productRepository,
                IAzureTableRepository<UserProfileRecord> userRepository,
                IAzureTableRepository<SampleAzureTableRecord> sampleRepository)
            {
                _productRepository = productRepository;
                _userRepository = userRepository;
                _sampleRepository = sampleRepository;
            }

            /// <summary>
            /// Example: Create a new product
            /// </summary>
            public async Task<ProductRecord> CreateProductAsync(string name, decimal price, string category)
            {
                var product = new ProductRecord
                {
                    PartitionKey = category, // Use category as partition key for better distribution
                    ProductName = name,
                    Price = price,
                    Category = category,
                    StockQuantity = 0
                };

                return await _productRepository.CreateAsync(product);
            }

            /// <summary>
            /// Example: Get all products in a category
            /// </summary>
            public async Task<IEnumerable<ProductRecord>> GetProductsByCategoryAsync(string category)
            {
                var filter = $"PartitionKey eq '{category}'";
                return await _productRepository.QueryAsync(filter);
            }

            /// <summary>
            /// Example: Update product stock
            /// </summary>
            public async Task UpdateProductStockAsync(string category, string productId, int newStock)
            {
                var product = await _productRepository.GetAsync(category, productId);
                if (product != null)
                {
                    product.StockQuantity = newStock;
                    await _productRepository.UpdateAsync(category, productId, product);
                }
            }

            /// <summary>
            /// Example: Create a user profile
            /// </summary>
            public async Task<UserProfileRecord> CreateUserProfileAsync(string userId, string firstName, string lastName, string email)
            {
                var userProfile = new UserProfileRecord
                {
                    PartitionKey = "users", // All users in same partition for this example
                    RowKey = userId,
                    UserId = userId,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email
                };

                return await _userRepository.CreateAsync(userProfile);
            }

            /// <summary>
            /// Example: Get all enabled users
            /// </summary>
            public async Task<IEnumerable<UserProfileRecord>> GetEnabledUsersAsync()
            {
                var filter = "IsEnabled eq true";
                return await _userRepository.QueryAsync(filter);
            }

            /// <summary>
            /// Example: Create multiple products in a batch (same partition)
            /// </summary>
            public async Task<IEnumerable<ProductRecord>> CreateProductBatchAsync(string category, params (string Name, decimal Price)[] products)
            {
                var productRecords = products.Select(p => new ProductRecord
                {
                    PartitionKey = category, // All products must have same partition key for batch
                    ProductName = p.Name,
                    Price = p.Price,
                    Category = category,
                    StockQuantity = 0
                }).ToList();

                return await _productRepository.CreateBatchAsync(productRecords);
            }

            /// <summary>
            /// Example: Query products as JSON with custom naming policy
            /// </summary>
            public async Task<IEnumerable<object>> GetProductsAsJsonAsync(PropertyNamingPolicy namingPolicy = PropertyNamingPolicy.CamelCase)
            {
                return await _productRepository.QueryAsJsonAsync(namingPolicy: namingPolicy);
            }

            /// <summary>
            /// Example: Create products from JSON
            /// </summary>
            public async Task<IEnumerable<Dictionary<string, object?>>> CreateProductsFromJsonAsync(string jsonArray)
            {
                return await _productRepository.CreateAsJsonBatchAsync(jsonArray, PropertyNamingPolicy.CamelCase);
            }
        }
    }
}