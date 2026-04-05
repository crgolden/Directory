import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { signal } from '@angular/core';

describe('authGuard', () => {
  const run = () =>
    TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
    );

  it('returns true when authenticated', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { isAuthenticated: signal(true) } }],
    });

    expect(run()).toBe(true);
  });

  it('returns false and redirects to /bff/login when anonymous', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: AuthService, useValue: { isAuthenticated: signal(false) } }],
    });

    // jsdom allows redefining window.location via Object.defineProperty on window
    const hrefSetter = vi.fn();
    Object.defineProperty(window, 'location', {
      value: { ...window.location, set href(v: string) { hrefSetter(v); } },
      writable: true,
      configurable: true,
    });

    const result = run();

    expect(result).toBe(false);
    expect(hrefSetter).toHaveBeenCalledWith('/bff/login');
  });
});
