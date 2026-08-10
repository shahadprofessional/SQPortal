namespace SQPortal.Helpers;

/// <summary>
/// Field rules used as [RegularExpression] regexes, HTML pattern attributes,
/// and the forms' typing filters. Deliberately unanchored: both attribute
/// validators match the whole value, and the page scripts add ^(?:…)$.
/// </summary>
public static class FieldPatterns
{
    /// <summary>Letters and spaces, at least one character.</summary>
    public const string Name = "[A-Za-z ]+";

    /// <summary>Letters and spaces, or nothing at all.</summary>
    public const string NameOptional = "[A-Za-z ]*";

    /// <summary>Exactly eight digits.</summary>
    public const string Phone = "[0-9]{8}";

    /// <summary>Letters and digits, no spaces or symbols.</summary>
    public const string AlphanumericOptional = "[A-Za-z0-9]*";

    /// <summary>Letters, digits and spaces — for comments and notes.</summary>
    public const string TextOptional = "[A-Za-z0-9 ]*";

    public const string NameMessage = "{0} may only contain letters and spaces.";
    public const string PhoneMessage = "{0} must be exactly 8 digits.";
    public const string AlphanumericMessage = "{0} may only contain letters and numbers.";
    public const string TextMessage = "{0} may only contain letters, numbers and spaces.";
}
