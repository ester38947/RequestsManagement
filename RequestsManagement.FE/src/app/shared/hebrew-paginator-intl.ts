import { Injectable } from '@angular/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

/** תוויות paginator בעברית. */
@Injectable()
export class HebrewPaginatorIntl extends MatPaginatorIntl {
  override itemsPerPageLabel = 'פריטים בעמוד:';
  override nextPageLabel = 'העמוד הבא';
  override previousPageLabel = 'העמוד הקודם';
  override firstPageLabel = 'העמוד הראשון';
  override lastPageLabel = 'העמוד האחרון';

  override getRangeLabel = (page: number, pageSize: number, length: number): string => {
    if (length === 0 || pageSize === 0) {
      return `0 מתוך ${length.toLocaleString('he-IL')}`;
    }
    const start = page * pageSize;
    const end = Math.min(start + pageSize, length);
    return `${(start + 1).toLocaleString('he-IL')}–${end.toLocaleString('he-IL')} מתוך ${length.toLocaleString('he-IL')}`;
  };
}
