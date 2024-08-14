using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Diginsight.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using NGuid;
using AzureProvisioning.Configurations;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;

namespace AzureProvisioning;

internal sealed class Provisioner : IProvisioner
{
    private readonly ILogger logger;
    private readonly IHostEnvironment hostEnvironment;

    private static readonly Guid ProvisionerGuidNs = GuidHelpers.CreateFromName(Guid.Empty, typeof(Provisioner).FullName!);

    private static readonly IdentityAccessPermissions ReadOnlyIdentityAccessPermissions = new()
    {
        Keys = { IdentityAccessKeyPermission.Get, IdentityAccessKeyPermission.List },
        Secrets = { IdentityAccessSecretPermission.Get, IdentityAccessSecretPermission.List },
        Certificates = { IdentityAccessCertificatePermission.Get, IdentityAccessCertificatePermission.List },
    };

    private static readonly IdentityAccessPermissions ReadWriteIdentityAccessPermissions = new()
    {
        Keys =
        {
            IdentityAccessKeyPermission.Get,
            IdentityAccessKeyPermission.List,
            IdentityAccessKeyPermission.Update,
            IdentityAccessKeyPermission.Create,
            IdentityAccessKeyPermission.Import,
            IdentityAccessKeyPermission.Delete,
            IdentityAccessKeyPermission.Recover,
            IdentityAccessKeyPermission.Backup,
            IdentityAccessKeyPermission.Restore,
            IdentityAccessKeyPermission.Purge,
            IdentityAccessKeyPermission.Release,
            IdentityAccessKeyPermission.Rotate,
            IdentityAccessKeyPermission.Getrotationpolicy,
            IdentityAccessKeyPermission.Setrotationpolicy,
        },
        Secrets =
        {
            IdentityAccessSecretPermission.Get,
            IdentityAccessSecretPermission.List,
            IdentityAccessSecretPermission.Set,
            IdentityAccessSecretPermission.Delete,
            IdentityAccessSecretPermission.Recover,
            IdentityAccessSecretPermission.Backup,
            IdentityAccessSecretPermission.Restore,
            IdentityAccessSecretPermission.Purge,
        },
        Certificates =
        {
            IdentityAccessCertificatePermission.Get,
            IdentityAccessCertificatePermission.List,
            IdentityAccessCertificatePermission.Update,
            IdentityAccessCertificatePermission.Create,
            IdentityAccessCertificatePermission.Import,
            IdentityAccessCertificatePermission.Delete,
            IdentityAccessCertificatePermission.Recover,
            IdentityAccessCertificatePermission.Backup,
            IdentityAccessCertificatePermission.Restore,
            IdentityAccessCertificatePermission.ManageContacts,
            IdentityAccessCertificatePermission.ManageIssuers,
            IdentityAccessCertificatePermission.GetIssuers,
            IdentityAccessCertificatePermission.ListIssuers,
            IdentityAccessCertificatePermission.SetIssuers,
            IdentityAccessCertificatePermission.DeleteIssuers,
            IdentityAccessCertificatePermission.Purge,
        },
    };

    private readonly Guid tenantId;
    private readonly GraphServiceClient graphClient;
    private readonly IDictionary<string, ArmClient> armClients;

    private readonly IDictionary<string, string?> userIdCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, Group?> groupCache = new Dictionary<string, Group?>(StringComparer.OrdinalIgnoreCase);
    private readonly IDictionary<string, string?> resolvedMemberIdCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    private readonly IDictionary<(ResourceIdentifier, string), (ResourceIdentifier, string)> roleCache =
        new Dictionary<(ResourceIdentifier, string), (ResourceIdentifier, string)>();

