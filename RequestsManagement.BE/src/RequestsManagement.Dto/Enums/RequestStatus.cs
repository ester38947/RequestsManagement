namespace RequestsManagement.Dto.Enums;

/// <summary>
/// Request status. Stored as Int32 in Mongo so that sorting by the enum follows its
/// numeric order; the API still exposes and accepts the names via JsonStringEnumConverter.
/// </summary>
public enum RequestStatus
{
    New = 0,
    InProgress = 1,
    Waiting = 2,
    Completed = 3,
}
