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
 * A mirror of the BE state machine (RequestStatusTransitions) — used only to show the relevant
 * transitions in the UI. The enforcement itself is on the server: an invalid transition returns 400.
 */
export const ALLOWED_TRANSITIONS: Record<RequestStatus, readonly RequestStatus[]> = {
  New: ['InProgress', 'Waiting'],
  InProgress: ['Waiting', 'Completed'],
  Waiting: ['InProgress', 'Completed'],
  Completed: [],
};
