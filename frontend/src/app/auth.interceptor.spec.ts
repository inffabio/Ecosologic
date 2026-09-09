import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    sessionStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('adds the Authorization header when a token is present', () => {
    sessionStorage.setItem('ecosologic.token', 'token-123');
    httpClient.get('http://localhost:5157/api/leads').subscribe();

    const request = httpMock.expectOne('http://localhost:5157/api/leads');
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-123');
    request.flush({});
  });

  it('does not add the Authorization header when no token is present', () => {
    httpClient.get('http://localhost:5157/api/leads').subscribe();

    const request = httpMock.expectOne('http://localhost:5157/api/leads');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('logs out and redirects to /login on a 401 response', () => {
    sessionStorage.setItem('ecosologic.token', 'token-123');
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    httpClient.get('http://localhost:5157/api/leads').subscribe({ error: () => undefined });
    const request = httpMock.expectOne('http://localhost:5157/api/leads');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem('ecosologic.token')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('logs out and redirects to /login on a 403 response', () => {
    sessionStorage.setItem('ecosologic.token', 'token-123');
    spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));

    httpClient.get('http://localhost:5157/api/leads').subscribe({ error: () => undefined });
    const request = httpMock.expectOne('http://localhost:5157/api/leads');
    request.flush({}, { status: 403, statusText: 'Forbidden' });

    expect(sessionStorage.getItem('ecosologic.token')).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('does not redirect for the login endpoint on a 401 response', () => {
    spyOn(router, 'navigate');

    httpClient
      .post('http://localhost:5157/api/auth/login', { email: 'a@b.com', password: 'x' })
      .subscribe({ error: () => undefined });
    const request = httpMock.expectOne('http://localhost:5157/api/auth/login');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigate).not.toHaveBeenCalled();
    expect(auth.getToken()).toBeNull();
  });

  it('does not redirect for non-auth errors', () => {
    spyOn(router, 'navigate');

    httpClient.get('http://localhost:5157/api/leads').subscribe({ error: () => undefined });
    const request = httpMock.expectOne('http://localhost:5157/api/leads');
    request.flush({}, { status: 500, statusText: 'Server Error' });

    expect(router.navigate).not.toHaveBeenCalled();
  });
});
