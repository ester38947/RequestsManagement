import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  finalize,
  map,
  merge,
  of,
  switchMap,
  tap,
} from 'rxjs';

import { toUserMessage } from '../../../core/http/http-error';
import { RequestFiltersComponent } from '../components/request-filters/request-filters';
import {
  HistoryDialogData,
  RequestHistoryDialogComponent,
} from '../components/request-history-dialog/request-history-dialog';
import {
  PageChange,
  RequestsTableComponent,
  SortChange,
  StatusChangeRequest,
} from '../components/requests-table/requests-table';
import { RequestsSummaryComponent } from '../components/requests-summary/requests-summary';
import { STATUS_LABELS } from '../models/request-labels';
import {
  EMPTY_FILTERS,
  PagedResult,
  RequestFilters,
  RequestListItem,
  RequestSearchQuery,
} from '../models/request.models';
import { RequestsService } from '../services/requests.service';

const SEARCH_DEBOUNCE_MS = 300;

const INITIAL_QUERY: RequestSearchQuery = {
  page: 1,
  pageSize: 25,
  sortBy: 'createdAt',
  sortDirection: 'Desc',
  search: '',
  filters: EMPTY_FILTERS,
};

/**
 * עמוד הפניות (smart container): מחזיק את מצב השאילתה ב-signals, מתזמר את ה-service,
 * ומעביר נתונים לרכיבי התצוגה (summary / filters / table).
 *
 * זרימת השליפה: כל שינוי בשאילתה (חיפוש, סינון, עמוד, מיון) מעדכן את ה-signal `query`,
 * וזרם אחד עם switchMap שולח בקשה לשרת. switchMap מבטל את הבקשה הקודמת שעדיין בדרך
 * (ה-HTTP request עצמו מבוטל), כך שתשובה ישנה לא יכולה לדרוס תשובה חדשה.
 * החיפוש עובר קודם debounceTime + distinctUntilChanged כדי לא לשלוח בקשה על כל הקשה.
 */
@Component({
  selector: 'app-request-list',
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    RequestFiltersComponent,
    RequestsSummaryComponent,
    RequestsTableComponent,
  ],
  templateUrl: './request-list.html',
  styleUrl: './request-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequestListComponent {
  private readonly service = inject(RequestsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly searchControl = new FormControl('', { nonNullable: true });

  protected readonly query = signal<RequestSearchQuery>(INITIAL_QUERY);
  protected readonly result = signal<PagedResult<RequestListItem> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly updatingIds = signal<ReadonlySet<string>>(new Set());

  protected readonly summary = this.service.summary;
  protected readonly summaryLoading = this.service.summaryLoading;
  protected readonly summaryError = this.service.summaryError;

  /** טעינה חוזרת של אותה שאילתה (כפתור "נסו שוב"). */
  private readonly reload$ = new Subject<void>();

  constructor() {
    this.searchControl.valueChanges
      .pipe(
        map((term) => term.trim()),
        debounceTime(SEARCH_DEBOUNCE_MS),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((search) => this.patchQuery({ search, page: 1 }));

    merge(toObservable(this.query), this.reload$.pipe(map(() => this.query())))
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.error.set(null);
        }),
        switchMap((query) =>
          this.service.search(query).pipe(
            catchError((err: unknown) => {
              this.error.set(toUserMessage(err));
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.loading.set(false);
        if (result) {
          this.result.set(result);
        }
      });

    this.service.refreshSummary();
  }

  protected onFiltersChange(filters: RequestFilters): void {
    this.patchQuery({ filters, page: 1 });
  }

  protected onSortChange(sort: SortChange): void {
    this.patchQuery({ ...sort, page: 1 });
  }

  protected onPageChange(change: PageChange): void {
    this.patchQuery(change);
  }

  protected retry(): void {
    this.reload$.next();
  }

  protected refreshSummary(): void {
    this.service.refreshSummary();
  }

  /** עדכון סטטוס עם Optimistic Concurrency: שולחים את הגרסה שהמשתמש ראה. */
  protected onStatusChange({ item, newStatus }: StatusChangeRequest): void {
    this.setUpdating(item.id, true);

    this.service
      .updateStatus(item.id, { newStatus, expectedVersion: item.version })
      .pipe(
        finalize(() => this.setUpdating(item.id, false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (updated) => {
          this.replaceRow(updated);
          this.notify(`הסטטוס של "${updated.title}" עודכן ל${STATUS_LABELS[updated.status]}.`);
        },
        error: (err: unknown) => this.handleUpdateError(item, err),
      });
  }

  protected openHistory(item: RequestListItem): void {
    this.dialog.open<RequestHistoryDialogComponent, HistoryDialogData>(
      RequestHistoryDialogComponent,
      {
        data: { requestId: item.id, title: item.title },
        width: '560px',
        maxWidth: '95vw',
        autoFocus: 'dialog',
      },
    );
  }

  private handleUpdateError(item: RequestListItem, err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      // מישהו אחר עדכן את הפנייה: מרעננים את השורה לגרסה העדכנית ומסבירים למשתמש.
      this.notify(
        'הפנייה עודכנה בינתיים על ידי משתמש אחר. הנתונים רועננו — בדקו את הסטטוס הנוכחי ונסו שוב.',
        10_000,
      );
      this.refreshRow(item.id);
      this.service.refreshSummary();
      return;
    }

    if (err instanceof HttpErrorResponse && err.status === 404) {
      this.notify('הפנייה כבר לא קיימת. הרשימה רועננה.');
      this.reload$.next();
      return;
    }

    this.notify(toUserMessage(err));
  }

  private refreshRow(id: string): void {
    this.service
      .getById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (fresh) => this.replaceRow(fresh),
        error: () => this.reload$.next(),
      });
  }

  private replaceRow(updated: RequestListItem): void {
    this.result.update((current) =>
      current
        ? { ...current, items: current.items.map((i) => (i.id === updated.id ? updated : i)) }
        : current,
    );
  }

  private setUpdating(id: string, updating: boolean): void {
    this.updatingIds.update((ids) => {
      const next = new Set(ids);
      if (updating) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  private patchQuery(patch: Partial<RequestSearchQuery>): void {
    this.query.update((q) => ({ ...q, ...patch }));
  }

  private notify(message: string, duration = 5000): void {
    this.snackBar.open(message, 'סגירה', { duration });
  }
}
