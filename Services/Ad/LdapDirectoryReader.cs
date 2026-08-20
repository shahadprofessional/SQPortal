/////////remove when you want to link AD\\\\\\\\\\
// The real Active Directory reader, kept commented out for the same reason as
// the Windows SSO block in Program.cs: the portal must build and run on a
// machine that is not domain-joined. To link it:
//   1. Uncomment the System.DirectoryServices.Protocols package reference in
//      SQPortal.csproj (same marker) and restore.
//   2. Delete the "/*" line just below and the "*/" line at the end of this
//      file, so the class below compiles.
//   3. Uncomment the LdapDirectoryReader registration in Program.cs (same
//      marker) — it replaces DisabledAdDirectoryReader.
//   4. Fill in the "Ad" section of appsettings.json with the group names IT
//      Security gives you, one per branch, and set Ad:Enabled to true.
// The connection authenticates as the app's own service account over
// Kerberos/NTLM, so that account needs read access to the groups — nothing
// more. Nothing here writes to the directory.
/*
using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;

namespace SQPortal.Services.Ad;

/// <summary>Reads branch-manager groups over LDAP. Read-only by construction.</summary>
public class LdapDirectoryReader : IAdDirectoryReader, IDisposable
{
    // Matching rule LDAP_MATCHING_RULE_IN_CHAIN: also returns members of
    // nested groups, so a group of groups still resolves to people.
    private const string InChain = "1.2.840.113556.1.4.1941";

    // Bit 2 of userAccountControl is ACCOUNTDISABLE; a resigned person whose
    // account is disabled but still in the group must not count as a manager.
    private const string DisabledAccount = "(userAccountControl:1.2.840.113556.1.4.803:=2)";

    private readonly AdSettings _settings;
    private readonly ILogger<LdapDirectoryReader> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private LdapConnection? _connection;
    private bool _disposed;

    public LdapDirectoryReader(IOptions<AdSettings> settings, ILogger<LdapDirectoryReader> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsLinked => _settings.IsLinked;

    public async Task<IReadOnlyList<AdUser>> GetGroupMembersAsync(string groupName, CancellationToken cancellationToken)
    {
        if (!IsLinked)
        {
            throw new AdDirectoryException(
                "Active Directory is not linked. Set Ad:Enabled and the group names in appsettings.json.");
        }

        // One request at a time: LdapConnection is not thread-safe and the
        // sync is the only caller.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var connection = Connect();
            var groupDn = FindGroupDn(connection, groupName);

            var filter =
                "(&(objectCategory=person)(objectClass=user)(!" + DisabledAccount + ")" +
                "(memberOf:" + InChain + ":=" + Escape(groupDn) + "))";

            var entries = Search(connection, filter, "sAMAccountName", "userPrincipalName", "displayName", "cn", "mail");

            var members = new List<AdUser>();
            foreach (var entry in entries)
            {
                var username = Value(entry, "sAMAccountName");
                if (string.IsNullOrEmpty(username)) continue;

                var upn = Value(entry, "userPrincipalName");
                var fullName = Value(entry, "displayName");
                if (string.IsNullOrEmpty(fullName)) fullName = Value(entry, "cn");

                members.Add(new AdUser(username, upn, fullName, Value(entry, "mail")));
            }

            return members;
        }
        catch (AdDirectoryException)
        {
            throw;
        }
        catch (Exception ex) when (ex is LdapException or DirectoryOperationException or InvalidOperationException)
        {
            // A dropped connection must not poison every later sync.
            Reset();
            throw new AdDirectoryException($"Reading AD group \"{groupName}\" failed: {ex.Message}", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private LdapConnection Connect()
    {
        if (_connection != null) return _connection;

        var identifier = string.IsNullOrWhiteSpace(_settings.Server)
            ? new LdapDirectoryIdentifier(Environment.UserDomainName, fullyQualifiedDnsHostName: true, connectionless: false)
            : new LdapDirectoryIdentifier(_settings.Server, fullyQualifiedDnsHostName: true, connectionless: false);

        // No credentials: the app's service account is used, so no password
        // for the directory is ever stored in this application.
        var connection = new LdapConnection(identifier, (NetworkCredential?)null, AuthType.Negotiate)
        {
            AuthType = AuthType.Negotiate
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.Sealing = true;
        connection.SessionOptions.Signing = true;
        connection.Timeout = TimeSpan.FromSeconds(30);
        connection.Bind();

        _logger.LogInformation("Bound to Active Directory at {Server}.",
            string.IsNullOrWhiteSpace(_settings.Server) ? "the machine's own domain" : _settings.Server);

        _connection = connection;
        return connection;
    }

    private string FindGroupDn(LdapConnection connection, string groupName)
    {
        var escaped = Escape(groupName);
        var filter =
            "(&(objectClass=group)(|(sAMAccountName=" + escaped + ")(cn=" + escaped + ")(distinguishedName=" + escaped + ")))";

        var entries = Search(connection, filter, "distinguishedName");
        if (entries.Count == 0)
        {
            throw new AdDirectoryException(
                $"AD group \"{groupName}\" was not found. Check the name against what IT Security gave you.");
        }
        if (entries.Count > 1)
        {
            throw new AdDirectoryException(
                $"AD group \"{groupName}\" matches {entries.Count} groups. Use its full distinguished name instead.");
        }

        return entries[0].DistinguishedName;
    }

    private List<SearchResultEntry> Search(LdapConnection connection, string filter, params string[] attributes)
    {
        var request = new SearchRequest(
            _settings.SearchBase, // empty means the domain root
            filter,
            SearchScope.Subtree,
            attributes);

        // Groups can hold more members than one page returns.
        var pageControl = new PageResultRequestControl(500) { IsCritical = false };
        request.Controls.Add(pageControl);
        request.Controls.Add(new SearchOptionsControl(SearchOption.DomainScope));

        var results = new List<SearchResultEntry>();
        while (true)
        {
            var response = (SearchResponse)connection.SendRequest(request);

            foreach (SearchResultEntry entry in response.Entries)
            {
                results.Add(entry);
            }

            var cookie = response.Controls
                .OfType<PageResultResponseControl>()
                .FirstOrDefault()?.Cookie;

            if (cookie == null || cookie.Length == 0) break;
            pageControl.Cookie = cookie;
        }

        return results;
    }

    private static string Value(SearchResultEntry entry, string attribute)
    {
        var values = entry.Attributes[attribute];
        if (values == null || values.Count == 0) return string.Empty;
        return values[0]?.ToString()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// RFC 4515 escaping, so a group name can never be read as filter syntax.
    /// </summary>
    private static string Escape(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\5c"); break;
                case '*': sb.Append("\\2a"); break;
                case '(': sb.Append("\\28"); break;
                case ')': sb.Append("\\29"); break;
                case '\0': sb.Append("\\00"); break;
                case '/': sb.Append("\\2f"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private void Reset()
    {
        _connection?.Dispose();
        _connection = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
*/
/////////end of AD code\\\\\\\\\\
