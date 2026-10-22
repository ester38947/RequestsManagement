using RequestsManagement.Dto.Enums;

namespace RequestsManagement.Dto.Requests;

/// <summary>
/// דוח תוצאת bulk (Partial Success): כל פנייה מעובדת ומתועדת בנפרד, כשלון של אחת לא מבטל אחרות.
/// </summary>
public sealed record BulkUpdateStatusResultDto(
    int Total,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkStatusItemResultDto> Items);

/// <summary>
/// תוצאה לפנייה בודדת.
/// Version: בהצלחה – הגרסה החדשה; בקונפליקט/מעבר לא חוקי – הגרסה העדכנית ב-DB; ב-NotFound – null.
/// </summary>
public sealed record BulkStatusItemResultDto(
    string Id,
    BulkItemOutcome Outcome,
    int? Version,
    RequestStatus? CurrentStatus,
    string? Error);

public enum BulkItemOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    InvalidTransition = 3,
}
