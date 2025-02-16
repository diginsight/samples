using Diginsight.Stringify;

namespace AuthenticationSampleClient;

public class Plant
{
    [StringifiableMember(Order = 3)]
    public Guid Id { get; set; }

    [StringifiableMember(Order = 2)]
    public string? Name { get; set; }

    [StringifiableMember(Order = 4)]
    public string? Description { get; set; }

    [StringifiableMember(Order = 5)]
    public string? Address { get; set; }

    [StringifiableMember(Order = 7)]
    public DateOnly CreationDate { get; set; }
}
