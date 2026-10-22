namespace RequestsManagement.Dto.Enums;

/// <summary>
/// סטטוס הפנייה. מאוחסן כמחרוזת ב-Mongo (ראה מיפוי במודל) לקריאוּת.
/// </summary>
public enum RequestStatus
{
    New = 0,
    InProgress = 1,
    Waiting = 2,
    Completed = 3,
}
