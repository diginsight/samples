namespace AzureProvisioning;

internal interface IProvisioner
{
    Task EnsureGroupsAsync(IReadOnlyDictionary<string, IEnumerable<string>> ownersByGroup, CancellationToken cancellationToken);
    Task FillGroupsAsync(IReadOnlyDictionary<string, IEnumerable<string>> desiredMembersByGroup, IDictionary<string, ISet<string>> excessMembersByGroup, CancellationToken cancellationToken);
    Task WriteExcessMembersAsync(TextWriter writer, IDictionary<string, ISet<string>> excessMembersByGroup);
    Task AssignRolesAsync(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IEnumerable<string>>> roleAsgsByTarget, CancellationToken cancellationToken);
    Task AssignKvAccessPoliciesAsync(IReadOnlyDictionary<string, IEnumerable<KvAccessDescriptor>> accessDescriptorsByKv, CancellationToken cancellationToken);
}
