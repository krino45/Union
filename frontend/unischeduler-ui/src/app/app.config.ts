import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { MAT_DIALOG_DEFAULT_OPTIONS, MatDialogConfig } from '@angular/material/dialog';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';

const dialogDefaults: MatDialogConfig = {
  disableClose: true,
  maxWidth: '95vw',
  maxHeight: '92vh',
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: MAT_DIALOG_DEFAULT_OPTIONS, useValue: dialogDefaults }
  ]
};
