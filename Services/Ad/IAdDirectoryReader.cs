namespace SQPortal.Services.Ad;

/// <summary>
/// Read-only view of Active Directory. The portal only ever reads: who is in a
/// branch-manager group, and their name and email. Changing any of it is done
/// in AD by whoever owns identity, never here.
/// </summary>
public interface IAdDirectoryReader
{
    /// <summary>True when the reader can actually reach a directory.</summary>
    bool IsLinked { get; }

    /// <summary>
    /// The enabled user accounts in one group. Throws
    /// <see cref="AdDirectoryException"/> when the group cannot be read, so a
    /// sync fails loudly instead of silently emptying the roster.
    /// </summary>
    Task<IReadOnlyList<AdUser>> GetGroupMembersAsync(string groupName, CancellationToken cancellationToken);
}

/// <summary>One AD account, as the portal stores it.</summary>
/// <param name="Username">sAMAccountName — the portal's key for the person.</param>
/// <param name="UserPrincipalName">Full username including the domain suffix.</param>
/// <param name="FullName">displayName, used when addressing them.</param>
/// <param name="Email">mail attribute; empty when AD has none.</param>
public record AdUser(string Username, string UserPrincipalName, string FullName, string Email);

/// <summary>A directory lookup failed; the caller must not treat it as "no members".</summary>
public class AdDirectoryException : Exception
{
    public AdDirectoryException(string message) : base(message) { }

    public AdDirectoryException(string message, Exception inner) : base(message, inner) { }
}
