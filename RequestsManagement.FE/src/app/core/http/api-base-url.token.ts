import { InjectionToken } from '@angular/core';

/** כתובת הבסיס של ה-API (ללא '/api'). מסופק ב-app.config מתוך environment. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
