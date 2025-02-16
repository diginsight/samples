#nullable enable

using Diginsight.SmartCache;


public sealed class MyCacheKeyProvider : ICacheKeyProvider
{
    //public ToKeyResult ToKey(ICacheKeyService service, object? obj)
    //{
    //    switch (obj)
    //    {
    //        //case IGroup g:
    //        //    var key = new ToKeyResult(g.Id);
    //        //    return key;

    //        default:
    //            return default;
    //    }
    //}

    object? ICacheKeyProvider.ToKey(ICacheKeyService service, object? obj)
    {
        switch (obj)
        {
            //case IGroup g:
            //    var key = new ToKeyResult(g.Id);
            //    return key;

            default:
                return default;
        }
    }
}
