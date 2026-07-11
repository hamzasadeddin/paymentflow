import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Usage: { path: 'admin', canActivate: [roleGuard], data: { roles: [AppRoles.Administrator] } } */
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const allowedRoles = (route.data['roles'] as string[] | undefined) ?? [];
  return allowedRoles.length === 0 || auth.hasAnyRole(allowedRoles)
    ? true
    : router.createUrlTree(['/dashboard']);
};
