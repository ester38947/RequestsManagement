using MongoDB.Bson.Serialization.Attributes;
using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dal.Models;

/// <summary>
/// A request document in the "requests" collection.
/// Enums are stored as Int32 (see below); Version is used for optimistic concurrency.
/// </summary>
public sealed class RequestModel : BaseEntity
{
    public string Title { get; set; } = default!;

    public string OrganizationName { get; set; } = default!;

    // Stored as Int32 (the enum default) so sorting by Status/Priority reflects the enum order
    // rather than an alphabetical order of names. The enum values are ordered by severity/flow.
    public RequestStatus Status { get; set; }

    public RequestPriority Priority { get; set; }

    public string? AssignedTo { get; set; }

    /// <summary>RowVersion for detecting a concurrent update (optimistic concurrency). Starts at 0, increments on each update.</summary>
    public int Version { get; set; }
}
