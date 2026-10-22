// טיפוסים התואמים ל-DTOs של ה-BE (enums מגיעים כמחרוזות — JsonStringEnumConverter).

export type RequestStatus = 'New' | 'InProgress' | 'Waiting' | 'Completed';
export type RequestPriority = 'Low' | 'Medium' | 'High';
export type SortDirection = 'Asc' | 'Desc';
export type SortField =
  'createdAt' | 'updatedAt' | 'priority' | 'status' | 'organizationName' | 'title';

export interface RequestListItem {
  id: string;
  title: string;
  organizationName: string;
  status: RequestStatus;
  priority: RequestPriority;
  assignedTo: string | null;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/** סינון בלבד (ללא דפדוף/מיון/חיפוש) — הערך של פאנל הסינון. */
export interface RequestFilters {
  status: RequestStatus | null;
  priority: RequestPriority | null;
  organizationName: string;
  assignedTo: string;
  createdFrom: Date | null;
  createdTo: Date | null;
}

/** כל פרמטרי השליפה ששולחים לשרת. */
export interface RequestSearchQuery {
  page: number;
  pageSize: number;
  sortBy: SortField;
  sortDirection: SortDirection;
  search: string;
  filters: RequestFilters;
}

export interface StatusCount {
  status: RequestStatus;
  count: number;
}

export interface PriorityCount {
  priority: RequestPriority;
  count: number;
}

export interface RequestsSummary {
  total: number;
  byStatus: StatusCount[];
  byPriority: PriorityCount[];
}

export interface StatusHistoryItem {
  id: string;
  requestId: string;
  previousStatus: RequestStatus;
  newStatus: RequestStatus;
  changedAt: string;
  changedBy: string;
}

export interface UpdateStatusBody {
  newStatus: RequestStatus;
  expectedVersion: number;
  changedBy?: string;
}

export const EMPTY_FILTERS: RequestFilters = {
  status: null,
  priority: null,
  organizationName: '',
  assignedTo: '',
  createdFrom: null,
  createdTo: null,
};
