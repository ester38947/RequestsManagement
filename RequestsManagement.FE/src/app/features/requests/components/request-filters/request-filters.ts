import { ChangeDetectionStrategy, Component, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';

import { PRIORITIES, PRIORITY_LABELS, STATUSES, STATUS_LABELS } from '../../models/request-labels';
import { RequestFilters, RequestPriority, RequestStatus } from '../../models/request.models';

/**
 * Filter panel (presentational component): holds the form and reports changes through an output only —
 * no access to a service. Debounce so typing in the text fields does not send a request per character.
 */
@Component({
  selector: 'app-request-filters',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
  ],
  templateUrl: './request-filters.html',
  styleUrl: './request-filters.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequestFiltersComponent {
  readonly filtersChange = output<RequestFilters>();

  protected readonly statuses = STATUSES;
  protected readonly priorities = PRIORITIES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly priorityLabels = PRIORITY_LABELS;

  protected readonly form = new FormGroup({
    status: new FormControl<RequestStatus | null>(null),
    priority: new FormControl<RequestPriority | null>(null),
    organizationName: new FormControl('', { nonNullable: true }),
    assignedTo: new FormControl('', { nonNullable: true }),
    createdFrom: new FormControl<Date | null>(null),
    createdTo: new FormControl<Date | null>(null),
  });

  constructor() {
    this.form.valueChanges
      .pipe(
        debounceTime(300),
        map(() => this.form.getRawValue()),
        distinctUntilChanged(sameFilters),
        takeUntilDestroyed(),
      )
      .subscribe((filters) => this.filtersChange.emit(filters));
  }

  protected clear(): void {
    this.form.reset();
  }
}

function sameFilters(a: RequestFilters, b: RequestFilters): boolean {
  return (
    a.status === b.status &&
    a.priority === b.priority &&
    a.organizationName.trim() === b.organizationName.trim() &&
    a.assignedTo.trim() === b.assignedTo.trim() &&
    a.createdFrom?.getTime() === b.createdFrom?.getTime() &&
    a.createdTo?.getTime() === b.createdTo?.getTime()
  );
}
