import { Routes } from '@angular/router';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatPaginatorIntl } from '@angular/material/paginator';

import { HebrewPaginatorIntl } from '../../shared/hebrew-paginator-intl';

/**
 * The feature routes (lazy-loaded). The Material providers are here and not in app.config
 * so Material code goes into the page chunk, not the initial bundle.
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
