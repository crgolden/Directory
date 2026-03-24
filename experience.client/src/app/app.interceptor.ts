import { HttpInterceptorFn } from '@angular/common/http';

export const appInterceptor: HttpInterceptorFn = (req, next) => {
  let headers = req.headers.set('X-CSRF', '1');
  const modifiedRequest = req.clone({
    withCredentials: true,
    headers: headers
  });
  return next(modifiedRequest);
};
