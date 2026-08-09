using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.ViewModels.Account;

public class LoginViewModel
{
    [Required, StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>Where to land after sign-in. Only ever a local URL.</summary>
    public string? ReturnUrl { get; set; }

    /// <summary>True when no credentials are configured — sign-in cannot succeed until Auth is set.</summary>
    public bool NotConfigured { get; set; }
}
