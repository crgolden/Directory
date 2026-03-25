import { TestBed } from '@angular/core/testing';
import { HttpInterceptorFn } from '@angular/common/http';
import { HttpRequest, HttpHeaders, HttpResponse } from '@angular/common/http';
import { appInterceptor } from './app.interceptor';
import { of } from 'rxjs';

describe('appInterceptor', () => {
  const interceptor: HttpInterceptorFn = (req, next) =>
    TestBed.runInInjectionContext(() => appInterceptor(req, next));

  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  it('should set the X-CSRF header to 1', () => {
    const request = new HttpRequest('GET', '/test', { headers: new HttpHeaders() });
    let modifiedReq: HttpRequest<any> | undefined;

    const next = (req: HttpRequest<any>) => {
      modifiedReq = req;
      return of(new HttpResponse({ status: 200 }));
    };

    interceptor(request, next).subscribe(() => {
      expect(modifiedReq).toBeDefined();
      expect(modifiedReq!.headers.get('X-CSRF')).toBe('1');
    });
  });
});
