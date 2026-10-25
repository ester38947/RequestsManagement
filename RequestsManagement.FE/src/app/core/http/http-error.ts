import { HttpErrorResponse } from '@angular/common/http';

import { ApiResponse, ProblemDetails } from './api-response';

const GENERIC_ERROR = 'אירעה שגיאה. נסו שוב מאוחר יותר.';
const NETWORK_ERROR = 'אין חיבור לשרת. ודאו שה-API פועל ונסו שוב.';

/**
 * Converts an HTTP error into a user-facing message.
 * Priority order: validation errors -> ProblemDetails detail -> envelope error -> a generic message.
 */
export function toUserMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return GENERIC_ERROR;
  }

  if (error.status === 0) {
    return NETWORK_ERROR;
  }

  const body = error.error as (ProblemDetails & Partial<ApiResponse<unknown>>) | null;
  if (!body || typeof body !== 'object') {
    return GENERIC_ERROR;
  }

  const validationMessages = body.errors ? Object.values(body.errors).flat() : [];
  if (validationMessages.length > 0) {
    return validationMessages.join(' ');
  }

  return body.detail ?? body.error ?? body.title ?? GENERIC_ERROR;
}
