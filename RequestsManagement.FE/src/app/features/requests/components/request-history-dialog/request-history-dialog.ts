import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { finalize } from 'rxjs';

import { toUserMessage } from '../../../../core/http/http-error';
import { STATUS_LABELS } from '../../models/request-labels';
import { StatusHistoryItem } from '../../models/request.models';
import { RequestsService } from '../../services/requests.service';

export interface HistoryDialogData {
  requestId: string;
  title: string;
}

/** דיאלוג היסטוריית שינויי סטטוס (מהחדש לישן). טוען את הנתונים בעצמו, עם מצבי טעינה/ריק/שגיאה. */
@Component({
  selector: 'app-request-history-dialog',
  imports: [DatePipe, MatButtonModule, MatDialogModule, MatProgressBarModule],
  templateUrl: './request-history-dialog.html',
  styleUrl: './request-history-dialog.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RequestHistoryDialogComponent {
  protected readonly data = inject<HistoryDialogData>(MAT_DIALOG_DATA);
  private readonly service = inject(RequestsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly items = signal<StatusHistoryItem[]>([]);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.service
      .getHistory(this.data.requestId)
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (items) => this.items.set(items),
        error: (err: unknown) => this.error.set(toUserMessage(err)),
      });
  }
}
