import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';

import { PRIORITY_LABELS, STATUS_LABELS } from '../../models/request-labels';
import { RequestsSummary } from '../../models/request.models';

/** כרטיסי סיכום (aggregations): סך הכל, לפי סטטוס ולפי עדיפות. רכיב תצוגה בלבד. */
@Component({
  selector: 'app-requests-summary',
  imports: [DecimalPipe, MatButtonModule, MatProgressBarModule],
  templateUrl: './requests-summary.html',
  styleUrl: './requests-summary.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequestsSummaryComponent {
  readonly summary = input<RequestsSummary | null>(null);
  readonly loading = input(false);
  readonly error = input<string | null>(null);
  readonly retry = output<void>();

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly priorityLabels = PRIORITY_LABELS;

  /** אחוז מכלל הפניות — לרוחב העמודה בגרף הפשוט. */
  protected readonly total = computed(() => this.summary()?.total ?? 0);

  protected percent(count: number): number {
    const total = this.total();
    return total > 0 ? Math.round((count / total) * 100) : 0;
  }
}
