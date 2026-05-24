import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const universityId = authService.currentUniversity?.universityId;

  // The JWT travels in an httpOnly cookie, so every request must send credentials. The selected
  // university is not secret and stays a header — but never clobber one a caller set explicitly
  // (the superadmin invite dialog targets a specific university that way).
  const headers: Record<string, string> = {};
  if (universityId && !req.headers.has('X-University-Id')) headers['X-University-Id'] = universityId;

  const authReq = req.clone({ withCredentials: true, setHeaders: headers });

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      // 401 = the session cookie is missing/expired.
      if (err.status === 401 && !req.url.endsWith('/auth/me') && !req.url.endsWith('/auth/login')) {
        authService.handleUnauthorized();
      }
      return throwError(() => err);
    })
  );
};
