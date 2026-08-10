namespace SQPortal.Data;

///////// test-only: delete this file when AD is linked \\\\\\\\\\

/// <summary>A click-to-sign-in identity for the test environment; no password.</summary>
public sealed record TestUser(string Username, string DisplayName, string Description);

/// <summary>
/// Hardcoded test roster, used only until Windows SSO is linked (see the
/// marked AD block in Program.cs). No role system exists; users differ by
/// name only.
/// </summary>
public static class TestUsers
{
    public static readonly IReadOnlyList<TestUser> All = new[]
    {
        new TestUser("test.admin",   "Admin (Test)",          "Full portal access — settings, cases, reports"),
        new TestUser("test.staff",   "SQ Staff (Test)",       "Day-to-day case handling"),
        new TestUser("test.manager", "Branch Manager (Test)", "Reviews weekly branch reports")
    };

    /// <summary>Roster lookup; unknown names return null.</summary>
    public static TestUser? Find(string? username) =>
        string.IsNullOrWhiteSpace(username)
            ? null
            : All.FirstOrDefault(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
}
