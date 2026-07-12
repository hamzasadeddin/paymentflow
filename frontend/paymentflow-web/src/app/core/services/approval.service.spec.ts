import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ApprovalService } from './approval.service';
import { environment } from '../../../environments/environment';

describe('ApprovalService', () => {
  let service: ApprovalService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [ApprovalService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ApprovalService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('fetches the unified queue', () => {
    service.queue().subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/approvals`);
    expect(req.request.method).toBe('GET');
    req.flush({ payments: [], beneficiaries: [] });
  });

  it('fetches decision history for a payment', () => {
    service.paymentDecisions('p1').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/payments/p1/approvals`);
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('posts notes when approving a payment', () => {
    service.approvePayment('p1', 'looks good').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/payments/p1/approve`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ notes: 'looks good' });
    req.flush({ id: 'p1' });
  });

  it('posts to the beneficiary reject endpoint', () => {
    service.rejectBeneficiary('b1', 'no').subscribe();
    const req = httpMock.expectOne(`${environment.apiUrl}/beneficiaries/b1/reject`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ notes: 'no' });
    req.flush({ id: 'b1' });
  });
});
