namespace BankingApi.Application.Common.Exceptions;

/// <summary>
/// Thrown when a command or query fails FluentValidation rules,
/// or when a business rule (e.g. same-account transfer) is violated.
/// Maps to HTTP 422 Unprocessable Entity.
/// Carries a dictionary of field-level error messages for RFC 7807 responses.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Field-level validation errors.
    /// Key   = property name (camelCase matches JSON property names).
    /// Value = one or more error messages for that field.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Single-message constructor — used for business-rule violations
    /// that are not tied to a specific field (e.g. same-account transfer).
    /// Stored under the key "general".
    /// </summary>
    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>
        {
            ["general"] = new[] { message }
        };
    }

    /// <summary>
    /// Multi-field constructor — used by the Wolverine FluentValidation
    /// middleware when one or more validators fail.
    /// </summary>
    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>
    /// Convenience constructor from a FluentValidation ValidationResult.
    /// Groups failures by property name.
    /// </summary>
    public ValidationException(
        IEnumerable<(string PropertyName, string ErrorMessage)> failures)
        : base("One or more validation errors occurred.")
    {
        Errors = failures
            .GroupBy(f => ToCamelCase(f.PropertyName),
                     f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    public ValidationException(string message, Exception inner)
        : base(message, inner)
    {
        Errors = new Dictionary<string, string[]>
        {
            ["general"] = new[] { message }
        };
    }

    private static string ToCamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s
            : char.ToLowerInvariant(s[0]) + s[1..];
}