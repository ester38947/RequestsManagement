namespace RequestsManagement.Dto.Common;

/// <summary>
/// Uniform wrapper for every API response. Each endpoint returns ApiResponseDto&lt;T&gt;
/// to keep a consistent contract for the client (Success/Data/Error).
/// </summary>
public sealed class ApiResponseDto<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string? Error { get; init; }

    public static ApiResponseDto<T> Ok(T data) => new() { Success = true, Data = data };

    public static ApiResponseDto<T> Fail(string error) => new() { Success = false, Error = error };
}
