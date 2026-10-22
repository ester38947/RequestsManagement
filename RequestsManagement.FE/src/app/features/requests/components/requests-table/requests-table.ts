import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule, Sort, SortDirection as MatSortDirection } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';

import { ALLOWED_TRANSITIONS, PRIORITY_LABELS, STATUS_LABELS } from '../../models/request-labels';
import {
  RequestListItem,
  RequestPriority,
  RequestStatus,
  SortDirection,
  SortField,
} from '../../models/request.models';

export interface PageChange {
  page: number;
  pageSize: number;
}

export interface SortChange {
  sortBy: SortField;
  sortDirection: SortDirection;
}

export interface StatusChangeRequest {
  item: RequestListItem;
  newStatus: RequestStatus;
}

/**
 * טבלת פניות (רכיב תצוגה). הדפדוף והמיון מבוצעים בשרת — הרכיב רק מציג את הדף הנוכחי
 * ומדווח על שינויי עמוד/מיון ועל בקשות פעולה דרך outputs.
 */
@Component({
  selector: 'app-requests-table',
  imports: [
    DatePipe,
    MatButtonModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
  ],
  templateUrl: './requests-table.html',
  styleUrl: './requests-table.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequestsTableComponent {
  readonly items = input.required<RequestListItem[]>();
  readonly totalCount = input.required<number>();
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly sortBy = input.required<SortField>();
  readonly sortDirection = input.required<SortDirection>();
  readonly loading = input(false);
  readonly updatingIds = input<ReadonlySet<string>>(new Set());

  readonly pageChange = output<PageChange>();
  readonly sortChange = output<SortChange>();
  readonly statusChange = output<StatusChangeRequest>();
  readonly historyRequested = output<RequestListItem>();

  protected readonly columns = [
    'title',
    'organizationName',
    'status',
    'priority',
    'assignedTo',
    'createdAt',
    'updatedAt',
    'actions',
  ];
  // תואם ל-MaxPageSize של השרת (100).
  protected readonly pageSizeOptions = [10, 25, 50, 100];

  protected readonly matSortDirection = computed<MatSortDirection>(() =>
    this.sortDirection() === 'Asc' ? 'asc' : 'desc',
  );

  // ה-row ב-matCellDef אינו מוקלד, לכן גישה לתוויות דרך פונקציות מוקלדות.
  protected statusLabel(status: RequestStatus): string {
    return STATUS_LABELS[status];
  }

  protected priorityLabel(priority: RequestPriority): string {
    return PRIORITY_LABELS[priority];
  }

  protected allowedTransitions(status: RequestStatus): readonly RequestStatus[] {
    return ALLOWED_TRANSITIONS[status];
  }

  protected onSort(sort: Sort): void {
    this.sortChange.emit({
      sortBy: sort.active as SortField,
      sortDirection: sort.direction === 'asc' ? 'Asc' : 'Desc',
    });
  }

  protected onPage(event: PageEvent): void {
    this.pageChange.emit({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }
}
