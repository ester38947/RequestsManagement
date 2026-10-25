/** The uniform BE envelope (ApiResponseDto<T>). */
export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: string | null;
}

/** ProblemDetails (RFC 7807) as returned by the global exception handler. */
export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  [extension: string]: unknown;
}
