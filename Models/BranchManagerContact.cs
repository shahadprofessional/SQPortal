namespace SQPortal.Models;

/// <summary>
/// A branch manager as the portal shows and mails them, resolved for one
/// branch on one date. Every field is a copy of Active Directory.
/// </summary>
/// <param name="Username">AD username (sAMAccountName).</param>
/// <param name="FullName">AD display name; empty when AD has none.</param>
/// <param name="Email">AD email address; empty when AD has none.</param>
/// <param name="IsActive">False once they are in no branch-manager group any more.</param>
public record BranchManagerContact(string Username, string FullName, string Email, bool IsActive)
{
    /// <summary>Full name when AD has one, otherwise the username.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(FullName) ? Username : FullName;

    public bool HasEmail => !string.IsNullOrWhiteSpace(Email);
}
