using System.ComponentModel.DataAnnotations;

namespace Hermodr;

/// <summary>
/// An Entity Framework Core entity that represents a custom extension attribute
/// for an audit trail entry.
/// </summary>
public class DbAuditTrailAttribute
{
    /// <summary>
    /// Gets or sets the unique identifier of the attribute.
    /// </summary>
    [Key]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the identifier of the parent audit trail entry.
    /// </summary>
    public string AuditTrailEntryId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the attribute key.
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the attribute value.
    /// </summary>
    public string Value { get; set; } = null!;
}
