import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedQuery, PagedResult } from '../models/paged.models';
import {
  AccountSummary, CreateCustomerRequest, CustomerDetail, CustomerStatus, CustomerSummary
} from '../models/customer.models';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/customers`;

  list(query: PagedQuery & { status?: CustomerStatus }): Observable<PagedResult<CustomerSummary>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<CustomerSummary>>(this.base, { params });
  }

  get(id: string): Observable<CustomerDetail> {
    return this.http.get<CustomerDetail>(`${this.base}/${id}`);
  }

  create(request: CreateCustomerRequest): Observable<CustomerDetail> {
    return this.http.post<CustomerDetail>(this.base, request);
  }

  accounts(customerId: string): Observable<AccountSummary[]> {
    return this.http.get<AccountSummary[]>(`${this.base}/${customerId}/accounts`);
  }
}
