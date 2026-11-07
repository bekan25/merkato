import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated ? true : router.createUrlTree(['/login']);
};

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated) return router.createUrlTree(['/login']);
  const roles = (route.data['roles'] || []) as string[];
  return auth.hasRole(roles) ? true : router.createUrlTree(['/dashboard']);
};
