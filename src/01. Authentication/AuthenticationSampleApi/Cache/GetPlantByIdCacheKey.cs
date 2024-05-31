
using Diginsight.SmartCache;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

public record PlantInvalidationRule(Guid PlantId) : IInvalidationRule;


//[SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
//internal sealed record GetPlantByIdCacheKey(Guid PlantId) : IInvalidatable
//{
//    public bool IsInvalidatedBy(IInvalidationRule invalidationRule, out Func<Task> ic)
//    {
//        ic = null;
//        if (invalidationRule is PlantInvalidationRule pir && (PlantId == Guid.Empty || pir.PlantId == PlantId))
//        {
//            return true;
//        }
//        return false;
//    }
//}

internal sealed class GetPlantByIdCacheKey : IInvalidatable, IManualSize
{
    private readonly EqualityCore equalityCore;

    public Func<Task> ReloadAsync { private get; set; }

    [JsonProperty]
    private Guid PlantId => equalityCore.PlantId;

    public GetPlantByIdCacheKey(
        ICacheKeyService cacheKeyService,
        Guid plantId
    ) : this(new EqualityCore(plantId)) { }

    [JsonConstructor]
    private GetPlantByIdCacheKey(Guid plantId) : this(new EqualityCore(plantId)) { }
    private GetPlantByIdCacheKey(EqualityCore equalityCore) { this.equalityCore = equalityCore; }

    public bool IsInvalidatedBy(IInvalidationRule invalidationRule, out Func<Task> ic)
    {
        if (invalidationRule is PlantInvalidationRule air && (PlantId == Guid.Empty || air.PlantId == PlantId))
        {
            ic = ReloadAsync;
            return true;
        }

        ic = null;
        return false;
    }

    public override bool Equals(object obj) => equalityCore == (obj as GetPlantByIdCacheKey)?.equalityCore;

    public override int GetHashCode() => equalityCore.GetHashCode();

    public (long Sz, bool Fxd) GetSize(Func<object, (long Sz, bool Fxd)> innerGetSize)
    {
        (long Sz, bool Fxd) inner0 = innerGetSize(equalityCore);
        long inner1Sz = ReloadAsync is null ? 0 : IntPtr.Size;

        return (inner0.Sz + inner1Sz, inner0.Fxd);
    }

    [SuppressMessage("ReSharper", "NotAccessedPositionalProperty.Local")]
    internal sealed record EqualityCore(Guid PlantId);
}

