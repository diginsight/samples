namespace IdentityAPI;

public class CosmosDbOptions {
    public required string ConnectionString { get; set; }
    public required string Database { get; set; }
    public required string Collection { get; set; }
}
