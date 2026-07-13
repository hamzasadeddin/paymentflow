import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ComplianceCase, ComplianceCaseStatus } from '../models/compliance.models';

/**
 * Reads the compliance review queue and drives clear/reject on holds. Also wraps
 * the role-gated account-number reveal (reused from Phase 02) so the Compliance
 * screen can surface it as a first-class action; the server audits every reveal.
 */
@Injectable({ providedIn: 'root' })
export class ComplianceService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  queue(status: ComplianceCaseStatus | null = ComplianceCaseStatus.Open): Observable<ComplianceCase[]> {
    const query = status === null ? '' : `?status=${status}`;
    return this.http.get<ComplianceCase[]>(`${this.base}/compliance/cases${query}`);
  }

  casesForPayment(paymentId: string): Observable<ComplianceCase[]> {
    return this.http.get<ComplianceCase[]>(`${this.base}/payments/${paymentId}/compliance`);
  }

  clear(caseId: string, notes?: string): Observable<ComplianceCase> {
    return this.http.post<ComplianceCase>(`${this.base}/compliance/cases/${caseId}/clear`, { notes });
  }

  reject(caseId: string, notes?: string): Observable<ComplianceCase> {
    return this.http.post<ComplianceCase>(`${this.base}/compliance/cases/${caseId}/reject`, { notes });
  }

  /** Full account number for privileged roles; the response body is the raw string. */
  revealAccountNumber(accountId: string): Observable<string> {
    return this.http.get(`${this.base}/accounts/${accountId}/reveal-number`, { responseType: 'text' });
  }
}
