import { Routes } from '@angular/router';
import { authGuard, adminGuard } from './core/guards/auth.guard';
import { universityGuard, superAdminGuard } from './core/guards/university.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'select-university', pathMatch: 'full' },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register.component').then(m => m.RegisterComponent)
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent)
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./features/auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent)
  },
  {
    path: 'select-university',
    canActivate: [authGuard],
    loadComponent: () => import('./features/university-select/university-select.component').then(m => m.UniversitySelectComponent)
  },
  {
    path: 'superadmin',
    canActivate: [authGuard, superAdminGuard],
    loadComponent: () => import('./features/superadmin/superadmin.component').then(m => m.SuperAdminComponent)
  },
  {
    path: 'admin',
    canActivate: [authGuard, universityGuard, adminGuard],
    children: [
      { path: '', redirectTo: 'schedules', pathMatch: 'full' },
      {
        path: 'schedules',
        loadComponent: () => import('./features/admin/schedule-generator/schedule-generator.component').then(m => m.ScheduleGeneratorComponent)
      },
      {
        path: 'schedules/:id/editor',
        loadComponent: () => import('./features/admin/schedule-editor/schedule-editor.component').then(m => m.ScheduleEditorComponent)
      },
      {
        path: 'buildings',
        loadComponent: () => import('./features/admin/data-management/buildings/buildings.component').then(m => m.BuildingsComponent)
      },
      {
        path: 'rooms',
        loadComponent: () => import('./features/admin/data-management/rooms/rooms.component').then(m => m.RoomsComponent)
      },
      {
        path: 'teachers',
        loadComponent: () => import('./features/admin/data-management/teachers/teachers.component').then(m => m.TeachersComponent)
      },
      {
        path: 'subjects',
        loadComponent: () => import('./features/admin/data-management/subjects/subjects.component').then(m => m.SubjectsComponent)
      },
      {
        path: 'groups',
        loadComponent: () => import('./features/admin/data-management/groups/groups.component').then(m => m.GroupsComponent)
      },
      {
        path: 'faculties',
        loadComponent: () => import('./features/admin/data-management/faculties/faculties.component').then(m => m.FacultiesComponent)
      },
      {
        path: 'departments',
        loadComponent: () => import('./features/admin/data-management/departments/departments.component').then(m => m.DepartmentsComponent)
      },
      {
        path: 'reschedule-requests',
        loadComponent: () => import('./features/admin/reschedule-requests/reschedule-requests.component').then(m => m.RescheduleRequestsComponent)
      },
      {
        path: 'excel',
        loadComponent: () => import('./features/admin/excel/excel.component').then(m => m.ExcelComponent)
      },
      {
        path: 'study-plans',
        loadComponent: () => import('./features/admin/data-management/study-plans/study-plans.component').then(m => m.StudyPlansComponent)
      },
      {
        path: 'calendar-plans',
        loadComponent: () => import('./features/admin/data-management/calendar-plans/calendar-plans.component').then(m => m.CalendarPlansComponent)
      },
      {
        path: 'floor-plan',
        loadComponent: () => import('./features/admin/floor-plan-editor/floor-plan-editor.component').then(m => m.FloorPlanEditorComponent)
      }
    ]
  },
  {
    path: 'teacher',
    canActivate: [authGuard, universityGuard],
    children: [
      { path: '', redirectTo: 'my-schedule', pathMatch: 'full' },
      {
        path: 'my-schedule',
        loadComponent: () => import('./features/teacher-portal/my-schedule/my-schedule.component').then(m => m.MyScheduleComponent)
      },
      {
        path: 'availability',
        loadComponent: () => import('./features/teacher-portal/availability-editor/availability-editor.component').then(m => m.AvailabilityEditorComponent)
      },
      {
        path: 'reschedule',
        loadComponent: () => import('./features/teacher-portal/reschedule-form/reschedule-form.component').then(m => m.RescheduleFormComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
