import { Injectable, signal } from '@angular/core';
import { Observable, Subject, catchError, map, of, switchMap, tap } from 'rxjs';

import { ApiResponse } from '../../../core/http/api-response';
import { BaseHttpService, QueryParams } from '../../../core/http/base-http.service';
import { toUserMessage } from '../../../core/http/http-error';
import {
  PagedResult,
  RequestListItem,
  RequestSearchQuery,
  RequestsSummary,
  StatusHistoryItem,
  UpdateStatusBody,
} from '../models/request.models';

/**
 * Access to the requests API. Every call opens the envelope and returns a clean domain type.
 * Holds the summary (aggregations) state in signals — shared, and auto-refreshed after a status update.
 */
@Injectable({ providedIn: 'root' })
export class RequestsService extends BaseHttpService {
  protected readonly servicePath = 'requests';

  private readonly _summary = signal<RequestsSummary | null>(null);
  private readonly _summaryLoading = signal(false);
  private readonly _summaryError = signal<string | null>(null);

  readonly summary = this._summary.asReadonly();
  readonly summaryLoading = this._summaryLoading.asReadonly();
  readonly summaryError = this._summaryError.asReadonly();

  // switchMap: a new refresh cancels a previous refresh still in flight.
  private readonly summaryRefresh$ = new Subject<void>();

  constructor() {
    super();
    this.summaryRefresh$
      .pipe(
        tap(() => {
          this._summaryLoading.set(true);
          this._summaryError.set(null);
        }),
        switchMap(() =>
          this.get<ApiResponse<RequestsSummary>>('summary').pipe(
            map((res) => res.data),
            catchError((err: unknown) => {
              this._summaryError.set(toUserMessage(err));
              return of(null);
            }),
          ),
        ),
      )
      .subscribe((summary) => {
        this._summaryLoading.set(false);
        if (summary) {
          this._summary.set(summary);
        }
      });
  }

  /** Fetch a page of requests — all filtering/sorting/paging happens on the server. */
  search(query: RequestSearchQuery): Observable<PagedResult<RequestListItem>> {
    return this.get<ApiResponse<PagedResult<RequestListItem>>>('', toQueryParams(query)).pipe(
      map((res) => res.data!),
    );
  }

  getById(id: string): Observable<RequestListItem> {
    return this.get<ApiResponse<RequestListItem>>(encodeURIComponent(id)).pipe(
      map((res) => res.data!),
    );
  }

  /** Status update. 409 (concurrent update) / 400 (invalid transition) arrive as HTTP errors for the caller to handle. */
  updateStatus(id: string, body: UpdateStatusBody): Observable<RequestListItem> {
    return this.patch<ApiResponse<RequestListItem>>(`${encodeURIComponent(id)}/status`, body).pipe(
      map((res) => res.data!),
      tap(() => this.refreshSummary()),
    );
  }

  getHistory(id: string): Observable<StatusHistoryItem[]> {
    return this.get<ApiResponse<StatusHistoryItem[]>>(`${encodeURIComponent(id)}/history`).pipe(
      map((res) => res.data ?? []),
    );
  }

  refreshSummary(): void {
    this.summaryRefresh$.next();
  }
}

function toQueryParams(query: RequestSearchQuery): QueryParams {
  const { filters } = query;
  return {
    page: query.page,
    pageSize: query.pageSize,
    sortBy: query.sortBy,
    sortDirection: query.sortDirection,
    search: query.search.trim(),
    status: filters.status,
    priority: filters.priority,
    organizationName: filters.organizationName.trim(),
    assignedTo: filters.assignedTo.trim(),
    createdFrom: filters.createdFrom ? startOfDay(filters.createdFrom).toISOString() : null,
    createdTo: filters.createdTo ? endOfDay(filters.createdTo).toISOString() : null,
  };
}

// A date range covers whole days in local time.
function startOfDay(date: Date): Date {
  const d = new Date(date);
  d.setHours(0, 0, 0, 0);
  return d;
}

function endOfDay(date: Date): Date {
  const d = new Date(date);
  d.setHours(23, 59, 59, 999);
  return d;
}
