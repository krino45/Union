import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const universityGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated) {
    router.navigate(['/login']);
    return false;
  }
  if (auth.isSuperAdmin) return true;
  if (auth.currentUniversity) return true;
  router.navigate(['/select-university']);
  return false;
};

export const superAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isSuperAdmin) return true;
  router.navigate(['/']);
  return false;
};
