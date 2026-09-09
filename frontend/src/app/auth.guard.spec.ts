import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard } from './auth.guard';

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

describe('authGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AuthService, provideHttpClient(), provideRouter([])] });
    sessionStorage.clear();
  });

  it('allows access when a valid non-expired Admin token is present', () => {
    sessionStorage.setItem('ecosologic.token', token({ [ROLE_CLAIM]: 'Admin', exp: futureExp() }));

    const result = TestBed.runInInjectionContext(() => authGuard(undefined as never, undefined as never));

    expect(result).toBeTrue();
  });

  it('redirects to /login when no token is present', () => {
    const result = TestBed.runInInjectionContext(() => authGuard(undefined as never, undefined as never));

    expect(result?.toString()).toBe('/login');
  });

  it('redirects to /login when the token is expired', () => {
    sessionStorage.setItem(
      'ecosologic.token',
      token({ [ROLE_CLAIM]: 'Admin', exp: Math.floor(Date.now() / 1000) - 3600 })
    );

    const result = TestBed.runInInjectionContext(() => authGuard(undefined as never, undefined as never));

    expect(result?.toString()).toBe('/login');
  });

  it('redirects to /login when the token has no Admin role', () => {
    sessionStorage.setItem('ecosologic.token', token({ exp: futureExp() }));

    const result = TestBed.runInInjectionContext(() => authGuard(undefined as never, undefined as never));

    expect(result?.toString()).toBe('/login');
  });
});
