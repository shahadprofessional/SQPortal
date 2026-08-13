using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>
/// One row per applied schema version. Written by Scripts/database.sql and
/// checked at startup, so a database that missed the script surfaces as a
/// clear log message instead of scattered runtime errors.
/// </summary>
public class SchemaVersion
{
    [Key]
    [MaxLength(20)]
    public string Version { get; set; } = string.Empty;

    public DateTime AppliedAtUtc { get; set; }
}
