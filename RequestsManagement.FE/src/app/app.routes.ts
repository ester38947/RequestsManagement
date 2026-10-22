import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadChildren: () =>
      import('./features/requests/requests.routes').then((m) => m.REQUESTS_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
