import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { PaymentService, generateIdempotencyKey } from './payment.service';
import { environment } from '../../../environments/environment';

describe('PaymentService', () => {
  let service: PaymentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PaymentService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(PaymentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('builds query params for list()', () => {
    service.list({ page: 2, pageSize: 10, status: 3, search: 'PAY-2026' }).subscribe();
    const req = httpMock.expectOne(r => r.url === `${environment.apiUrl}/payments`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('status')).toBe('3');
    expect(req.request.params.get('search')).toBe('PAY-2026');
    req.flush({ items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: true });
  });

  it('omits empty optional params', () => {
    service.list({ page: 1 }).subscribe();
    const req = httpMock.expectOne(r => r.url === `${environment.apiUrl}/payments`);
    expect(req.request.params.has('search')).toBe(false);
    expect(req.request.params.has('status')).toBe(false);
    req.flush({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
  });

  it('sends an Idempotency-Key header on create()', () => {
    service.create({ sourceAccountId: 'a', beneficiaryId: 'b', amount: 100, currency: 'USD' }).subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/payments`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.has('Idempotency-Key')).toBe(true);
    expect(req.request.headers.get('Idempotency-Key')!.length).toBeGreaterThan(0);
    req.flush({ id: 'p1' });
  });

  it('posts to the submit-for-approval endpoint', () => {
    service.submitForApproval('p1').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/payments/p1/submit-for-approval`);
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'p1' });
  });

  it('generates distinct idempotency keys', () => {
    const a = generateIdempotencyKey();
    const b = generateIdempotencyKey();
    expect(a).not.toBe(b);
    expect(a.length).toBeGreaterThan(10);
  });
});
