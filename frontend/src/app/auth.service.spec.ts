import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

function base64url(value: object): string {
  return btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function token(payload: Record<string, unknown>): string {
  return `e30.${base64url(payload)}.signature`;
}

function futureExp(): number {
  return Math.floor(Date.now() / 1000) + 3600;
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    sessionStorage.clear();
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('posts credentials and stores the returned token in sessionStorage', () => {
    service.login('admin@ecosologic.com.br', 's3nh4').subscribe((result) => {
      expect(result).toBe('jwt-token');
      expect(sessionStorage.getItem('ecosologic.token')).toBe('jwt-token');
    });

    const request = http.expectOne('http://localhost:5157/api/auth/login');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'admin@ecosologic.com.br', password: 's3nh4' });
    request.flush({ token: 'jwt-token' });
  });

  it('isAuthenticated is false when no token is stored', () => {
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('isAuthenticated is true for a valid non-expired Admin token', () => {
    sessionStorage.setItem('ecosologic.token', token({ [ROLE_CLAIM]: 'Admin', exp: futureExp() }));

    expect(service.isAuthenticated()).toBeTrue();
  });

  it('isAuthenticated is false for an expired token', () => {
    sessionStorage.setItem(
      'ecosologic.token',
      token({ [ROLE_CLAIM]: 'Admin', exp: Math.floor(Date.now() / 1000) - 3600 })
    );

    expect(service.isAuthenticated()).toBeFalse();
  });

  it('isAuthenticated is false when the token has no Admin role', () => {
    sessionStorage.setItem('ecosologic.token', token({ exp: futureExp() }));

    expect(service.isAuthenticated()).toBeFalse();
  });

  it('isAuthenticated is false when the role is not Admin', () => {
    sessionStorage.setItem('ecosologic.token', token({ [ROLE_CLAIM]: 'User', exp: futureExp() }));

    expect(service.isAuthenticated()).toBeFalse();
  });

  it('logout clears the stored token', () => {
    sessionStorage.setItem('ecosologic.token', token({ [ROLE_CLAIM]: 'Admin', exp: futureExp() }));
    service.logout();

    expect(sessionStorage.getItem('ecosologic.token')).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });
});
