namespace SQPortal.Data;

///////// test-only: delete this file when AD is linked \\\\\\\\\\

/// <summary>
/// A click-to-sign-in identity for the test environment. No passwords — the
/// login page lists these and signing in is one press.
/// </summary>
public sealed record TestUser(string Username, string DisplayName, string Description);

/// <summary>
/// The hardcoded test roster. Edit freely — these exist only until Windows SSO
/// is linked (see the marked AD block in Program.cs), then this file is deleted.
/// The portal has no role system yet, so the users differ by name only.
/// </summary>
public static class TestUsers
{
    public static readonly IReadOnlyList<TestUser> All = new[]
    {
        new TestUser("test.admin",   "Admin (Test)",          "Full portal access — settings, cases, reports"),
        new TestUser("test.staff",   "SQ Staff (Test)",       "Day-to-day case handling"),
        new TestUser("test.manager", "Branch Manager (Test)", "Reviews weekly branch reports")
    };

    /// <summary>Roster lookup — anything not on the list is rejected.</summary>
    public static TestUser? Find(string? username) =>
        string.IsNullOrWhiteSpace(username)
            ? null
            : All.FirstOrDefault(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
}
