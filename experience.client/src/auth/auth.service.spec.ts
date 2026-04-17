import { TestBed } from '@angular/core/testing';
import { AuthService, Claim, Session } from './auth.service';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import {
  provideHttpClient,
} from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

describe('AuthService', () => {
  let service: AuthService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(AuthService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('should be created without making any HTTP requests', () => {
    expect(service).toBeTruthy();
    httpTestingController.expectNone('bff/user');
  });

  it('should return session data after initialize', async () => {
    const mockSession: Session = [
      { type: 'name', value: 'TestUser' },
      { type: 'bff:logout_url', value: '/logout' },
    ];

    const result = firstValueFrom(service.initialize());
    const req = httpTestingController.expectOne('bff/user');
    expect(req.request.method).toBe('GET');
    req.flush(mockSession);
    await result;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.isAnonymous()).toBe(false);
    expect(service.username()).toBe('TestUser');
    expect(service.logoutUrl()).toBe('/logout');
  });

  it('should be unauthenticated on HTTP error', async () => {
    const result = firstValueFrom(service.initialize());
    const req = httpTestingController.expectOne('bff/user');
    req.error(new ProgressEvent('error'));
    await result;

    expect(service.isAuthenticated()).toBe(false);
    expect(service.isAnonymous()).toBe(true);
    expect(service.username()).toBeNull();
    expect(service.logoutUrl()).toBeNull();
    expect(service.session()).toEqual([]);
  });

  it('should not make a new HTTP request when reading session signal after initialize', async () => {
    const mockSession: Session = [{ type: 'name', value: 'TestUser' }];

    const result = firstValueFrom(service.initialize());
    const req = httpTestingController.expectOne('bff/user');
    req.flush(mockSession);
    await result;

    service.session();
    httpTestingController.expectNone('bff/user');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should re-fetch on refresh', async () => {
    const mockSession1: Session = [{ type: 'name', value: 'User1' }];
    const mockSession2: Session = [{ type: 'name', value: 'User2' }];

    const init = firstValueFrom(service.initialize());
    httpTestingController.expectOne('bff/user').flush(mockSession1);
    await init;

    service.refresh();
    httpTestingController.expectOne('bff/user').flush(mockSession2);

    expect(service.username()).toBe('User2');
  });

  it('should treat empty session array as authenticated', async () => {
    const result = firstValueFrom(service.initialize());
    httpTestingController.expectOne('bff/user').flush([]);
    await result;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.isAnonymous()).toBe(false);
    expect(service.session()).toEqual([]);
    expect(service.username()).toBeNull();
    expect(service.logoutUrl()).toBeNull();
  });

  it('should return logoutUrl as-is when sid claim is present', async () => {
    const mockSession: Claim[] = [
      { type: 'bff:logout_url', value: '/bff/logout?sid=abc123' },
      { type: 'sid', value: 'abc123' },
    ];

    const result = firstValueFrom(service.initialize());
    httpTestingController.expectOne('bff/user').flush(mockSession);
    await result;

    expect(service.logoutUrl()).toBe('/bff/logout?sid=abc123');
  });
});
