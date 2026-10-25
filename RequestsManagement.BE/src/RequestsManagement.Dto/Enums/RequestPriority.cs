namespace RequestsManagement.Dto.Enums;

/// <summary>
/// Request priority. Stored as Int32 in Mongo so sorting follows the enum order
/// (Low &lt; Medium &lt; High) rather than alphabetically.
/// </summary>
public enum RequestPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
}
