
using Diginsight.SmartCache;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

public record UserInvalidationRule(Guid UserId) : IInvalidationRule;


//[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
//internal sealed record GetUserByIdCacheKey(Guid UserId) : IInvalidatable
//{
//    public bool IsInvalidatedBy(IInvalidationRule invalidationRule, out Func<Task> ic)
//    {
//        ic = null;
//        if (invalidationRule is UserInvalidationRule pir && (UserId == Guid.Empty || pir.UserId == UserId))
//        {
//            return true;
//        }
//        return false;
//    }
//}

internal sealed class GetUserByIdCacheKey : IInvalidatable
{
    private readonly EqualityCore equalityCore;

    public Func<Task> ReloadAsync { private get; set; }

    [JsonProperty]
    private Guid UserId => equalityCore.UserId;

    public GetUserByIdCacheKey(
        ICacheKeyService cacheKeyService,
        Guid plantId
    ) : this(new EqualityCore(plantId)) { }

    [JsonConstructor]
    private GetUserByIdCacheKey(Guid plantId) : this(new EqualityCore(plantId)) { }
    private GetUserByIdCacheKey(EqualityCore equalityCore) { this.equalityCore = equalityCore; }

    public bool IsInvalidatedBy(IInvalidationRule invalidationRule, out Func<Task> ic)
    {
        if (invalidationRule is UserInvalidationRule air && (UserId == Guid.Empty || air.UserId == UserId))
        {
            ic = ReloadAsync;
            return true;
        }

        ic = null;
        return false;
    }

    public override bool Equals(object obj) => equalityCore == (obj as GetUserByIdCacheKey)?.equalityCore;

    public override int GetHashCode() => equalityCore.GetHashCode();

    public (long Sz, bool Fxd) GetSize(Func<object, (long Sz, bool Fxd)> innerGetSize)
    {
        (long Sz, bool Fxd) inner0 = innerGetSize(equalityCore);
        long inner1Sz = ReloadAsync is null ? 0 : IntPtr.Size;

        return (inner0.Sz + inner1Sz, inner0.Fxd);
    }

    [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
    internal sealed record EqualityCore(Guid UserId);
}

