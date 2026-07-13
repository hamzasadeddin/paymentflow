import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedQuery, PagedResult } from '../models/paged.models';
import { CreatePaymentRequest, Payment, PaymentStatus } from '../models/payment.models';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/payments`;

  list(query: PagedQuery & {
    status?: PaymentStatus;
    sourceAccountId?: string;
    beneficiaryId?: string;
  }): Observable<PagedResult<Payment>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<Payment>>(this.base, { params });
  }

  get(id: string): Observable<Payment> {
    return this.http.get<Payment>(`${this.base}/${id}`);
  }

  create(request: CreatePaymentRequest): Observable<Payment> {
    // A fresh idempotency key per create attempt makes safe retries possible:
    // resending the same key returns the original payment instead of a duplicate.
    const headers = { 'Idempotency-Key': generateIdempotencyKey() };
    return this.http.post<Payment>(this.base, request, { headers });
  }

  submitForApproval(id: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.base}/${id}/submit-for-approval`, {});
  }

  /** Manually trigger settlement of an approved payment. */
  process(id: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.base}/${id}/process`, {});
  }

  cancel(id: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.base}/${id}/cancel`, {});
  }

  approve(id: string, notes?: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.base}/${id}/approve`, { notes });
  }

  reject(id: string, notes?: string): Observable<Payment> {
    return this.http.post<Payment>(`${this.base}/${id}/reject`, { notes });
  }
}

/** RFC 4122 v4 GUID; uses crypto.randomUUID when available. */
export function generateIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}
