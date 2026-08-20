namespace SQPortal.Services.Ad;

/// <summary>
/// The reader in use while Active Directory is not linked yet (Ad:Enabled is
/// false, no group names configured, or the LDAP reader is still commented
/// out). Every lookup fails, so a sync reports "not linked" and leaves the
/// stored roster exactly as it is — it never empties or invents managers.
/// </summary>
public class DisabledAdDirectoryReader : IAdDirectoryReader
{
    public bool IsLinked => false;

    public Task<IReadOnlyList<AdUser>> GetGroupMembersAsync(string groupName, CancellationToken cancellationToken) =>
        throw new AdDirectoryException(
            "Active Directory is not linked. Set Ad:Enabled and the group names in appsettings.json, " +
            "and follow the AD markers in SQPortal.csproj and Services/Ad/LdapDirectoryReader.cs.");
}
