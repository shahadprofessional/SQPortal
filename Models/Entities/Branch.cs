using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

public class Branch
{
    [Key]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
