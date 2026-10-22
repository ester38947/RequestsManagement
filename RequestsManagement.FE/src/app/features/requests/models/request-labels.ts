import { RequestPriority, RequestStatus } from './request.models';

export const STATUS_LABELS: Record<RequestStatus, string> = {
  New: 'חדשה',
  InProgress: 'בטיפול',
  Waiting: 'ממתינה',
  Completed: 'הושלמה',
};

export const PRIORITY_LABELS: Record<RequestPriority, string> = {
  Low: 'נמוכה',
  Medium: 'בינונית',
  High: 'גבוהה',
};

export const STATUSES: readonly RequestStatus[] = ['New', 'InProgress', 'Waiting', 'Completed'];
export const PRIORITIES: readonly RequestPriority[] = ['Low', 'Medium', 'High'];

/**
 * מראה של מכונת המצבים של ה-BE (RequestStatusTransitions) — משמש רק כדי להציג ב-UI
 * את המעברים הרלוונטיים. האכיפה עצמה בשרת: מעבר לא חוקי יחזיר 400.
 */
export const ALLOWED_TRANSITIONS: Record<RequestStatus, readonly RequestStatus[]> = {
  New: ['InProgress', 'Waiting'],
  InProgress: ['Waiting', 'Completed'],
  Waiting: ['InProgress', 'Completed'],
  Completed: [],
};
