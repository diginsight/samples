using Microsoft.Azure.Cosmos;

namespace IdentityAPI;

public static class FeedIteratorExtensions {
    public static async Task<IEnumerable<T>> GetItemsAsync<T>(this FeedIterator<T> iterator)
        where T : class
    {
        var result = new List<T>();
        while (iterator.HasMoreResults)
        {
            foreach (var location in await iterator.ReadNextAsync())
            {
                if (location is null) continue;
                result.Add(location);
            }
        }
        return result;
    }

}