    public Provisioner(
        string tenantId,
        ILogger<Provisioner> logger,
        IGraphClientCredentialsOptions graphClientCredentialsOptions,
        IReadOnlyDictionary<string, IArmClientCredentialsOptions> armClientCredentialsOptionsDict,
        IHostEnvironment hostEnvironment
    )
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { tenantId, graphClientCredentialsOptions, armClientCredentialsOptionsDict });

        this.logger = logger;
        this.hostEnvironment = hostEnvironment;

        var credential = default(TokenCredential);

        if (!string.IsNullOrEmpty(graphClientCredentialsOptions.ClientId) && !string.IsNullOrEmpty(graphClientCredentialsOptions.ClientSecret))
        {
            credential = new ClientSecretCredential(
                graphClientCredentialsOptions.TenantId ?? tenantId,
                graphClientCredentialsOptions.ClientId, graphClientCredentialsOptions.ClientSecret
            );
        }
        if (credential == null)
        {
            var environmentName = hostEnvironment.EnvironmentName;
            bool isChina = environmentName.EndsWith("cn", StringComparison.OrdinalIgnoreCase);
            AzureCliCredentialOptions credentialOptions = new();
            credentialOptions.TenantId = tenantId;
            if (isChina)
            {
                credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureChina;
            }
            credential = new ChainedTokenCredential(new AzureCliCredential(credentialOptions));
        }

        graphClient = new GraphServiceClient(credential);

        armClients = armClientCredentialsOptionsDict.ToDictionary(
            static x => x.Key,
            x =>
            {
                IArmClientCredentialsOptions cco = x.Value;

                var credential1 = default(TokenCredential);
                if (!string.IsNullOrEmpty(cco.ClientId) && !string.IsNullOrEmpty(cco.ClientSecret))
                {
                    credential1 = new ClientSecretCredential(cco.TenantId ?? tenantId, cco.ClientId, cco.ClientSecret);
                }
                if (credential1 == null)
                {
                    var environmentName = hostEnvironment.EnvironmentName;
                    bool isChina = environmentName.EndsWith("cn", StringComparison.OrdinalIgnoreCase);
                    AzureCliCredentialOptions credentialOptions = new();
                    credentialOptions.TenantId = tenantId;
                    if (isChina)
                    {
                        credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureChina;
                    }
                    credential1 = new ChainedTokenCredential(new AzureCliCredential(credentialOptions));
                }

                return new ArmClient(credential1, x.Key);
            },
            StringComparer.OrdinalIgnoreCase
        );

        this.tenantId = Guid.Parse(tenantId);
    }

    public async Task EnsureGroupsAsync(IReadOnlyDictionary<string, IEnumerable<string>> ownersByGroup, CancellationToken cancellationToken)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { ownersByGroup });

        try
        {
            foreach ((string groupName, IEnumerable<string> owners) in ownersByGroup)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnsureGroupAsync(groupName, owners);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception ensuring groups");
        }
    }

    private async Task EnsureGroupAsync(string name, IEnumerable<string> owners)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { name, owners });

        try
        {
            Group? group = await GetGroupAsync(name, true);

            ISet<string> desiredOwnerIds = new HashSet<string>();
            foreach (string owner in owners)
            {
                desiredOwnerIds.Add(await GetUserIdOrThrowAsync(owner));
            }

            if (group is not null)
            {
                logger.LogInformation("Updating group '{GroupName}'", name);

                string groupId = group.Id!;
                IEnumerable<string> currentOwnerIds = (await graphClient.Groups[groupId].Owners.GetAsync())!.Value!.Select(static x => x.Id!).ToArray();

                foreach (string ownerId in currentOwnerIds.Except(desiredOwnerIds))
                {
                    await graphClient.Groups[groupId].Owners[ownerId].Ref.DeleteAsync();
                }

                foreach (string ownerId in desiredOwnerIds.Except(currentOwnerIds))
                {
                    await graphClient.Groups[groupId].Owners.Ref.PostAsync(MakeReferenceCreate(ownerId, true));
                }
            }
            else
            {
                logger.LogInformation("Creating group '{GroupName}'", name);

                group = new Group()
                {
                    DisplayName = name,
                    GroupTypes = [],
                    MailEnabled = false,
                    MailNickname = name,
                    SecurityEnabled = true,
                    AdditionalData = new Dictionary<string, object>()
                    {
                        ["owners@odata.bind"] = desiredOwnerIds.Select(static x => MakeOdataId(x, true)).ToArray(),
                    },
                };

                await graphClient.Groups.PostAsync(group);

                groupCache.Remove(name);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error ensuring group '{GroupName}'", name);
        }
    }

    private async Task<Group?> GetGroupAsync(string name, bool forceFetch = false)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { name, forceFetch }, logLevel: LogLevel.Trace);

        if (forceFetch || !groupCache.TryGetValue(name, out Group? group))
        {
            logger.LogTrace("Getting group '{GroupName}'", name);

            var groups = (await graphClient.Groups.GetAsync(rc =>
            {
                rc.QueryParameters.Filter = $"displayName eq '{name}'";
                rc.QueryParameters.Select = ["id", "displayName"];
            }
                ))?.Value;

            //var groups01 = (await graphClient.Groups.GetAsync(rc =>
            //{
            //    rc.QueryParameters.Filter = $"contains(displayName, 'DevSamples')";
            //    rc.QueryParameters.Select = ["id", "displayName"];
            //}))?.Value;
            //
            //var group02 = await graphClient.Groups["6c39f206-cb73-4ff1-b607-4a3352d64549"].GetAsync();


            var groupsAll = (await graphClient.Groups.GetAsync())?.Value;

            if (groupsAll != null)
            {
                foreach (var grp in groupsAll)
                {
                    logger.LogDebug("{groupDisplayName} ({groupId} {groupUniqueName})", grp.DisplayName, grp.Id, grp.UniqueName);
                }
            }

            group = groupCache[name] = groups?.FirstOrDefault();
        }

        activity.SetOutput(group);
        return group;
    }

    public async Task FillGroupsAsync(
        IReadOnlyDictionary<string, IEnumerable<string>> desiredMembersByGroup,
        IDictionary<string, ISet<string>> excessMembersByGroup,
        CancellationToken cancellationToken
    )
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { desiredMembersByGroup });

        foreach ((string groupName, IEnumerable<string> desiredMembers) in desiredMembersByGroup)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                Group group = await GetGroupAsync(groupName) ?? throw new ProvisioningException($"Group '{groupName}' not found");
                group.Members = (await graphClient.Groups[group.Id!].Members.GetAsync(cancellationToken: CancellationToken.None))!.Value;

                logger.LogInformation("Updating group '{GroupName}' (current members: {currentMembers}, required members: {requiredMembers})", groupName, group.Members?.Count ?? 0, desiredMembers.Count());

                ISet<string> desiredMembersIds = new HashSet<string>();
                foreach (string desiredMember in desiredMembers)
                {
                    desiredMembersIds.Add(await ResolveMemberAsync(desiredMember));
                }

                await SetMembersAsync(group, desiredMembersIds, excessMembersByGroup);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error filling group {GroupName}", groupName);
            }
        }
    }

    private async Task<string> ResolveMemberAsync(string member)
    {
        if (!resolvedMemberIdCache.TryGetValue(member, out string? memberId))
        {
            try
            {
                if (Guid.TryParse(member, out _))
                {
                    memberId = member;
                    return memberId;
                }
                if (member.Contains('@'))
                {
                    memberId = await GetUserIdOrThrowAsync(member);
                    return memberId;
                }
                memberId = await GetGroupIdAsync(member);
                return memberId ?? throw new ProvisioningException($"Group '{member}' not found");
            }
            finally
            {
                resolvedMemberIdCache[member] = memberId;
            }
        }

        return memberId ?? throw new ProvisioningException($"Group '{member}' not found");
    }

    private async Task<string> GetUserIdOrThrowAsync(string mail)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { mail }, logLevel: LogLevel.Trace);

        if (!userIdCache.TryGetValue(mail, out string? userId))
        {
            logger.LogTrace("Getting user '{Mail}'", mail);

            var users = (await graphClient.Users.GetAsync(
                    rc =>
                    {
                        rc.QueryParameters.Filter = $"mail eq '{mail}'";
                        rc.QueryParameters.Select = ["id"];
                    }
                ))?.Value;

            userIdCache[mail] = userId = (users?.FirstOrDefault())?.Id;
        }

        var ret = userId ?? throw new ProvisioningException($"User '{mail}' not found");
        activity?.SetOutput(ret);
        return ret;
    }

    private async Task SetMembersAsync(Group group, IEnumerable<string> desiredMemberIds, IDictionary<string, ISet<string>> excessMembersByGroup)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { groupName = group.DisplayName, desiredMemberIds });

        logger.LogDebug("Setting members for group '{GroupName}'", group.DisplayName);

        string groupId = group.Id!;
        IEnumerable<string> currentMemberIds = group.Members!.Select(static o => o.Id!).ToArray();

        if (!excessMembersByGroup.TryGetValue(groupId, out ISet<string>? excessMembers))
        {
            excessMembers = excessMembersByGroup[groupId] = new HashSet<string>();
        }

        foreach (string memberId in currentMemberIds.Except(desiredMemberIds))
        {
            excessMembers.Add(memberId);
        }

        foreach (string memberId in desiredMemberIds.Except(currentMemberIds))
        {
            await graphClient.Groups[group.Id].Members.Ref.PostAsync(MakeReferenceCreate(memberId, false));
        }
    }

    public async Task WriteExcessMembersAsync(TextWriter writer, IDictionary<string, ISet<string>> excessMembersByGroup)
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { excessMembersByGroup }, logLevel: LogLevel.Debug);

        if (excessMembersByGroup.Count <= 0)
            return;

        IDictionary<string, (string Kind, string Label)> dirObjInfoCache = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

        async Task<(string Kind, string Label)?> SafeGetDirObjInfoAsync(string dirObjId)
        {
            if (dirObjInfoCache.TryGetValue(dirObjId, out (string Kind, string Label) dirObjInfo))
            {
                return dirObjInfo;
            }

            try
            {
                logger.LogTrace("Getting directory object {DirObjId}", dirObjId);

                DirectoryObject directoryObject = (await graphClient.DirectoryObjects[dirObjId].GetAsync())!;
                return directoryObject switch
                {
                    User user => dirObjInfoCache[dirObjId] = ("user", $"{user.DisplayName} | {user.Mail}"),
                    Group group => dirObjInfoCache[dirObjId] = ("group", group.DisplayName!),
                    ServicePrincipal servicePrincipal => dirObjInfoCache[dirObjId] = ("service principal", servicePrincipal.DisplayName!),
                    _ => null,
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        bool headerWritten = false;

        async Task WriteHeaderAsync()
        {
            if (headerWritten)
                return;

            headerWritten = true;
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("Excess members:");
        }

        foreach ((string groupId, IEnumerable<string> memberIds) in excessMembersByGroup)
        {
            if (!memberIds.Any())
                continue;

            await WriteHeaderAsync();
            await writer.WriteLineAsync($"# Group {groupId}{(await SafeGetDirObjInfoAsync(groupId) is var (_, groupLabel) ? $" ('{groupLabel}')" : "")}");
            foreach (string memberId in memberIds)
            {
                await writer.WriteLineAsync($"\t# {memberId}{(await SafeGetDirObjInfoAsync(memberId) is var (memberKind, memberLabel) ? $" ({memberKind} '{memberLabel}')" : "")}");
            }
        }
    }

    public async Task AssignRolesAsync(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IEnumerable<string>>> roleAsgsByTarget,
        CancellationToken cancellationToken
    )
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { roleAsgsByTarget });

        try
        {
            foreach ((string rawTargetResourceId, IReadOnlyDictionary<string, IEnumerable<string>> roleAsgs) in roleAsgsByTarget)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    ParseResource(
                        rawTargetResourceId,
                        out ResourceIdentifier targetResourceId,
                        out string _,
                        out ResourceIdentifier subscriptionResourceId,
                        out ArmClient armClient
                    );

                    logger.LogInformation("Assigning roles to resource {ResourceType} '{ResourceName}'", targetResourceId.ResourceType, targetResourceId.Name);

                    Guid resourceGuidNs = GuidHelpers.CreateFromName(ProvisionerGuidNs, targetResourceId.ToString());
                    GenericResource resource = armClient.GetGenericResource(targetResourceId);
                    RoleAssignmentCollection roleAssignments = resource.GetRoleAssignments();

                    //var resourceName = resource.Data.Name;
                    IReadOnlyDictionary<Guid, ICollection<RoleAssignmentResource>> allRoleAsgResources =
                        await roleAssignments.GetAllAsync("atScope()")
                            .Where(x => x.Data.Scope == targetResourceId)
                            .Where(static x => x.Data.PrincipalType == RoleManagementPrincipalType.Group)
                            .GroupBy(static x => x.Data.PrincipalId!.Value)
                            .ToDictionaryAwaitAsync(
                                static g => ValueTask.FromResult(g.Key),
                                async static g => (ICollection<RoleAssignmentResource>)await g.AsAsyncEnumerable().ToListAsync(cancellationToken: default),
                                cancellationToken: default
                            );

                    foreach ((string groupIdOrName, IEnumerable<string> rawRoleDfns) in roleAsgs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            if (Guid.TryParse(groupIdOrName, out Guid groupId))
                            {
                                logger.LogTrace("Group {GroupId}", groupIdOrName);
                            }
                            else
                            {
                                if (await GetGroupIdAsync(groupIdOrName) is not { } groupId0)
                                    continue;

                                groupId = Guid.Parse(groupId0);
                                logger.LogTrace("Group {GroupId} ('{GroupName}')", groupId0, groupIdOrName);
                            }

                            Guid groupGuidNs = GuidHelpers.CreateFromName(resourceGuidNs, groupId.ToString());

                            ICollection<RoleAssignmentResource> groupRoleAsgResources = allRoleAsgResources.GetValueOrDefault(groupId) ?? new List<RoleAssignmentResource>();

                            bool groupSucceeded = true;
                            foreach (string rawRoleDfn in rawRoleDfns)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                try
                                {
                                    (ResourceIdentifier roleDfnResourceId, string roleDfnId) = await GetRoleAsync(rawRoleDfn, subscriptionResourceId, armClient);
                                    string roleAsgName = GuidHelpers.CreateFromName(groupGuidNs, roleDfnId).ToString();

                                    if (groupRoleAsgResources.FirstOrDefault(x => x.Data.RoleDefinitionId == roleDfnResourceId) is { } roleAsgResource)
                                    {
                                        groupRoleAsgResources.Remove(roleAsgResource);
                                        if (roleAsgResource.Data.Name == roleAsgName)
                                            continue;
                                        await roleAsgResource.DeleteAsync(WaitUntil.Completed, cancellationToken: default);
                                    }

                                    await roleAssignments.CreateOrUpdateAsync(
                                        WaitUntil.Completed,
                                        roleAsgName,
                                        new RoleAssignmentCreateOrUpdateContent(roleDfnResourceId, groupId) { PrincipalType = RoleManagementPrincipalType.Group },
                                        CancellationToken.None
                                    );
                                }
                                catch (Exception exception)
                                {
                                    groupSucceeded = false;
                                    logger.LogError(exception, "Error assigning role '{RoleDefinition}' to group '{GroupIdOrName}' on resource {ResourceId}", rawRoleDfn, groupIdOrName, rawTargetResourceId);
                                }
                            }

                            if (groupSucceeded)
                            {
                                foreach (RoleAssignmentResource roleAsgResource in groupRoleAsgResources)
                                {
                                    await roleAsgResource.DeleteAsync(WaitUntil.Completed, cancellationToken: default);
                                }
                            }
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            logger.LogError(exception, "Error assigning roles to group '{GroupIdOrName}' on resource {ResourceId}", groupIdOrName, rawTargetResourceId);
                        }
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Error assigning roles on resource {ResourceId}", rawTargetResourceId);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error assigning roles");
        }
    }

    public async Task AssignKvAccessPoliciesAsync(
        IReadOnlyDictionary<string, IEnumerable<KvAccessDescriptor>> accessDescriptorsByKv,
        CancellationToken cancellationToken
    )
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { accessDescriptorsByKv });

        try
        {
            foreach ((string rawKvResourceId, IEnumerable<KvAccessDescriptor> accessDescriptors) in accessDescriptorsByKv)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ParseResource(
                    rawKvResourceId,
                    out ResourceIdentifier kvResourceId,
                    out string _,
                    out ResourceIdentifier _,
                    out ArmClient armClient
                );

                logger.LogInformation("Assigning access policies on key vault '{KvName}'", kvResourceId.Name);

                KeyVaultResource kvResource = armClient.GetKeyVaultResource(kvResourceId);

                foreach (IEnumerable<KvAccessDescriptor> chunk in accessDescriptors.Chunk(16))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await kvResource.UpdateAccessPolicyAsync(
                            AccessPolicyUpdateKind.Add,
                            new KeyVaultAccessPolicyParameters(
                                new KeyVaultAccessPolicyProperties(
                                    await chunk
                                        .ToAsyncEnumerable()
                                        .SelectAwait(async x => (GroupId: await GetGroupIdAsync(x.GroupName), x.ReadOnly))
                                        .Where(static x => x.GroupId is not null)
                                        .Select(
                                            x =>
                                                new KeyVaultAccessPolicy(
                                                    tenantId,
                                                    x.GroupId!,
                                                    x.ReadOnly ? ReadOnlyIdentityAccessPermissions : ReadWriteIdentityAccessPermissions
                                                )
                                        )
                                        .ToArrayAsync(CancellationToken.None)
                                )
                            ),
                            CancellationToken.None
                        );
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Error assigning access policies on key vault '{KvName}'", kvResourceId.Name);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error assigning Azure Key Vault policies");
        }
    }

    private void ParseResource(
        string rawResourceId, out ResourceIdentifier targetResourceId,
        out string subscriptionId,
        out ResourceIdentifier subscriptionResourceId,
        out ArmClient armClient
    )
    {
        targetResourceId = ResourceIdentifier.Parse(rawResourceId);
        subscriptionId = targetResourceId.SubscriptionId!;
        subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        armClient = armClients.TryGetValue(subscriptionId, out ArmClient? armClient0)
            ? armClient0
            : armClients[subscriptionId] = new ArmClient(new AzureCliCredential(), subscriptionId);
    }

    private async Task<(ResourceIdentifier ResourceId, string Id)> GetRoleAsync(
        string rawRole, ResourceIdentifier subscriptionResourceId, ArmClient armClient
    )
    {
        using var activity = Observability.ActivitySource.StartMethodActivity(logger, new { rawRole, subscriptionResourceId }, logLevel: LogLevel.Trace);

        (ResourceIdentifier, string) key = (subscriptionResourceId, rawRole);
        return roleCache.TryGetValue(key, out var roleTuple)
            ? roleTuple
            : roleCache[key] = await CoreGetRoleAsync();

        async Task<(ResourceIdentifier ResourceId, string Id)> CoreGetRoleAsync()
        {
            ResourceIdentifier roleResourceId;
            string roleId;

            if (Guid.TryParse(rawRole, out _))
            {
                roleResourceId = AuthorizationRoleDefinitionResource.CreateResourceIdentifier(
                    subscriptionResourceId.ToString(), new ResourceIdentifier(rawRole)
                );
                roleId = rawRole;
            }
            else
            {
                AuthorizationRoleDefinitionResource roleResource =
                    await armClient.GetAuthorizationRoleDefinitions(subscriptionResourceId)
                        .GetAllAsync($"roleName eq '{rawRole}'")
                        .FirstOrDefaultAsync()
                    ?? throw new ProvisioningException($"Role '{rawRole}' not found");
                roleResourceId = roleResource.Id;
                roleId = roleResource.Data.Name;
            }

            return (roleResourceId, roleId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<string?> GetGroupIdAsync(string name) => (await GetGroupAsync(name))?.Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string MakeOdataId(string id, bool owner) => $"https://graph.microsoft.com/v1.0/{(owner ? "users" : "directoryObjects")}/{id}";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReferenceCreate MakeReferenceCreate(string id, bool owner) => new() { OdataId = MakeOdataId(id, owner) };
}
