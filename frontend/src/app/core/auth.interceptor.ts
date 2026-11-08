import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = sessionStorage.getItem('merkato_access_token');
  if (!token || !request.url.startsWith('/api/')) return next(request);

  const router = inject(Router);
  const authenticatedRequest = request.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      // Access tokens are short-lived. Clear expired/revoked sessions instead
      // of repeatedly sending an invalid bearer token to protected endpoints.
      if (error instanceof HttpErrorResponse && error.status === 401) {
        sessionStorage.removeItem('merkato_access_token');
        sessionStorage.removeItem('merkato_user');
        if (router.url !== '/login') void router.navigateByUrl('/login');
      }
      return throwError(() => error);
    })
  );
};
