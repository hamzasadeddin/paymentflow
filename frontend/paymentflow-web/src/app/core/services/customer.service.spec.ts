import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CustomerService } from './customer.service';
import { environment } from '../../../environments/environment';

describe('CustomerService', () => {
  let service: CustomerService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CustomerService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CustomerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('builds query params for list()', () => {
    service.list({ page: 2, pageSize: 10, search: 'cedar', status: 1 }).subscribe();
    const req = httpMock.expectOne(r => r.url === `${environment.apiUrl}/customers`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('search')).toBe('cedar');
    expect(req.request.params.get('status')).toBe('1');
    req.flush({ items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: true });
  });

  it('omits empty optional params', () => {
    service.list({ page: 1 }).subscribe();
    const req = httpMock.expectOne(r => r.url === `${environment.apiUrl}/customers`);
    expect(req.request.params.has('search')).toBe(false);
    req.flush({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
  });
});
