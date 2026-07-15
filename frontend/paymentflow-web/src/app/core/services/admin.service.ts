import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult, PagedQuery } from '../models/paged.models';
import {
  AdminUser, CreateUserRequest, Rules, RuleSet,
  ApprovalRuleValues, ScreeningRuleValues, ReconciliationRuleValues, ProcessingRuleValues
} from '../models/admin.models';

/**
 * Administration API: user & role management and the four config-backed rule
 * sections. Rule updates carry the section's row version for optimistic
 * concurrency (a stale version comes back as 409).
 */
@Injectable({ providedIn: 'root' })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin`;

  // ---------- Users ----------

  users(query: PagedQuery): Observable<PagedResult<AdminUser>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<AdminUser>>(`${this.base}/users`, { params });
  }

  createUser(request: CreateUserRequest): Observable<AdminUser> {
    return this.http.post<AdminUser>(`${this.base}/users`, request);
  }

  setActive(userId: string, isActive: boolean): Observable<AdminUser> {
    const action = isActive ? 'activate' : 'deactivate';
    return this.http.post<AdminUser>(`${this.base}/users/${userId}/${action}`, {});
  }

  setRoles(userId: string, roles: string[]): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${this.base}/users/${userId}/roles`, { roles });
  }

  resetPassword(userId: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.base}/users/${userId}/reset-password`, { newPassword });
  }

  // ---------- Rules ----------

  rules(): Observable<Rules> {
    return this.http.get<Rules>(`${this.base}/rules`);
  }

  updateApproval(body: ApprovalRuleValues & { rowVersion?: string }): Observable<RuleSet<ApprovalRuleValues>> {
    return this.http.put<RuleSet<ApprovalRuleValues>>(`${this.base}/rules/approval`, body);
  }

  updateScreening(body: ScreeningRuleValues & { rowVersion?: string }): Observable<RuleSet<ScreeningRuleValues>> {
    return this.http.put<RuleSet<ScreeningRuleValues>>(`${this.base}/rules/screening`, body);
  }

  updateReconciliation(
    body: ReconciliationRuleValues & { rowVersion?: string }): Observable<RuleSet<ReconciliationRuleValues>> {
    return this.http.put<RuleSet<ReconciliationRuleValues>>(`${this.base}/rules/reconciliation`, body);
  }

  updateProcessing(body: ProcessingRuleValues & { rowVersion?: string }): Observable<RuleSet<ProcessingRuleValues>> {
    return this.http.put<RuleSet<ProcessingRuleValues>>(`${this.base}/rules/processing`, body);
  }
}
