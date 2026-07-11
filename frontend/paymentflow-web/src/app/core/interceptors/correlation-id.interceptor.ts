import { HttpInterceptorFn } from '@angular/common/http';

/** Every API call carries a correlation id that the backend echoes and logs. */
export const correlationIdInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ setHeaders: { 'X-Correlation-Id': crypto.randomUUID() } }));
