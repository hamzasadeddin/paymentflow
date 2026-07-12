import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApprovalDecision, ApprovalQueue } from '../models/approval.models';
import { Payment } from '../models/payment.models';
import { Beneficiary } from '../models/beneficiary.models';

/**
 * Reads the unified maker-checker queue and drives approve/reject on the two
 * subject types. Approve/reject reuse the existing payment/beneficiary
 * endpoints; the server enforces separation of duties and dual control.
 */
@Injectable({ providedIn: 'root' })
export class ApprovalService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  queue(): Observable<ApprovalQueue> {
    return this.http.get<ApprovalQueue>(`${this.base}/approvals`);
  }

  paymentDecisions(paymentId: string): Observable<ApprovalDecision[]> {
    return this.http.get<ApprovalDecision[]>(`${this.base}/payments/${paymentId}/approvals`);
  }

  approvePayment(id: string, notes?: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.base}/payments/${id}/approve`, { notes });
  }

  rejectPayment(id: string, notes?: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.base}/payments/${id}/reject`, { notes });
  }

  approveBeneficiary(id: string, notes?: string): Observable<Beneficiary> {
    return this.http.post<Beneficiary>(`${this.base}/beneficiaries/${id}/approve`, { notes });
  }

  rejectBeneficiary(id: string, notes?: string): Observable<Beneficiary> {
    return this.http.post<Beneficiary>(`${this.base}/beneficiaries/${id}/reject`, { notes });
  }
}
