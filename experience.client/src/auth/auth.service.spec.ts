import { TestBed } from '@angular/core/testing';
import { AuthService, Claim, Session } from './auth.service';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import {
  provideHttpClient,
} from '@angular/common/http';

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

  it('should be created', () => {
    const req = httpTestingController.expectOne('bff/user');
    req.flush(null);
    expect(service).toBeTruthy();
  });

  it('should return session data on successful getSession', () => {
    const mockSession: Session = [
      { type: 'name', value: 'TestUser' },
      { type: 'bff:logout_url', value: '/logout' },
    ];

    service.session();
    const req = httpTestingController.expectOne('bff/user');
    expect(req.request.method).toBe('GET');
    req.flush(mockSession);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.isAnonymous()).toBe(false);
    expect(service.username()).toBe('TestUser');
    expect(service.logoutUrl()).toBe('/logout');
  });

  it('should return null session on getSession error', () => {
    service.session();
    const req = httpTestingController.expectOne('bff/user');
    req.error(new ProgressEvent('error'));

    expect(service.isAuthenticated()).toBe(false);
    expect(service.isAnonymous()).toBe(true);
    expect(service.username()).toBeNull();
    expect(service.logoutUrl()).toBeNull();
  });

  it('should cache session data', () => {
    const mockSession: Session = [{ type: 'name', value: 'TestUser' }];

    service.session();
    const firstReq = httpTestingController.expectOne('bff/user');
    firstReq.flush(mockSession);

    service.session();
    httpTestingController.expectNone('bff/user');
    expect(service.isAuthenticated()).toBe(true);
  });

  it('should ignore cache when ignoreCache is true', () => {
    const mockSession1: Session = [{ type: 'name', value: 'User1' }];
    const mockSession2: Session = [{ type: 'name', value: 'User2' }];

    let result1: Session | undefined;
    service.getSession().subscribe(s => result1 = s);
    const req1 = httpTestingController.expectOne('bff/user');
    req1.flush(mockSession1);
    expect(result1).toEqual(mockSession1);

    let result2: Session | undefined;
    service.getSession(true).subscribe(s => result2 = s);
    const req2 = httpTestingController.expectOne('bff/user');
    req2.flush(mockSession2);
    expect(result2).toEqual(mockSession2);

    expect(service.username()).toBe('User1'); // still the old signal data
  });

  it('should handle an empty session response', () => {
    service.session();
    const req = httpTestingController.expectOne('bff/user');
    req.flush([]);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.isAnonymous()).toBe(false);
    expect(service.username()).toBeNull();
    expect(service.logoutUrl()).toBeNull();
  });
});
