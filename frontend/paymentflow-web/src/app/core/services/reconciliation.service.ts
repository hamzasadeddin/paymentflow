import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BreakStatus, ReconciliationBreak, ReconciliationRun } from '../models/reconciliation.models';

/**
 * Drives reconciliation: kick off a run, list past runs, drill into a run's
 * breaks, and resolve/ignore each. The server matches settled payments against a
 * simulated external statement and classifies the differences.
 */
@Injectable({ providedIn: 'root' })
export class ReconciliationService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  run(): Observable<ReconciliationRun> {
    return this.http.post<ReconciliationRun>(`${this.base}/reconciliation/run`, {});
  }

  runs(): Observable<ReconciliationRun[]> {
    return this.http.get<ReconciliationRun[]>(`${this.base}/reconciliation/runs`);
  }

  breaks(runId: string, status: BreakStatus | null = null): Observable<ReconciliationBreak[]> {
    const query = status === null ? '' : `?status=${status}`;
    return this.http.get<ReconciliationBreak[]>(`${this.base}/reconciliation/runs/${runId}/breaks${query}`);
  }

  resolve(breakId: string, notes?: string): Observable<ReconciliationBreak> {
    return this.http.post<ReconciliationBreak>(`${this.base}/reconciliation/breaks/${breakId}/resolve`, { notes });
  }

  ignore(breakId: string, notes?: string): Observable<ReconciliationBreak> {
    return this.http.post<ReconciliationBreak>(`${this.base}/reconciliation/breaks/${breakId}/ignore`, { notes });
  }
}
