import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/paged.models';
import { AuditEvent, AuditEventTypeGroup, AuditQuery } from '../models/audit.models';

/**
 * Reads the security audit trail (paged, filtered, newest first) and the
 * event-type catalog that populates the viewer's filter dropdown.
 */
@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/audit-events`;

  list(query: AuditQuery): Observable<PagedResult<AuditEvent>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<AuditEvent>>(this.base, { params });
  }

  eventTypes(): Observable<AuditEventTypeGroup[]> {
    return this.http.get<AuditEventTypeGroup[]>(`${this.base}/event-types`);
  }
}
