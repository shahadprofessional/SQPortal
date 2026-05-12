using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

public class BusinessPartner
{
    [Key]
    [MaxLength(40)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;
}
