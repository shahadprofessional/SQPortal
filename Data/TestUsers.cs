namespace SQPortal.Data;

///////// test-only: delete this file when AD is linked \\\\\\\\\\

/// <summary>
/// The identity every visitor is signed in as while Active Directory is not
/// linked. There is no login page and no password: the middleware marked
/// test-only in Program.cs signs the request in as this user. Once Windows SSO
/// is linked (see the marked AD block in Program.cs) the real account name
/// comes from the domain and this file goes away.
/// </summary>
public static class TestUser
{
    public const string Username = "test.admin";
    public const string DisplayName = "Admin (Test)";
}
