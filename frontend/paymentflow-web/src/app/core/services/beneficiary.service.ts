import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedQuery, PagedResult } from '../models/paged.models';
import { Beneficiary, BeneficiaryStatus, CreateBeneficiaryRequest } from '../models/beneficiary.models';

@Injectable({ providedIn: 'root' })
export class BeneficiaryService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/beneficiaries`;

  list(query: PagedQuery & { status?: BeneficiaryStatus; customerId?: string }): Observable<PagedResult<Beneficiary>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<Beneficiary>>(this.base, { params });
  }

  create(request: CreateBeneficiaryRequest): Observable<Beneficiary> {
    return this.http.post<Beneficiary>(this.base, request);
  }

  submitForApproval(id: string): Observable<Beneficiary> {
    return this.http.post<Beneficiary>(`${this.base}/${id}/submit-for-approval`, {});
  }

  approve(id: string, notes?: string): Observable<Beneficiary> {
    return this.http.post<Beneficiary>(`${this.base}/${id}/approve`, { notes });
  }

  reject(id: string, notes?: string): Observable<Beneficiary> {
    return this.http.post<Beneficiary>(`${this.base}/${id}/reject`, { notes });
  }
}
