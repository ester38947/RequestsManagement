import { InjectionToken } from '@angular/core';

/** The API base URL (without '/api'). Provided in app.config from the environment. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
