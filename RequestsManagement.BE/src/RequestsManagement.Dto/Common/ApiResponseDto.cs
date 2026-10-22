namespace RequestsManagement.Dto.Common;

/// <summary>
/// עטיפה אחידה לכל תגובות ה-API. כל endpoint מחזיר ApiResponseDto&lt;T&gt;
/// כדי לשמור על חוזה עקבי ללקוח (Success/Data/Error).
/// </summary>
public sealed class ApiResponseDto<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string? Error { get; init; }

    public static ApiResponseDto<T> Ok(T data) => new() { Success = true, Data = data };

    public static ApiResponseDto<T> Fail(string error) => new() { Success = false, Error = error };
}
