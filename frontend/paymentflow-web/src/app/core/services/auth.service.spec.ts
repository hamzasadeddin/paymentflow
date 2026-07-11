import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { AuthSession } from '../models/auth.models';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const session: AuthSession = {
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: new Date(Date.now() + 900_000).toISOString(),
    refreshToken: 'refresh-token',
    user: { id: '1', email: 'admin@paymentflow.local', displayName: 'Ava Admin', roles: ['Administrator'] }
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.currentUser()).toBeNull();
  });

  it('stores the session after login', () => {
    service.login('admin@paymentflow.local', 'pw').subscribe();

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(session);

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.accessToken()).toBe('access-token');
    expect(service.hasAnyRole(['Administrator'])).toBeTrue();
    expect(service.hasAnyRole(['ComplianceOfficer'])).toBeFalse();
  });

  it('clears the session on logout even if the API call fails', () => {
    service.login('admin@paymentflow.local', 'pw').subscribe();
    httpMock.expectOne(`${environment.apiUrl}/auth/login`).flush(session);

    service.logout().subscribe({ error: () => undefined });
    httpMock.expectOne(`${environment.apiUrl}/auth/logout`).flush(null, { status: 500, statusText: 'Server Error' });

    expect(service.isAuthenticated()).toBeFalse();
  });
});
