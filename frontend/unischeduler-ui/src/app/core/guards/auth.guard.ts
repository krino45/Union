import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated) return true;
  if (auth.isSuperAdmin) { router.navigate(['/superadmin']); return false; }
  if (auth.currentUniversity) {
    router.navigate([auth.isAdmin ? '/admin/schedules' : '/teacher/my-schedule']);
    return false;
  }
  router.navigate(['/select-university']);
  return false;
};

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated) return true;
  router.navigate(['/login']);
  return false;
};

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAdmin) return true;
  router.navigate(['/']);
  return false;
};

export const teacherGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated) return true;
  router.navigate(['/login']);
  return false;
};
