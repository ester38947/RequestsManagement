import { HttpClient, HttpParams } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from './api-base-url.token';

/** פרמטרי query; ערכי undefined/null/ריקים מושמטים. */
export type QueryParams = Record<string, string | number | null | undefined>;

/**
 * בסיס לכל שירותי ה-HTTP: מרכיב את ה-URL ‏(`{apiBaseUrl}/api/{servicePath}/{path}`)
 * ובונה HttpParams. ללא gateway — הנתיב הוא '/api/' ישירות.
 */
export abstract class BaseHttpService {
  protected readonly http = inject(HttpClient);
  protected readonly apiBaseUrl = inject(API_BASE_URL);
  protected abstract readonly servicePath: string;

  protected get baseUrl(): string {
    return `${this.apiBaseUrl}/api/${this.servicePath}`;
  }

  protected get<T>(path: string, params?: QueryParams): Observable<T> {
    return this.http.get<T>(this.url(path), { params: this.toHttpParams(params) });
  }

  protected post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(this.url(path), body);
  }

  protected patch<T>(path: string, body: unknown): Observable<T> {
    return this.http.patch<T>(this.url(path), body);
  }

  private url(path: string): string {
    return path ? `${this.baseUrl}/${path}` : this.baseUrl;
  }

  private toHttpParams(params?: QueryParams): HttpParams {
    let httpParams = new HttpParams();
    for (const [key, value] of Object.entries(params ?? {})) {
      if (value !== undefined && value !== null && value !== '') {
        httpParams = httpParams.set(key, String(value));
      }
    }
    return httpParams;
  }
}
