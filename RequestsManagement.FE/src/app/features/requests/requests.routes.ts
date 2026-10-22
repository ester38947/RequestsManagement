import { Routes } from '@angular/router';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

import { HebrewPaginatorIntl } from '../../shared/hebrew-paginator-intl';

/**
 * נתיבי ה-feature (נטען lazy). ה-providers של Material כאן ולא ב-app.config
 * כדי שקוד Material ייכנס ל-chunk של העמוד ולא ל-bundle הראשוני.
 */
export const REQUESTS_ROUTES: Routes = [
  {
    path: '',
    title: 'ניהול פניות',
    providers: [
      { provide: MAT_DATE_LOCALE, useValue: 'he-IL' },
      provideNativeDateAdapter(),
      { provide: MatPaginatorIntl, useClass: HebrewPaginatorIntl },
    ],
    loadComponent: () => import('./request-list/request-list').then((m) => m.RequestListComponent),
  },
];
