import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../models/auth.models';

/** Normalizes RFC 7807 Problem Details into a typed ApiError for the UI. */
export const apiErrorInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      const problem = error.error ?? {};
      const apiError: ApiError = {
        status: error.status,
        title: problem.title ?? (error.status === 0 ? 'Cannot reach the PaymentFlow API.' : 'Something went wrong.'),
        code: typeof problem.type === 'string' ? problem.type.split('/').pop() : undefined,
        correlationId: problem.correlationId,
        fieldErrors: problem.errors
      };
      return throwError(() => apiError);
    })
  );
